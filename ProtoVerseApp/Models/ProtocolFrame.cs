using System;
using System.Collections.Generic;

namespace ProtoVerseApp.Models
{
    /// <summary>
    /// One frame on the wire between the PC app and ProtoCore.
    ///
    /// Layout:
    ///   [STX 0x02] [ProtoModId] [MsgType] [Length] [Payload x Length] [Checksum] [ETX 0x03]
    ///
    /// Checksum = XOR of ProtoModId, MsgType, Length, and every payload byte.
    ///
    /// Note: payload bytes are NOT escaped/byte-stuffed in this first version, so a
    /// payload that happens to contain 0x02 or 0x03 will not desync framing on its
    /// own (the reader trusts the Length field once STX is found) - but if bytes are
    /// ever dropped mid-payload, resync relies on finding the next STX. If that turns
    /// out to be a problem in practice, add SLIP-style escaping later; not needed yet.
    /// </summary>
    public class ProtocolFrame
    {
        public const byte Stx = 0x02;
        public const byte Etx = 0x03;

        /// <summary>Max payload size. Keeps frames small and reader buffers simple.
        /// Anything larger (e.g. a full sweep capture) should be sent as multiple
        /// StreamData frames rather than growing this.</summary>
        public const int MaxPayloadLength = 250;

        public ProtoModId ModuleId { get; }
        public MsgType Type { get; }
        public byte[] Payload { get; }

        public ProtocolFrame(ProtoModId moduleId, MsgType type, byte[]? payload = null)
        {
            payload ??= Array.Empty<byte>();
            if (payload.Length > MaxPayloadLength)
                throw new ArgumentOutOfRangeException(nameof(payload),
                    $"Payload length {payload.Length} exceeds max {MaxPayloadLength}");

            ModuleId = moduleId;
            Type = type;
            Payload = payload;
        }

        /// <summary>Serializes this frame to the exact bytes to send on the wire.</summary>
        public byte[] Encode()
        {
            var buffer = new byte[6 + Payload.Length];
            buffer[0] = Stx;
            buffer[1] = (byte)ModuleId;
            buffer[2] = (byte)Type;
            buffer[3] = (byte)Payload.Length;
            Array.Copy(Payload, 0, buffer, 4, Payload.Length);
            buffer[4 + Payload.Length] = ComputeChecksum((byte)ModuleId, (byte)Type, (byte)Payload.Length, Payload);
            buffer[5 + Payload.Length] = Etx;
            return buffer;
        }

        private static byte ComputeChecksum(byte moduleId, byte msgType, byte length, byte[] payload)
        {
            byte checksum = (byte)(moduleId ^ msgType ^ length);
            foreach (var b in payload)
                checksum ^= b;
            return checksum;
        }

        public override string ToString() =>
            $"[{ModuleId} / {Type}] {Payload.Length} byte(s): {BitConverter.ToString(Payload)}";
    }

    /// <summary>
    /// Stateful, incremental frame reader. Feed it raw bytes as they arrive from the
    /// serial port (which may split a frame across multiple reads, or bundle several
    /// frames into one read) and it raises <see cref="FrameParsed"/> for each
    /// complete, checksum-valid frame it finds.
    /// </summary>
    public class ProtocolFrameReader
    {
        private enum State { WaitingForStx, ReadingHeader, ReadingPayload, ReadingChecksum, ReadingEtx }

        private State _state = State.WaitingForStx;
        private readonly List<byte> _headerBuffer = new(3);
        private readonly List<byte> _payloadBuffer = new();
        private byte _moduleId;
        private byte _msgType;
        private byte _length;
        private byte _checksum;

        public event Action<ProtocolFrame>? FrameParsed;

        /// <summary>Raised when bytes are discarded due to a framing/checksum error,
        /// with a short human-readable reason - useful for a "protocol errors" counter
        /// in the UI while bringing up the link.</summary>
        public event Action<string>? FrameError;

        public void Feed(byte[] data, int count)
        {
            for (int i = 0; i < count; i++)
                FeedByte(data[i]);
        }

        private void FeedByte(byte b)
        {
            switch (_state)
            {
                case State.WaitingForStx:
                    if (b == ProtocolFrame.Stx)
                    {
                        _headerBuffer.Clear();
                        _state = State.ReadingHeader;
                    }
                    break;

                case State.ReadingHeader:
                    _headerBuffer.Add(b);
                    if (_headerBuffer.Count == 3)
                    {
                        _moduleId = _headerBuffer[0];
                        _msgType = _headerBuffer[1];
                        _length = _headerBuffer[2];
                        _payloadBuffer.Clear();
                        _state = _length == 0 ? State.ReadingChecksum : State.ReadingPayload;
                    }
                    break;

                case State.ReadingPayload:
                    _payloadBuffer.Add(b);
                    if (_payloadBuffer.Count == _length)
                        _state = State.ReadingChecksum;
                    break;

                case State.ReadingChecksum:
                    _checksum = b;
                    _state = State.ReadingEtx;
                    break;

                case State.ReadingEtx:
                    if (b != ProtocolFrame.Etx)
                    {
                        FrameError?.Invoke("Missing ETX - discarding frame");
                        _state = State.WaitingForStx;
                        break;
                    }

                    byte expected = (byte)(_moduleId ^ _msgType ^ _length);
                    foreach (var pb in _payloadBuffer)
                        expected ^= pb;

                    if (expected != _checksum)
                    {
                        FrameError?.Invoke("Checksum mismatch - discarding frame");
                    }
                    else
                    {
                        var frame = new ProtocolFrame((ProtoModId)_moduleId, (MsgType)_msgType, _payloadBuffer.ToArray());
                        FrameParsed?.Invoke(frame);
                    }
                    _state = State.WaitingForStx;
                    break;
            }
        }
    }
}
