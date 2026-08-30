using System;
using ProtoVerseApp.Models;

namespace ProtoVerseApp.Services
{
    /// <summary>
    /// Abstraction over "something that sends/receives ProtocolFrames." Lets
    /// FrameDispatcher point at either the real serial port (SerialService) or
    /// MockSerialService without either the dispatcher or any panel view model
    /// knowing which one is behind it.
    /// </summary>
    public interface ISerialService : IDisposable
    {
        bool IsConnected { get; }

        event Action<ProtocolFrame>? FrameReceived;
        event Action<string>? FrameError;

        /// <summary>Raised when the transport drops unexpectedly (cable pulled, device
        /// reset, port vanished) rather than via an explicit Disconnect() call. The
        /// transport is already fully torn down by the time this fires - callers just
        /// need to update UI state, not clean anything up.</summary>
        event Action<string>? Disconnected;

        void Connect(string portName, int baudRate = 115200);
        void Disconnect();
        void Send(ProtocolFrame frame);
    }
}
