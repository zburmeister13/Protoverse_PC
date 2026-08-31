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

        /// <summary>ProtoCore's physical slots, in slot order - always exactly
        /// SlotCount of them, occupied or not. Replaced the old flat
        /// `ObservableCollection&lt;object&gt; Panels` when the layout became a
        /// navigator plus detail pane: the navigator needs to show empty slots as rows
        /// rather than as stacked "nothing here" cards, and needs a slot number to show
        /// against each. The panel view models inside are unchanged and are still built
        /// by the same ModuleCatalog call.</summary>
        public ObservableCollection<SlotViewModel> Slots { get; } = new();

        /// <summary>Which slot's workspace (live panel + manual) fills the detail pane.
        /// Never null once the constructor has run - selection falls back to slot 0
        /// rather than showing an empty right-hand side, which reads as broken.</summary>
        [ObservableProperty]
        private SlotViewModel? _selectedSlot;
        public TrafficLogViewModel TrafficLog { get; }
        public HelpViewModel Help { get; } = new();

        /// <summary>Local profiles, so two people sharing a PC track separate kits.
        /// Not a security boundary - see <see cref="AccountStore"/>. Shared between the
        /// header's sign-in control and the Library, so both see the same active
        /// account.</summary>
        private readonly AccountStore _accounts = new();

        /// <summary>Backs the sign-in control in the window's top-right corner.</summary>
        public AccountViewModel Account { get; }

        /// <summary>The Library tab's full ProtoMod catalog - everything that exists,
        /// not just what's plugged in. Read-only content; this class's only interaction
        /// with it is telling it which module types the slots currently hold, so the
        /// user's kit can be tracked against what's actually detected. Assigned in the
        /// constructor before ResetSlotsToEmpty() (which clears its live marks) runs.</summary>
        public LibraryViewModel Library { get; }

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
            Account = new AccountViewModel(_accounts);
            Library = new LibraryViewModel(_accounts);

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
            var newPanels = new List<SlotViewModel>(SlotCount);
            var slotErrors = new List<string>();
            var detectedIds = new List<ProtoModId>();
            int detectedCount = 0;

            for (int slot = 0; slot < SlotCount; slot++)
            {
                var moduleId = (ProtoModId)(ushort)(frame.Payload[slot * 2] | (frame.Payload[slot * 2 + 1] << 8));
                if (moduleId == ProtoModId.None)
                {
                    newPanels.Add(SlotViewModel.Empty(slot));
                    continue;
                }

                detectedCount++;
                // Every non-empty slot counts as "in your kit" for the Library tab,
                // including a type this build has no panel for (F02/BasicLed today) -
                // owning a board and this app being able to control it are different
                // things, and the Library is about the former. ProtoModId.Unknown ends
                // up in here too, harmlessly: no catalog entry claims that ID, so it
                // matches nothing.
                detectedIds.Add(moduleId);
                try
                {
                    var panel = ModuleCatalog.TryCreate(moduleId, _dispatcher);
                    newPanels.Add(panel != null
                        ? new SlotViewModel(slot, panel, moduleId, SlotState.Occupied, panel.DisplayName)
                        : BuildUnsupportedSlot(slot, moduleId));
                }
                catch (Exception ex)
                {
                    newPanels.Add(BuildUnsupportedSlot(slot, moduleId));
                    slotErrors.Add($"slot {slot} ({moduleId}): {ex.GetType().Name}");
                }
            }

            DetachModulePanels();
            Slots.Clear();
            foreach (var panel in newPanels)
                Slots.Add(panel);

            RestoreSelection();
            Library.UpdateInstalled(detectedIds);

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
            foreach (var module in Slots.Select(s => s.Content).OfType<ModulePanelViewModelBase>())
            {
                try { module.Detach(); }
                catch { /* best-effort cleanup, see doc comment above */ }
            }
        }

        /// <summary>Wraps a present-but-unsupported module. Uses the same
        /// UnknownModuleViewModel as before; only the navigator label is new, and it's
        /// deliberately short - the full explanation stays in the detail pane, since a
        /// 200px navigator row can't carry "ProtoCore doesn't recognize its EEPROM
        /// identity".</summary>
        private static SlotViewModel BuildUnsupportedSlot(int slot, ProtoModId moduleId)
        {
            var circuitCode = ProtoModBoardCatalog.Entries.FirstOrDefault(e => e.Id == moduleId)?.CircuitCode;
            var label = moduleId == ProtoModId.Unknown
                ? "Unrecognized board"
                : circuitCode ?? $"0x{(ushort)moduleId:X4}";

            return new SlotViewModel(slot, new UnknownModuleViewModel(moduleId), moduleId, SlotState.Unsupported, label);
        }

        /// <summary>Keeps the detail pane pointed somewhere sensible after a hot-swap:
        /// the same physical slot if it's still worth showing, otherwise the first
        /// occupied one, otherwise slot 1. Without this, swapping a module while its
        /// workspace is open leaves the right-hand side bound to a discarded view
        /// model.</summary>
        private void RestoreSelection()
        {
            int preferred = SelectedSlot?.Index ?? 0;

            SelectedSlot =
                Slots.FirstOrDefault(s => s.Index == preferred && s.IsOccupied)
                ?? Slots.FirstOrDefault(s => s.IsOccupied)
                ?? Slots.FirstOrDefault();
        }

        private void ResetSlotsToEmpty()
        {
            DetachModulePanels();
            Slots.Clear();
            for (int i = 0; i < SlotCount; i++)
                Slots.Add(SlotViewModel.Empty(i));

            RestoreSelection();

            // Disconnected means the app no longer knows what's installed - which is a
            // different claim from "nothing is installed", so the Library drops back to
            // "connect to see which of these are in your kit" rather than marking every
            // module not-owned.
            Library.ClearInstalled();
        }
    }
}
