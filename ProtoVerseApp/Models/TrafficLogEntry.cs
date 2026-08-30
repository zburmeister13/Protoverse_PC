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
        public string PayloadHex { get; }
        public string RawHex { get; }
        public string? Info { get; }

        public TrafficLogEntry(TrafficDirection direction, ProtocolFrame frame)
        {
            Timestamp = DateTime.Now;
            Direction = direction;
            Module = frame.ModuleId.ToString();
            MsgType = frame.Type.ToString();
            PayloadHex = frame.Payload.Length == 0 ? "-" : BitConverter.ToString(frame.Payload).Replace('-', ' ');
            RawHex = BitConverter.ToString(frame.Encode()).Replace('-', ' ');
        }

        public TrafficLogEntry(string errorMessage)
        {
            Timestamp = DateTime.Now;
            Direction = TrafficDirection.Error;
            PayloadHex = "-";
            RawHex = "-";
            Info = errorMessage;
        }
    }
}
