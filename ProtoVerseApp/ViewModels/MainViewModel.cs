using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Top-level view model for MainWindow. Owns the serial connection lifecycle and
    /// the collection of slot view models shown in the stacked layout.
    ///
    /// ProtoCore has a fixed number of physical ProtoMod slots (three), but which
    /// module type - if any - occupies each one varies, and there are far more
    /// possible ProtoMod types than any given app build ships panels for. So this
    /// class never assumes a specific lineup: all three slots start (and, on
    /// disconnect, revert to) EmptySlotViewModel, and only get populated with real
    /// panel view models - built by ModuleCatalog - once a PresenceReport says what's
    /// actually plugged in. A recognized-by-firmware-but-unsupported-by-this-app type
    /// becomes an UnknownModuleViewModel instead of crashing or vanishing.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private const int SlotCount = 3;

        private ISerialService _serial;
        private readonly FrameDispatcher _dispatcher;

        public ObservableCollection<string> AvailablePorts { get; } = new();
        public ObservableCollection<object> Panels { get; } = new();
        public TrafficLogViewModel TrafficLog { get; }
        public HelpViewModel Help { get; } = new();

        [ObservableProperty]
        private string? _selectedPort;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private string _statusMessage = "Not connected";

        [ObservableProperty]
        private bool _simulatorMode;

        /// <summary>Whether the real-port dropdown/refresh should be usable - disabled
        /// while simulator mode is on, since there's no real port to pick.</summary>
        public bool CanSelectRealPort => !SimulatorMode;

        public MainViewModel()
        {
            _serial = new SerialService();
            _dispatcher = new FrameDispatcher(_serial);
            _dispatcher.FrameReceived += OnFrameReceived;
            _dispatcher.FrameError += msg => StatusMessage = $"Protocol error: {msg}";
            _dispatcher.Disconnected += OnTransportDisconnected;

            TrafficLog = new TrafficLogViewModel(_dispatcher);

            ResetSlotsToEmpty();

            RefreshPorts();
        }

        partial void OnSimulatorModeChanged(bool value)
        {
            OnPropertyChanged(nameof(CanSelectRealPort));

            if (IsConnected)
                Disconnect();

            _serial = value ? new MockSerialService() : new SerialService();
            _dispatcher.SetTransport(_serial);
            ConnectCommand.NotifyCanExecuteChanged();
            StatusMessage = value ? "Simulator mode enabled - no hardware required" : "Simulator mode disabled";
        }

        [RelayCommand]
        private void RefreshPorts()
        {
            AvailablePorts.Clear();
            foreach (var port in SerialService.GetAvailablePorts())
                AvailablePorts.Add(port);
        }

        [RelayCommand(CanExecute = nameof(CanConnect))]
        private void Connect()
        {
            if (!SimulatorMode && string.IsNullOrEmpty(SelectedPort))
                return;

            var portName = SimulatorMode ? "SIMULATOR" : SelectedPort!;

            try
            {
                _dispatcher.Connect(portName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A real, expected failure mode - e.g. the port's underlying USB device
                // is in a broken state (a flaky USB CDC driver, the board resetting
                // mid-open), or another program already has the port open. Not a bug -
                // show it as a normal failed-connect status instead of letting it
                // become an unhandled-exception dialog for something this routine.
                IsConnected = false;
                StatusMessage = $"Failed to connect to {(SimulatorMode ? "Simulator" : SelectedPort)}: {ex.Message}";
                return;
            }

            IsConnected = _dispatcher.IsConnected;

            if (IsConnected)
            {
                // Auto-request presence on connect so slots populate without an extra
                // manual step - telemetry (Accel+Temp/Electronic Load) starts streaming
                // immediately on connect regardless, which made it look like modules
                // were "detected" in the Traffic Log while the slots stayed empty
                // because nothing had asked Core who's actually plugged in yet.
                // "Identify slots" stays available for a manual re-query (e.g. after a
                // hot-swap).
                StatusMessage = $"Connected to {(SimulatorMode ? "Simulator" : SelectedPort)} - requesting installed ProtoMods...";
                _dispatcher.RequestPresence();
            }
            else
            {
                StatusMessage = "Failed to connect";
            }
        }

        private bool CanConnect() => !IsConnected && (SimulatorMode || !string.IsNullOrEmpty(SelectedPort));

        partial void OnSelectedPortChanged(string? value) => ConnectCommand.NotifyCanExecuteChanged();
        partial void OnIsConnectedChanged(bool value) => ConnectCommand.NotifyCanExecuteChanged();

        [RelayCommand]
        private void Disconnect()
        {
            _dispatcher.Disconnect();
            SetDisconnectedState("Not connected");
        }

        /// <summary>The transport dropped on its own (cable pulled, device reset) -
        /// it has already torn itself down by this point, so this only needs to catch
        /// the UI up: stop claiming we're connected and stop showing stale slots.</summary>
        private void OnTransportDisconnected(string reason) =>
            SetDisconnectedState($"Device disconnected unexpectedly: {reason}");

        private void SetDisconnectedState(string statusMessage)
        {
            IsConnected = false;
            StatusMessage = statusMessage;
            ResetSlotsToEmpty();
        }

        [RelayCommand]
        private void IdentifySlots()
        {
            if (!IsConnected)
                return;

            _dispatcher.RequestPresence();
            StatusMessage = "Requesting installed ProtoMods...";
        }

        private void OnFrameReceived(ProtocolFrame frame)
        {
            if (frame.ModuleId != ProtoModId.Core || frame.Type != MsgType.PresenceReport)
                return;

            // Convention (as of 2026-08-30): payload is a FIXED SlotCount*2-byte array
            // - exactly one ProtoModId per physical slot, 2 bytes little-endian each,
            // always in slot order (payload[0..1] = slot 0, [2..3] = slot 1, ...). An
            // empty slot reports ProtoModId.None rather than being omitted. This
            // replaces an earlier skip-empty-slots format that couldn't distinguish
            // "BlinkyLed in slot 0" from "BlinkyLed in slot 1, slots 0/2 empty" - both
            // produced the same single-entry payload, so a module in any slot but the
            // first always rendered in this app's first panel regardless of which
            // physical slot it was actually in. Agreed and fixed cross-session with
            // the firmware session after the user hit exactly that mismatch.
            if (frame.Payload.Length != SlotCount * 2)
            {
                StatusMessage = $"Malformed PresenceReport: expected {SlotCount * 2} payload bytes, got {frame.Payload.Length}";
                return;
            }

            // A hot-swap on the real board fires this handler at any moment, so it
            // must never be able to take the whole app down or leave the UI in a
            // half-rebuilt state. Two layers of isolation:
            //  1. Build the new slot lineup in a local list first, and only replace
            //     the live Panels collection once the whole new lineup is ready - if
            //     something below throws in a way that isn't already caught per-slot,
            //     the currently-displayed (working) panels are never touched.
            //  2. Construct each slot's panel inside its own try/catch, so one
            //     misbehaving ProtoMod type (e.g. a bug in a newly-added panel's
            //     constructor) degrades just that slot to "Unsupported" instead of
            //     losing the whole report - the other slots still update normally.
            var newPanels = new List<object>(SlotCount);
            var slotErrors = new List<string>();
            int detectedCount = 0;

            for (int slot = 0; slot < SlotCount; slot++)
            {
                var moduleId = (ProtoModId)(ushort)(frame.Payload[slot * 2] | (frame.Payload[slot * 2 + 1] << 8));
                if (moduleId == ProtoModId.None)
                {
                    newPanels.Add(new EmptySlotViewModel());
                    continue;
                }

                detectedCount++;
                try
                {
                    newPanels.Add(ModuleCatalog.TryCreate(moduleId, _dispatcher) ?? (object)new UnknownModuleViewModel(moduleId));
                }
                catch (Exception ex)
                {
                    newPanels.Add(new UnknownModuleViewModel(moduleId));
                    slotErrors.Add($"slot {slot} ({moduleId}): {ex.GetType().Name}");
                }
            }

            DetachModulePanels();
            Panels.Clear();
            foreach (var panel in newPanels)
                Panels.Add(panel);

            StatusMessage = slotErrors.Count == 0
                ? $"{detectedCount} ProtoMod(s) detected"
                : $"{detectedCount} ProtoMod(s) detected - panel error in {string.Join(", ", slotErrors)}";
        }

        /// <summary>Unsubscribes every currently-displayed module panel from the
        /// dispatcher before it's discarded, so a slot that's about to be reassigned
        /// (or emptied) doesn't leave a dead view model still processing frames.
        /// Best-effort per panel - one panel's Detach() misbehaving (e.g. a future
        /// panel type with a buggy cleanup path) must not stop the rest from being
        /// detached or block a hot-swap rebuild from completing.</summary>
        private void DetachModulePanels()
        {
            foreach (var module in Panels.OfType<ModulePanelViewModelBase>())
            {
                try { module.Detach(); }
                catch { /* best-effort cleanup, see doc comment above */ }
            }
        }

        private void ResetSlotsToEmpty()
        {
            DetachModulePanels();
            Panels.Clear();
            for (int i = 0; i < SlotCount; i++)
                Panels.Add(new EmptySlotViewModel());
        }
    }
}
