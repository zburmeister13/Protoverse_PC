using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// The Rails tab: discrete on/off control of ProtoCore's six switchable supply
    /// rails.
    ///
    /// DEVICE-ECHO ONLY, same rule as every module panel. Nothing here flips because
    /// the user clicked - a click sends a command, and the rails redraw from the
    /// snapshot firmware sends back. That matters more here than usual, because
    /// firmware auto-enables parent rails on the way up: clicking "1.0 V on" can
    /// legitimately turn three rails on, and only the device's reply knows which.
    /// This is also why the controls are buttons rather than checkboxes - a checkbox
    /// visibly flips itself on click, which would be a local guess.
    ///
    /// COMMANDED, NOT MEASURED. PGOOD is not routed to the MCU on this hardware
    /// revision, so firmware reports the bit it last wrote and cannot know whether a
    /// rail actually came up. The UI says so rather than implying a readback - see
    /// <see cref="VoltageRailCatalog"/>.
    ///
    /// FIRMWARE IS THE AUTHORITY ON INTERLOCKS. This class mirrors the supply tree
    /// only to explain what a click will do before it happens, and to report a
    /// refusal afterwards. It never blocks a command on its own model: if the two
    /// ever disagree, the device wins and says so, rather than the app silently
    /// forbidding something that would have worked.
    /// </summary>
    public partial class RailsViewModel : ObservableObject
    {
        private readonly FrameDispatcher _dispatcher;

        public ObservableCollection<RailRowViewModel> Rails { get; } = new();

        /// <summary>Null until we've heard one way or the other. False once firmware
        /// has told us it doesn't know what Core is - see
        /// <see cref="VoltageRailCatalog.ErrNotPresent"/>.</summary>
        [ObservableProperty]
        private bool? _isSupported;

        [ObservableProperty]
        private bool _isConnected;

        /// <summary>Last interlock refusal, or any other error the device sent back.
        /// Cleared on the next successful snapshot so a stale complaint doesn't sit
        /// under a state it no longer describes.</summary>
        [ObservableProperty]
        private string? _lastError;

        public bool HasError => LastError != null;

        /// <summary>Shown instead of the rail list when firmware predates rail
        /// control. Deliberately specific: "your firmware is too old" and "the link
        /// is dead" look identical from a UI that only knows nothing happened.</summary>
        public bool ShowUnsupportedNotice => IsConnected && IsSupported == false;

        public bool ShowRails => IsConnected && IsSupported != false;

        public RailsViewModel(FrameDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _dispatcher.FrameReceived += OnFrameReceived;

            foreach (var rail in VoltageRailCatalog.Rails)
                Rails.Add(new RailRowViewModel(rail, this));

            RefreshNotes();
        }

        partial void OnIsSupportedChanged(bool? value)
        {
            OnPropertyChanged(nameof(ShowUnsupportedNotice));
            OnPropertyChanged(nameof(ShowRails));
        }

        partial void OnIsConnectedChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowUnsupportedNotice));
            OnPropertyChanged(nameof(ShowRails));
        }

        partial void OnLastErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

        /// <summary>Called by MainViewModel once a connection is up. Rail state is
        /// never assumed - the tab asks rather than guessing everything is on.</summary>
        public void OnConnected()
        {
            IsConnected = true;
            IsSupported = null;
            LastError = null;
            RequestRails();
        }

        public void OnDisconnected()
        {
            IsConnected = false;
            _pending = null;
            IsSupported = null;
            LastError = null;
            foreach (var row in Rails)
                row.SetState(false);
            RefreshNotes();
        }

        [RelayCommand]
        private void RequestRails() =>
            _dispatcher.Send(ProtoModId.Core, MsgType.Command, new[] { VoltageRailCatalog.CmdGetRails });

        /// <summary>What the last SetRail asked for, so the reply can be checked
        /// against it. Firmware has no wire error for an I2C write that fails at the
        /// expander (a bus fault rather than a validation problem) - it replies with
        /// the true, unchanged state instead. That is the honest thing for it to do,
        /// but it means a failed write is indistinguishable from nothing happening
        /// unless somebody compares the reply to the request. This is that
        /// comparison.</summary>
        private (RailId Id, bool Desired)? _pending;

        internal void SetRail(RailId id, bool on)
        {
            LastError = null;
            _pending = (id, on);
            _dispatcher.Send(ProtoModId.Core, MsgType.Command,
                new[] { VoltageRailCatalog.CmdSetRail, (byte)id, (byte)(on ? 1 : 0) });
        }

        private void OnFrameReceived(ProtocolFrame frame)
        {
            if (frame.ModuleId != ProtoModId.Core)
                return;

            if (frame.Type == MsgType.Error)
            {
                HandleError(frame);
                return;
            }

            if (frame.Type != MsgType.Response)
                return; // PresenceReport is MainViewModel's business, not ours

            // [rail_count, one byte per rail]. A fixed array with an explicit count
            // rather than a packed mask, for the same reason PresenceReport is one:
            // a packed encoding once let two different physical situations produce
            // identical bytes here, and that bug cost real debugging time.
            if (frame.Payload.Length < 1)
                return;

            int count = frame.Payload[0];
            if (count == 0 || frame.Payload.Length < count + 1)
            {
                LastError = $"Malformed rail report: says {count} rail(s) but carries {frame.Payload.Length - 1} state byte(s).";
                return;
            }

            IsSupported = true;

            // NOT cleared here. A refusal is immediately followed by a re-sync, and
            // clearing on the snapshot wiped the explanation before it could be read -
            // the rail correctly refused to move and the UI said nothing about why.
            // LastError is cleared when the user next acts (SetRail) or reconnects,
            // so it always describes the most recent thing that happened.

            for (int i = 0; i < count; i++)
            {
                // A firmware build reporting more rails than this app knows about is
                // expected eventually - ignore the extras rather than failing, the
                // same way an unrecognized ProtoModId degrades instead of crashing.
                var row = Rails.FirstOrDefault(r => (byte)r.Id == i);
                row?.SetState(frame.Payload[i + 1] != 0);
            }

            CheckPendingWasApplied();
            RefreshNotes();
        }

        /// <summary>Compares the snapshot against what was last asked for. Only ever
        /// reports a mismatch - it never "corrects" anything, because the device's
        /// state is the truth and the request was the guess.</summary>
        private void CheckPendingWasApplied()
        {
            if (_pending is not { } pending)
                return;

            _pending = null;

            var row = Rails.FirstOrDefault(r => r.Id == pending.Id);
            if (row == null || row.IsOn == pending.Desired)
                return;

            LastError =
                $"{row.Name} is still {(row.IsOn ? "on" : "off")} - ProtoCore accepted the command but the rail didn't change. " +
                "That usually means the write to the power expander failed on the I2C bus rather than the request being rejected.";
        }

        private void HandleError(ProtocolFrame frame)
        {
            // A refusal is a definite answer about the request, so the pending
            // intent is spent - leaving it set would make the re-sync below look
            // like a silent bus fault.
            _pending = null;

            byte code = frame.Payload.Length > 0 ? frame.Payload[0] : (byte)0;

            if (code == VoltageRailCatalog.ErrNotPresent)
            {
                // Not a failure to report as one: it means this ProtoCore's firmware
                // has no Core dispatch path, i.e. predates rail control entirely.
                IsSupported = false;
                LastError = null;
                return;
            }

            LastError = code == VoltageRailCatalog.ErrDependency
                ? "ProtoCore refused that: another rail is still powered from it. Switch the dependent rail off first."
                : $"ProtoCore rejected the rail command ({FrameInterpreter.Describe(frame)}).";

            // The refusal tells us nothing about current state, so re-sync rather
            // than leaving the toggles showing what we hoped would happen.
            RequestRails();
        }

        /// <summary>Recomputes every row's explanatory note. Cheap, and simpler to
        /// reason about than working out which rows a single change could affect -
        /// enabling one rail can change the note on any other.</summary>
        private void RefreshNotes()
        {
            foreach (var row in Rails)
                row.RefreshNote(this);
        }

        internal bool IsRailOn(RailId id) => Rails.FirstOrDefault(r => r.Id == id)?.IsOn ?? false;

        internal void NotifyStateChanged() => RefreshNotes();
    }

    /// <summary>One rail's row in the tab.</summary>
    public partial class RailRowViewModel : ObservableObject
    {
        private readonly RailsViewModel _owner;

        public VoltageRail Rail { get; }
        public RailId Id => Rail.Id;
        public string Name => Rail.Name;

        /// <summary>"1.8 V", or "Variable" where this app doesn't know the configured
        /// output. Never a guessed number.</summary>
        public string VoltageLabel => Rail.NominalVoltage ?? "Variable";

        public string SuppliedBy => Rail.SuppliedBy;

        [ObservableProperty]
        private bool _isOn;

        /// <summary>What clicking will do, in plain words - "Turn on" / "Turn off".</summary>
        public string ActionLabel => IsOn ? "Turn off" : "Turn on";

        /// <summary>The consequence of clicking, or why it will be refused. Null when
        /// there's nothing worth saying.</summary>
        [ObservableProperty]
        private string? _note;

        public bool HasNote => Note != null;

        /// <summary>True when firmware will refuse this click. The button stays
        /// enabled anyway - firmware is the authority, and a control disabled by the
        /// app's own model reads as broken and can block something that would have
        /// worked.</summary>
        [ObservableProperty]
        private bool _willBeRefused;

        public RailRowViewModel(VoltageRail rail, RailsViewModel owner)
        {
            Rail = rail;
            _owner = owner;
        }

        partial void OnNoteChanged(string? value) => OnPropertyChanged(nameof(HasNote));

        internal void SetState(bool on)
        {
            IsOn = on;
            OnPropertyChanged(nameof(ActionLabel));
        }

        [RelayCommand]
        private void Toggle() => _owner.SetRail(Id, !IsOn);

        /// <summary>Explain before, report after: says what a click is about to switch
        /// on, or what is currently blocking a switch-off.</summary>
        internal void RefreshNote(RailsViewModel owner)
        {
            if (IsOn)
            {
                var blockers = VoltageRailCatalog.ChildrenOf(Id)
                    .Where(child => owner.IsRailOn(child.Id))
                    .Select(child => child.Name)
                    .ToList();

                WillBeRefused = blockers.Count > 0;
                Note = blockers.Count > 0
                    ? $"Can't switch off yet - {Join(blockers)} {(blockers.Count == 1 ? "runs" : "run")} from this rail."
                    : null;
                return;
            }

            WillBeRefused = false;

            var willAlsoEnable = VoltageRailCatalog.AncestorsOf(Id)
                .Where(ancestor => !owner.IsRailOn(ancestor.Id))
                .Select(ancestor => ancestor.Name)
                .ToList();

            Note = willAlsoEnable.Count > 0
                ? $"Turning this on will also switch on {Join(willAlsoEnable)}, which it is powered from."
                : null;
        }

        private static string Join(IReadOnlyList<string> items) =>
            items.Count switch
            {
                0 => "",
                1 => items[0],
                2 => $"{items[0]} and {items[1]}",
                _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}"
            };
    }
}
