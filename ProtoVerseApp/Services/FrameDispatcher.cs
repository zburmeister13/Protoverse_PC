using System;
using System.IO;
using System.Windows.Threading;
using ProtoVerseApp.Models;

namespace ProtoVerseApp.Services
{
    /// <summary>
    /// Sits between the panels/view models and the underlying transport. Panels never
    /// touch ISerialService directly - they call Dispatcher.Send(...) and subscribe to
    /// Dispatcher.FrameReceived, filtering for their own ProtoModId.
    ///
    /// This is also where UI-thread marshalling happens: ISerialService implementations
    /// raise FrameReceived on a background thread, and WPF bound properties must only be
    /// touched from the UI thread, so this class re-posts the event through the
    /// Dispatcher (WPF's, not to be confused with this class's own name - unfortunate
    /// naming collision, kept distinct via the fully-qualified type below).
    /// </summary>
    public class FrameDispatcher
    {
        private ISerialService _serial;
        private readonly System.Windows.Threading.Dispatcher _uiDispatcher;

        public event Action<ProtocolFrame>? FrameReceived;
        public event Action<ProtocolFrame>? FrameSent;
        public event Action<string>? FrameError;

        /// <summary>Raised when the transport drops unexpectedly (cable pulled, device
        /// reset) rather than via an explicit Disconnect() call.</summary>
        public event Action<string>? Disconnected;

        public bool IsConnected => _serial.IsConnected;

        public FrameDispatcher(ISerialService serial)
        {
            _serial = serial;
            _uiDispatcher = System.Windows.Application.Current.Dispatcher;
            Subscribe(_serial);
        }

        /// <summary>Swaps the underlying transport (e.g. real serial &lt;-&gt; mock/
        /// simulator) without disturbing anything upstream - panels stay subscribed to
        /// this dispatcher the whole time and never know the swap happened.</summary>
        public void SetTransport(ISerialService serial)
        {
            _serial.Disconnect();
            Unsubscribe(_serial);
            _serial.Dispose();

            _serial = serial;
            Subscribe(_serial);
        }

        private void Subscribe(ISerialService serial)
        {
            serial.FrameReceived += OnSerialFrameReceived;
            serial.FrameError += OnSerialFrameError;
            serial.Disconnected += OnSerialDisconnected;
        }

        private void Unsubscribe(ISerialService serial)
        {
            serial.FrameReceived -= OnSerialFrameReceived;
            serial.FrameError -= OnSerialFrameError;
            serial.Disconnected -= OnSerialDisconnected;
        }

        public void Connect(string portName, int baudRate = 115200) => _serial.Connect(portName, baudRate);
        public void Disconnect() => _serial.Disconnect();

        /// <summary>Sends a command to a specific ProtoMod.</summary>
        public void Send(ProtoModId moduleId, MsgType type, byte[]? payload = null)
        {
            var frame = new ProtocolFrame(moduleId, type, payload);

            try
            {
                _serial.Send(frame);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
            {
                // If this was an unexpected drop, the transport already raised
                // Disconnected to tell the UI - swallow here so a button click can't
                // crash the UI thread on a pulled cable. TimeoutException included
                // alongside the others since SerialService now bounds its read/write
                // timeouts instead of blocking forever on a dead handle - a timeout is
                // just as much "the transport is gone" as an IOException is.
                return;
            }

            FrameSent?.Invoke(frame);
        }

        /// <summary>Convenience for the "Identify slots" button: asks ProtoCore which
        /// ProtoMod IDs are currently present.</summary>
        public void RequestPresence() => Send(ProtoModId.Core, MsgType.PresenceRequest);

        private void OnSerialFrameReceived(ProtocolFrame frame) =>
            _uiDispatcher.BeginInvoke(() => FrameReceived?.Invoke(frame));

        private void OnSerialFrameError(string message) =>
            _uiDispatcher.BeginInvoke(() => FrameError?.Invoke(message));

        private void OnSerialDisconnected(string reason) =>
            _uiDispatcher.BeginInvoke(() => Disconnected?.Invoke(reason));
    }
}
