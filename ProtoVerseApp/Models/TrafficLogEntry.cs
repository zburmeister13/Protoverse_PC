using System;

namespace ProtoVerseApp.Models
{
    public enum TrafficDirection { Sent, Received, Error }

    /// <summary>
    /// One row in the raw traffic log: either a frame that was sent/received, or a
    /// framing/checksum error reported by the reader. Kept as plain formatted strings
    /// (rather than holding onto the ProtocolFrame) since this exists purely for
    /// display in the debug panel.
    /// </summary>
    public class TrafficLogEntry
    {
        public DateTime Timestamp { get; }
        public TrafficDirection Direction { get; }

        public string DirectionLabel => Direction switch
        {
            TrafficDirection.Sent => "TX",
            TrafficDirection.Received => "RX",
            _ => "ERR"
        };

        public string? Module { get; }
        public string? MsgType { get; }
        /// <summary>The wire frame's Length byte (payload byte count) as decimal -
        /// one of the "blocks" in RawHex that isn't otherwise broken out into its
        /// own column.</summary>
        public string LengthLabel { get; }
        public string PayloadHex { get; }
        /// <summary>The wire frame's Checksum byte, the other RawHex "block" not
        /// otherwise broken out - an XOR of every other framed byte, see
        /// ProtocolFrame's doc comment for the exact formula.</summary>
        public string ChecksumHex { get; }
        public string RawHex { get; }
        /// <summary>Human-readable decode of what this frame actually means (e.g.
        /// "SetCurrentLimitMa: 100 mA") - see FrameInterpreter. Null only for the
        /// framing/checksum-error constructor below, where errorMessage fills this
        /// role instead.</summary>
        public string? Info { get; }

        public TrafficLogEntry(TrafficDirection direction, ProtocolFrame frame)
        {
            Timestamp = DateTime.Now;
            Direction = direction;
            Module = frame.ModuleId.ToString();
            MsgType = frame.Type.ToString();
            LengthLabel = frame.Payload.Length.ToString();
            PayloadHex = frame.Payload.Length == 0 ? "-" : BitConverter.ToString(frame.Payload).Replace('-', ' ');

            var encoded = frame.Encode();
            ChecksumHex = encoded[encoded.Length - 2].ToString("X2");
            RawHex = BitConverter.ToString(encoded).Replace('-', ' ');

            Info = FrameInterpreter.Describe(frame);
        }

        public TrafficLogEntry(string errorMessage)
        {
            Timestamp = DateTime.Now;
            Direction = TrafficDirection.Error;
            LengthLabel = "-";
            PayloadHex = "-";
            ChecksumHex = "-";
            RawHex = "-";
            Info = errorMessage;
        }
    }
}
