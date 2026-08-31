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
        private static readonly ProtoModId[] InstalledMods =
        {
            ProtoModId.BlinkyLed, ProtoModId.AccelTemp, ProtoModId.ElectronicLoad
        };

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
            {
                // 2 bytes little-endian per ProtoModId, matching the widened wire format.
                var payload = InstalledMods
                    .SelectMany(m => new[] { (byte)((ushort)m & 0xFF), (byte)((ushort)m >> 8) })
                    .ToArray();
                return new ProtocolFrame(ProtoModId.Core, MsgType.PresenceReport, payload);
            }

            if (frame.Type != MsgType.Command)
                return null;

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
