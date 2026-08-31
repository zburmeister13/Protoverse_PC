using System.Text;

namespace ProtoVerseApp.Models
{
    /// <summary>
    /// Turns a raw <see cref="ProtocolFrame"/> into a short, human-readable summary
    /// for the Traffic Log's Info column - e.g. "SetCurrentLimitMa: 100 mA" instead
    /// of making a reader hand-decode "01 64 00" themselves against the wire spec.
    /// Mirrors the payload layouts already implemented in each panel's own
    /// OnFrameReceived/SendCommand (BlinkyLedViewModel, ElectronicLoadViewModel,
    /// AccelTempViewModel, MainViewModel's PresenceReport handling) and
    /// MockSerialService - if one of those layouts ever changes, update the
    /// matching case here too. Every case is guarded by an explicit payload-length
    /// check rather than trusting the length a given MsgType/ProtoModId combination
    /// is supposed to carry - the frame reader only guarantees a checksum-valid
    /// frame, not that a module actually sent the payload shape this app expects
    /// (a real, seen-in-practice case: a firmware bug or a not-yet-implemented
    /// command can send back something shorter). Falls back to a generic
    /// description rather than showing nothing, and never throws - the raw bytes
    /// are always still visible in the other columns regardless.
    /// </summary>
    public static class FrameInterpreter
    {
        // The complete set as of 2026-08-30, confirmed directly against firmware's
        // Core/Inc/protocol.h by the firmware session (0x01-0x05, no gaps) - not
        // guessed. Any code outside this set falls back to a generic
        // "0xNN (unrecognized)" rather than risk mislabeling a future addition.
        private const byte ErrNotPresent = 0x01;
        private const byte ErrUnknownMsgType = 0x02;
        private const byte ErrBadPayloadLen = 0x03;
        private const byte ErrNotImplemented = 0x04;
        private const byte ErrBadValue = 0x05;

        private const byte BlinkyCmdSetState = 0x01;
        private const byte BlinkyCmdSetBlinkRateMs = 0x02;
        private const byte BlinkyCmdSetPattern = 0x03;
        private const byte BlinkyCmdSetDirection = 0x04;
        private const byte BlinkyCmdSetManualLeds = 0x05;

        private const byte LoadCmdSetCurrentLimitMa = 0x01;

        public static string Describe(ProtocolFrame frame) => frame.Type switch
        {
            MsgType.Error => DescribeError(frame),
            MsgType.PresenceRequest => "Query all installed ProtoMods",
            MsgType.PresenceReport => DescribePresenceReport(frame),
            MsgType.Command => DescribeCommand(frame),
            MsgType.Response => DescribeResponse(frame),
            MsgType.StreamData => DescribeStreamData(frame),
            _ => "-"
        };

        private static string DescribeError(ProtocolFrame frame)
        {
            if (frame.Payload.Length < 1)
                return "Error (no code in payload)";

            byte code = frame.Payload[0];
            string? name = code switch
            {
                ErrNotPresent => "PROTOCOL_ERR_NOT_PRESENT",
                ErrUnknownMsgType => "PROTOCOL_ERR_UNKNOWN_MSGTYPE",
                ErrBadPayloadLen => "PROTOCOL_ERR_BAD_PAYLOAD_LEN",
                ErrNotImplemented => "PROTOCOL_ERR_NOT_IMPLEMENTED",
                ErrBadValue => "PROTOCOL_ERR_BAD_VALUE",
                _ => null
            };
            return name is null
                ? $"Error: unrecognized code 0x{code:X2}"
                : $"Error: {name} (0x{code:X2})";
        }

        private static string DescribePresenceReport(ProtocolFrame frame)
        {
            if (frame.Payload.Length == 0 || frame.Payload.Length % 2 != 0)
                return $"Malformed PresenceReport ({frame.Payload.Length} byte(s), expected an even count)";

            var sb = new StringBuilder();
            int slotCount = frame.Payload.Length / 2;
            for (int slot = 0; slot < slotCount; slot++)
            {
                var moduleId = (ProtoModId)(ushort)(frame.Payload[slot * 2] | (frame.Payload[slot * 2 + 1] << 8));
                if (slot > 0) sb.Append(", ");
                sb.Append($"Slot {slot}: {(moduleId == ProtoModId.None ? "empty" : moduleId.ToString())}");
            }
            return sb.ToString();
        }

        private static string DescribeCommand(ProtocolFrame frame)
        {
            if (frame.Payload.Length == 0)
                return "Command (no payload)";

            byte subCommand = frame.Payload[0];

            if (frame.ModuleId == ProtoModId.BlinkyLed)
            {
                switch (subCommand)
                {
                    case BlinkyCmdSetState when frame.Payload.Length >= 2:
                        return $"SetState: {(frame.Payload[1] != 0 ? "On" : "Off")}";
                    case BlinkyCmdSetBlinkRateMs when frame.Payload.Length >= 3:
                        return $"SetBlinkRate: {ReadUInt16LE(frame.Payload, 1)} ms/step";
                    case BlinkyCmdSetPattern when frame.Payload.Length >= 2:
                        return $"SetPattern: {(BlinkyLedPattern)frame.Payload[1]}";
                    case BlinkyCmdSetDirection when frame.Payload.Length >= 2:
                        return $"SetDirection: {(frame.Payload[1] != 0 ? "Reverse" : "Forward")}";
                    case BlinkyCmdSetManualLeds when frame.Payload.Length >= 2:
                        return $"SetManualLeds: {DescribeLedMask(frame.Payload[1])}";
                }
            }
            else if (frame.ModuleId == ProtoModId.ElectronicLoad)
            {
                if (subCommand == LoadCmdSetCurrentLimitMa && frame.Payload.Length >= 3)
                    return $"SetCurrentLimitMa: {ReadUInt16LE(frame.Payload, 1)} mA";
            }

            return $"Command sub-code 0x{subCommand:X2} ({frame.Payload.Length} byte(s))";
        }

        private static string DescribeResponse(ProtocolFrame frame)
        {
            if (frame.ModuleId == ProtoModId.BlinkyLed && frame.Payload.Length >= 7)
            {
                bool enabled = frame.Payload[0] != 0;
                var mode = (BlinkyLedMode)frame.Payload[1];
                var pattern = (BlinkyLedPattern)frame.Payload[2];
                bool reverse = frame.Payload[3] != 0;
                int periodMs = ReadUInt16LE(frame.Payload, 4);
                byte mask = frame.Payload[6];
                string modeDetail = mode == BlinkyLedMode.Animated
                    ? $"Pattern={pattern}, Reverse={(reverse ? "Yes" : "No")}, Rate={periodMs}ms"
                    : $"LEDs={DescribeLedMask(mask)}";
                return $"State={(enabled ? "On" : "Off")}, Mode={mode}, {modeDetail}";
            }

            if (frame.ModuleId == ProtoModId.ElectronicLoad && frame.Payload.Length >= 3)
            {
                int commandedMa = ReadUInt16LE(frame.Payload, 0);
                byte dutyPercent = frame.Payload[2];
                return $"Commanded: {commandedMa} mA, Duty: {dutyPercent}%";
            }

            if (frame.ModuleId == ProtoModId.AccelTemp)
                return DescribeAccelTempPayload(frame);

            return $"Response ({frame.Payload.Length} byte(s))";
        }

        private static string DescribeStreamData(ProtocolFrame frame)
        {
            if (frame.ModuleId == ProtoModId.AccelTemp)
                return DescribeAccelTempPayload(frame);

            return $"StreamData ({frame.Payload.Length} byte(s))";
        }

        private static string DescribeAccelTempPayload(ProtocolFrame frame)
        {
            if (frame.Payload.Length < 7)
                return $"AccelTemp payload too short ({frame.Payload.Length} byte(s), expected 7)";

            sbyte tempC = unchecked((sbyte)frame.Payload[0]);
            double x = ReadInt16LE(frame.Payload, 1) / 1000.0;
            double y = ReadInt16LE(frame.Payload, 3) / 1000.0;
            double z = ReadInt16LE(frame.Payload, 5) / 1000.0;
            return $"Temp={tempC}°C, X={x:F2}g, Y={y:F2}g, Z={z:F2}g";
        }

        private static string DescribeLedMask(byte mask)
        {
            if (mask == 0) return "none";
            var sb = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append($"LED{i}");
            }
            return sb.ToString();
        }

        private static ushort ReadUInt16LE(byte[] data, int offset) =>
            (ushort)(data[offset] | (data[offset + 1] << 8));

        private static short ReadInt16LE(byte[] data, int offset) =>
            (short)(data[offset] | (data[offset + 1] << 8));
    }
}
