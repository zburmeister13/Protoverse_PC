using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ProtoVerseApp.Models;
using ProtoVerseApp.Models.Registry;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>One selectable board for a simulated slot.</summary>
    public record SimulatedBoardOption(ProtoModId Id, string Label);

    /// <summary>
    /// Lets Simulator mode stand in for a bench: pick what's in each of ProtoCore's
    /// slots and swap boards without touching hardware.
    ///
    /// The point isn't only convenience. Most of the interesting states in this app
    /// are reached by what's plugged in - a passive board, a board this build has no
    /// panel for, a board ProtoCore can't identify at all, an empty slot. Those used
    /// to be reachable only by editing <see cref="MockSerialService"/> and rebuilding,
    /// which is why several of them were historically verified once and then never
    /// again. The choices below deliberately include the awkward ones for that reason.
    ///
    /// Swapping goes through the real path: the mock volunteers a fresh PresenceReport
    /// exactly as firmware does on a hot-swap, so this exercises the app's actual
    /// rebuild logic rather than a simulator-only shortcut.
    /// </summary>
    public partial class SimulatorViewModel : ObservableObject
    {
        private readonly List<SlotSelectionViewModel> _slots = new();
        private MockSerialService? _mock;

        /// <summary>Everything a simulated slot can hold: empty, every board in the
        /// registry, and the two "you don't know this board" cases.</summary>
        public IReadOnlyList<SimulatedBoardOption> Options { get; }

        public ObservableCollection<SlotSelectionViewModel> Slots { get; } = new();

        /// <summary>Only shown while Simulator mode is on - it's meaningless against
        /// real hardware, where what's installed is a physical fact.</summary>
        [ObservableProperty]
        private bool _isVisible;

        public SimulatorViewModel(int slotCount)
        {
            var options = new List<SimulatedBoardOption>
            {
                new(ProtoModId.None, "— empty —")
            };

            // Every board the registry knows about, at the default revision. Reading
            // from the registry rather than a hand-kept list means a ProtoMod added
            // there shows up here automatically, which is the whole point of the
            // registry being the single source of truth.
            options.AddRange(ProtoModRegistryService.Entries
                .Where(e => e.Revision == ProtoModRegistryService.DefaultRevision)
                .OrderBy(e => e.CircuitCode)
                .Select(e => new SimulatedBoardOption(e.ProtoModId, $"{e.Name} ({e.CircuitCode})")));

            // The two degraded cases, deliberately reachable. "Unknown" is ProtoCore
            // reporting a board whose EEPROM read didn't match its catalog; the other
            // is a valid id this app has no panel for. They render differently and for
            // different reasons, and both are easy to break without noticing.
            options.Add(new SimulatedBoardOption(ProtoModId.Unknown, "Unrecognized board (0xFFE0)"));
            options.Add(new SimulatedBoardOption((ProtoModId)0x0FFF, "Board this app has no panel for"));

            Options = options;

            for (int i = 0; i < slotCount; i++)
            {
                var slot = new SlotSelectionViewModel(i, this);
                _slots.Add(slot);
                Slots.Add(slot);
            }
        }

        /// <summary>Called when the transport changes. A null mock means we're on real
        /// hardware, which hides the whole control.</summary>
        public void SetTransport(ISerialService serial)
        {
            _mock = serial as MockSerialService;
            IsVisible = _mock != null;

            if (_mock == null)
                return;

            // Show what the mock is actually reporting, rather than assuming the
            // defaults still hold.
            var installed = _mock.InstalledMods;
            for (int i = 0; i < Slots.Count; i++)
            {
                var id = i < installed.Count ? installed[i] : ProtoModId.None;
                Slots[i].SetWithoutPushing(Options.FirstOrDefault(o => o.Id == id) ?? Options[0]);
            }
        }

        internal void OnSlotChanged()
        {
            if (_mock == null)
                return;

            _mock.SetInstalledMods(Slots.Select(s => s.Selected?.Id ?? ProtoModId.None).ToList());
        }
    }

    /// <summary>One slot's board picker.</summary>
    public partial class SlotSelectionViewModel : ObservableObject
    {
        private readonly SimulatorViewModel _owner;
        private bool _suppressPush;

        public int Index { get; }
        public string Label => $"Slot {Index + 1}";

        [ObservableProperty]
        private SimulatedBoardOption? _selected;

        public SlotSelectionViewModel(int index, SimulatorViewModel owner)
        {
            Index = index;
            _owner = owner;
        }

        partial void OnSelectedChanged(SimulatedBoardOption? value)
        {
            if (_suppressPush)
                return;

            _owner.OnSlotChanged();
        }

        /// <summary>Sets the displayed choice without sending a new lineup back - used
        /// when syncing the UI to what the mock already reports, which would otherwise
        /// bounce straight back as a change.</summary>
        internal void SetWithoutPushing(SimulatedBoardOption option)
        {
            _suppressPush = true;
            try { Selected = option; }
            finally { _suppressPush = false; }
        }
    }
}
