using System;
using System.IO;
using System.IO.Ports;
using ProtoVerseApp.Models;

namespace ProtoVerseApp.Services
{
    /// <summary>
    /// Owns the physical serial connection to ProtoCore. This is the ONLY class that
    /// touches SerialPort directly - everything else (panels, view models) goes
    /// through FrameDispatcher instead, so there's never a risk of two things writing
    /// to the port at once.
    /// </summary>
    public class SerialService : ISerialService
    {
        private SerialPort? _port;
        private readonly ProtocolFrameReader _reader = new();

        public bool IsConnected => _port?.IsOpen ?? false;

        /// <summary>Raised whenever a complete, valid frame is received from ProtoCore.</summary>
        public event Action<ProtocolFrame>? FrameReceived;

        /// <summary>Raised on a framing/checksum error - forwarded from the reader for
        /// diagnostics/UI display.</summary>
        public event Action<string>? FrameError;

        /// <summary>Raised when the port disappears out from under us (cable pulled,
        /// device reset) rather than via an explicit Disconnect() call.</summary>
        public event Action<string>? Disconnected;

        public SerialService()
        {
            _reader.FrameParsed += frame => FrameReceived?.Invoke(frame);
            _reader.FrameError += msg => FrameError?.Invoke(msg);
        }

        /// <summary>Lists COM ports currently available on the system, for populating
        /// the connection dropdown in the UI.</summary>
        public static string[] GetAvailablePorts() => SerialPort.GetPortNames();

        public void Connect(string portName, int baudRate = 115200)
        {
            Disconnect();

            var port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);

            // Bounded, not SerialPort's default of infinite. Without this, a Read/Write
            // against a handle that's gone but hasn't been torn down cleanly by the OS
            // yet (observed for real: a ProtoMod re-insertion browning out ProtoCore's
            // supply and forcing a full MCU reset, not just a clean cable-pull) can block
            // the calling thread forever instead of throwing - and Send() runs
            // synchronously on the UI thread via FrameDispatcher, so that hang freezes
            // the whole app rather than just failing one command. 1s is generous for a
            // real frame (max 250 bytes at 115200 baud takes low tens of ms) while still
            // recovering promptly from a genuinely dead port.
            port.ReadTimeout = 1000;
            port.WriteTimeout = 1000;

            try
            {
                port.DataReceived += OnDataReceived;
                port.Open();
            }
            catch
            {
                // Open() can genuinely fail - a busy port, a flaky USB CDC driver, the
                // board resetting mid-open, etc. Don't leave a half-opened port sitting
                // in _port for a later call to trip over; let the caller decide how to
                // report this (MainViewModel turns it into a status message rather than
                // an unhandled-exception dialog).
                port.DataReceived -= OnDataReceived;
                port.Dispose();
                throw;
            }

            _port = port;
        }

        public void Disconnect()
        {
            if (_port == null) return;

            _port.DataReceived -= OnDataReceived;
            try { if (_port.IsOpen) _port.Close(); } catch (IOException) { /* already gone */ }
            _port.Dispose();
            _port = null;
        }

        /// <summary>Sends a frame as-is. Called only by FrameDispatcher.</summary>
        public void Send(ProtocolFrame frame)
        {
            if (_port == null || !_port.IsOpen)
                throw new InvalidOperationException("Serial port is not open.");

            var bytes = frame.Encode();
            try
            {
                _port.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex) when (IsDisconnectException(ex))
            {
                HandleUnexpectedDisconnect(ex.Message);
                throw;
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_port == null) return;

            try
            {
                int bytesToRead = _port.BytesToRead;
                if (bytesToRead == 0) return;

                var buffer = new byte[bytesToRead];
                int read = _port.Read(buffer, 0, bytesToRead);

                // Note: this callback fires on a background thread owned by SerialPort.
                // FrameDispatcher is responsible for marshalling FrameReceived back to the
                // UI thread before touching any bound view model properties.
                _reader.Feed(buffer, read);
            }
            catch (Exception ex) when (IsDisconnectException(ex))
            {
                HandleUnexpectedDisconnect(ex.Message);
            }
        }

        private static bool IsDisconnectException(Exception ex) =>
            ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException;

        /// <summary>Tears the port down the same way Disconnect() does, then tells
        /// upstream listeners this wasn't a deliberate disconnect.</summary>
        private void HandleUnexpectedDisconnect(string reason)
        {
            if (_port == null) return; // already handled by a concurrent caller

            Disconnect();
            Disconnected?.Invoke(reason);
        }

        public void Dispose() => Disconnect();
    }
}
