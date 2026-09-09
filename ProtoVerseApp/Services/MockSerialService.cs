using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ProtoVerseApp.Models;

namespace ProtoVerseApp.Services
{
    /// <summary>
    /// Fake transport that stands in for a real ProtoCore connection so the UI can be
    /// built and exercised without hardware plugged in. "Connects" instantly, reports
    /// a fixed set of ProtoMods present, and generates plausible command responses and
    /// periodic telemetry for the module that streams data (Accel+Temp) - values are
    /// synthetic (sine/cosine + jitter), not meant to be accurate, just enough to see
    /// the UI move. Electronic Load is response-only (no ADC feedback on real
    /// hardware, so nothing to stream) - see BuildLoadTelemetry.
    /// </summary>
    public class MockSerialService : ISerialService
    {
        /// <summary>What the fake ProtoCore reports as plugged in, one entry per
        /// physical slot. Settable at runtime so the Simulator can swap boards the way
        /// a person swaps them on the bench - see <see cref="SetInstalledMods"/>.
        ///
        /// Per-instance rather than static: toggling Simulator mode constructs a new
        /// MockSerialService, and a static array would leak one session's arrangement
        /// into the next.</summary>
        private ProtoModId[] _installedMods =
        {
            ProtoModId.BlinkyLed, ProtoModId.AccelTemp, ProtoModId.ElectronicLoad
        };

        /// <summary>Replaces the simulated slot lineup and, if connected, immediately
        /// volunteers a fresh PresenceReport - exactly as real firmware does when it
        /// notices a hot-swap. That means the app's normal rebuild path handles this,
        /// with no simulator-specific branch anywhere in the view models.</summary>
        public void SetInstalledMods(IReadOnlyList<ProtoModId> mods)
        {
            _installedMods = mods.ToArray();

            if (IsConnected)
                ScheduleReply(BuildPresenceReport());
        }

        public IReadOnlyList<ProtoModId> InstalledMods => _installedMods;

        private readonly Random _rng = new();
        private readonly List<Timer> _pendingReplies = new();
        private readonly object _lock = new();

        private Timer? _telemetryTimer;
        private double _elapsedSeconds;
        private int _currentLimitMa = 100;

        // BlinkyLed state - mirrors what real firmware echoes back as a 7-byte
        // snapshot in every Command's Response (see BuildBlinkyStateResponse).
        private bool _blinkyEnabled;
        private BlinkyLedMode _blinkyMode = BlinkyLedMode.Animated;
        private BlinkyLedPattern _blinkyPattern = BlinkyLedPattern.Bounce;
        private bool _blinkyReverse;
        private ushort _blinkyPeriodMs = 500;
        private byte _blinkyManualMask;

        public bool IsConnected { get; private set; }

        public event Action<ProtocolFrame>? FrameReceived;
        public event Action<string>? FrameError;

        /// <summary>Never raised - there's no real cable to pull on the simulator.</summary>
        public event Action<string>? Disconnected;

        public void Connect(string portName, int baudRate = 115200)
        {
            IsConnected = true;
            _telemetryTimer = new Timer(_ => EmitTelemetry(), null, 1000, 1000);
        }

        public void Disconnect()
        {
            IsConnected = false;

            _telemetryTimer?.Dispose();
            _telemetryTimer = null;

            lock (_lock)
            {
                foreach (var timer in _pendingReplies)
                    timer.Dispose();
                _pendingReplies.Clear();
            }
        }

        public void Send(ProtocolFrame frame)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Serial port is not open.");

            var reply = BuildReply(frame);
            if (reply != null)
                ScheduleReply(reply);
        }

        private void ScheduleReply(ProtocolFrame reply)
        {
            // Small fake latency so replies don't feel suspiciously instant.
            Timer timer = null!;
            timer = new Timer(_ =>
            {
                lock (_lock) _pendingReplies.Remove(timer);
                if (IsConnected)
                    FrameReceived?.Invoke(reply);
                timer.Dispose();
            }, null, 40, Timeout.Infinite);

            lock (_lock) _pendingReplies.Add(timer);
        }

        private ProtocolFrame? BuildReply(ProtocolFrame frame)
        {
            if (frame.ModuleId == ProtoModId.Core && frame.Type == MsgType.PresenceRequest)
                return BuildPresenceReport();

            if (frame.Type != MsgType.Command)
                return null;

            // ProtoCore's own supply rails, addressed to Core rather than to a slot.
            if (frame.ModuleId == ProtoModId.Core)
                return BuildRailReply(frame);

            // Sub-command byte conventions here match the placeholders in the panel
            // view models (BlinkyLedViewModel, ElectronicLoadViewModel) - update if
            // those change.
            switch (frame.ModuleId)
            {
                case ProtoModId.BlinkyLed when frame.Payload.Length >= 2 && frame.Payload[0] == 0x01: // SetState
                    _blinkyEnabled = frame.Payload[1] != 0;
                    return BuildBlinkyStateResponse();

                case ProtoModId.BlinkyLed when frame.Payload.Length >= 3 && frame.Payload[0] == 0x02: // SetBlinkRate
                    _blinkyPeriodMs = (ushort)(frame.Payload[1] | (frame.Payload[2] << 8));
                    return BuildBlinkyStateResponse();

                case ProtoModId.BlinkyLed when frame.Payload.Length >= 2 && frame.Payload[0] == 0x03: // SetPattern
                    _blinkyPattern = (BlinkyLedPattern)frame.Payload[1];
                    _blinkyMode = BlinkyLedMode.Animated;
                    return BuildBlinkyStateResponse();

                case ProtoModId.BlinkyLed when frame.Payload.Length >= 2 && frame.Payload[0] == 0x04: // SetDirection
                    _blinkyReverse = frame.Payload[1] != 0;
                    return BuildBlinkyStateResponse();

                case ProtoModId.BlinkyLed when frame.Payload.Length >= 2 && frame.Payload[0] == 0x05: // SetManualLeds
                    _blinkyManualMask = frame.Payload[1];
                    _blinkyMode = BlinkyLedMode.Manual;
                    return BuildBlinkyStateResponse();

                case ProtoModId.ElectronicLoad when frame.Payload.Length >= 3 && frame.Payload[0] == 0x01:
                    _currentLimitMa = frame.Payload[1] | (frame.Payload[2] << 8);
                    return BuildLoadTelemetry();

                default:
                    return null;
            }
        }

        /// <summary>One ProtoModId per slot, 2 bytes little-endian each, in slot order -
        /// the fixed-size format real firmware uses. An empty slot reports None rather
        /// than being omitted.</summary>
        private ProtocolFrame BuildPresenceReport()
        {
            var payload = _installedMods
                .SelectMany(m => new[] { (byte)((ushort)m & 0xFF), (byte)((ushort)m >> 8) })
                .ToArray();
            return new ProtocolFrame(ProtoModId.Core, MsgType.PresenceReport, payload);
        }

        /// <summary>Simulated rail state. Starts all-off, which is what real hardware
        /// does at power-up: every enable line has a pull-down holding its regulator
        /// off until firmware drives it.</summary>
        private readonly bool[] _rails = new bool[6];

        /// <summary>Rail control, addressed to Core. Mirrors firmware's behavior
        /// closely enough to exercise the UI properly - including the interlocks,
        /// because "what happens when I switch off a rail something else runs from"
        /// is the main thing worth rehearsing without hardware.</summary>
        private ProtocolFrame? BuildRailReply(ProtocolFrame frame)
        {
            if (frame.Payload.Length < 1)
                return RailError(ProtocolErrBadPayloadLen);

            switch (frame.Payload[0])
            {
                case VoltageRailCatalog.CmdGetRails:
                    return BuildRailSnapshot();

                case VoltageRailCatalog.CmdSetRail:
                    if (frame.Payload.Length < 3)
                        return RailError(ProtocolErrBadPayloadLen);

                    byte railIndex = frame.Payload[1];
                    byte state = frame.Payload[2];
                    if (railIndex >= _rails.Length || state > 1)
                        return RailError(ProtocolErrBadValue);

                    var id = (RailId)railIndex;
                    if (state == 1)
                    {
                        // Walk up enabling ancestors, exactly as firmware does - so a
                        // single click can legitimately bring up three rails, and the
                        // snapshot below reports all of them.
                        foreach (var ancestor in VoltageRailCatalog.AncestorsOf(id))
                            _rails[(int)ancestor.Id] = true;
                        _rails[railIndex] = true;
                    }
                    else
                    {
                        // Refuse while anything is still powered from this rail.
                        foreach (var child in VoltageRailCatalog.ChildrenOf(id))
                        {
                            if (_rails[(int)child.Id])
                                return RailError(VoltageRailCatalog.ErrDependency);
                        }
                        _rails[railIndex] = false;
                    }
                    return BuildRailSnapshot();

                default:
                    return RailError(ProtocolErrUnknownMsgType);
            }
        }

        /// <summary>[rail_count, one byte per rail] - the same snapshot every rail
        /// sub-command returns.</summary>
        private ProtocolFrame BuildRailSnapshot()
        {
            var payload = new byte[_rails.Length + 1];
            payload[0] = (byte)_rails.Length;
            for (int i = 0; i < _rails.Length; i++)
                payload[i + 1] = (byte)(_rails[i] ? 1 : 0);
            return new ProtocolFrame(ProtoModId.Core, MsgType.Response, payload);
        }

        private static ProtocolFrame RailError(byte code) =>
            new(ProtoModId.Core, MsgType.Error, new[] { code });

        private const byte ProtocolErrUnknownMsgType = 0x02;
        private const byte ProtocolErrBadPayloadLen = 0x03;
        private const byte ProtocolErrBadValue = 0x05;

        /// <summary>Every BlinkyLed Command gets back the same 7-byte full-state
        /// snapshot, regardless of which sub-command triggered it - matches the
        /// firmware-side format agreed cross-session (see CHANGELOG.md).</summary>
        private ProtocolFrame BuildBlinkyStateResponse()
        {
            var payload = new byte[7];
            payload[0] = (byte)(_blinkyEnabled ? 1 : 0);
            payload[1] = (byte)_blinkyMode;
            payload[2] = (byte)_blinkyPattern;
            payload[3] = (byte)(_blinkyReverse ? 1 : 0);
            WriteUInt16LE(payload, 4, _blinkyPeriodMs);
            payload[6] = _blinkyManualMask;
            return new ProtocolFrame(ProtoModId.BlinkyLed, MsgType.Response, payload);
        }

        private void EmitTelemetry()
        {
            if (!IsConnected) return;

            _elapsedSeconds += 1.0;
            FrameReceived?.Invoke(BuildAccelTempTelemetry());
        }

        private ProtocolFrame BuildAccelTempTelemetry()
        {
            sbyte tempC = (sbyte)(22 + 3 * Math.Sin(_elapsedSeconds / 5.0));
            short x = (short)(1000 * Math.Sin(_elapsedSeconds));
            short y = (short)(1000 * Math.Cos(_elapsedSeconds));
            short z = (short)(-980 + _rng.Next(-10, 10));

            var payload = new byte[7];
            payload[0] = unchecked((byte)tempC);
            WriteInt16LE(payload, 1, x);
            WriteInt16LE(payload, 3, y);
            WriteInt16LE(payload, 5, z);
            return new ProtocolFrame(ProtoModId.AccelTemp, MsgType.StreamData, payload);
        }

        /// <summary>Matches the real 3-byte Response firmware now sends for
        /// SetCurrentLimitMa: an echo of the commanded current (this board has no
        /// ADC feedback, so there's no measured value to report) plus the PWM duty
        /// cycle firmware computed for it. Duty here uses the same first-pass
        /// calibration firmware described (I*R=V, V/VDD=duty, R=10ohm, VDD=3.3V
        /// nominal) - not settled, real hardware verification is still pending on
        /// the firmware side, so treat this as illustrative only.</summary>
        private ProtocolFrame BuildLoadTelemetry()
        {
            const double senseResistorOhms = 10.0;
            const double supplyVoltage = 3.3;

            double voltage = (_currentLimitMa / 1000.0) * senseResistorOhms;
            byte dutyPercent = (byte)Math.Clamp(voltage / supplyVoltage * 100.0, 0, 100);

            var payload = new byte[3];
            WriteUInt16LE(payload, 0, (ushort)_currentLimitMa);
            payload[2] = dutyPercent;
            return new ProtocolFrame(ProtoModId.ElectronicLoad, MsgType.Response, payload);
        }

        private static void WriteInt16LE(byte[] buffer, int offset, short value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteUInt16LE(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)(value >> 8);
        }

        public void Dispose() => Disconnect();
    }
}
