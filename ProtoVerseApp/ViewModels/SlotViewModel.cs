using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ProtoVerseApp.Models;
using ProtoVerseApp.Models.Manual;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// One of ProtoCore's physical slots, as the left-hand navigator shows it.
    ///
    /// Wraps - rather than replaces - the existing per-module panel view models: the
    /// panel in <see cref="Content"/> is exactly the object the stacked layout used to
    /// bind to, built by the same <see cref="ModuleCatalog"/> call, so no module's
    /// control logic changed when the layout did. This type only adds the things a
    /// navigator needs and a stacked list didn't: which physical slot this is, a short
    /// label, and whether there's a manual to open.
    /// </summary>
    public partial class SlotViewModel : ObservableObject
    {
        /// <summary>Zero-based physical slot index. Displayed as 1-based.</summary>
        public int Index { get; }

        /// <summary>The module's panel view model, or an
        /// <see cref="EmptySlotViewModel"/>/<see cref="UnknownModuleViewModel"/>
        /// placeholder. Untouched by this class.</summary>
        public object Content { get; }

        public ProtoModId ModuleId { get; }
        public SlotState SlotState { get; }

        /// <summary>Short label for the navigator - the module's name, or why there
        /// isn't one. Kept to one line; the detail pane carries the full story.</summary>
        public string Label { get; }

        public string SlotName => $"Slot {Index + 1}";

        /// <summary>The in-app manual for whatever's in this slot, or null. Built per
        /// slot rather than shared, because a manual will eventually hold the learner's
        /// own answers and two slots can hold the same module type.</summary>
        public ManualViewModel? Manual { get; }

        public bool HasManual => Manual != null;

        /// <summary>Whether this slot has anything worth opening - drives whether the
        /// navigator row is selectable.</summary>
        public bool IsOccupied => SlotState != SlotState.Empty;

        private readonly AccountStore _accounts;

        /// <summary>Whether this slot's live controls (<see cref="Content"/>) are gated
        /// shut right now. Only ever true for an actual module panel
        /// (<see cref="ModulePanelViewModelBase"/>) - an empty, unsupported, or passive
        /// slot has no controls to lock in the first place. Two independent gates, in
        /// order: nobody signed in (there's no account to own progress or kit tracking,
        /// and this app has no guest mode - 2026-09-07 user decision), then, once signed
        /// in, whether this account has ever reached "Set up and try it" in this
        /// module's manual. A module with no manual (e.g. Accel+Temp, which doesn't have
        /// one yet) skips straight past the second gate rather than locking forever with
        /// no way to clear it.</summary>
        [ObservableProperty]
        private bool _isControlLocked;

        /// <summary>Why <see cref="IsControlLocked"/> is true right now, for the overlay
        /// to show. Meaningless while unlocked.</summary>
        [ObservableProperty]
        private string? _lockMessage;

        public SlotViewModel(int index, object content, ProtoModId moduleId, SlotState slotState, string label,
            AccountStore accounts)
        {
            Index = index;
            Content = content;
            ModuleId = moduleId;
            SlotState = slotState;
            Label = label;
            _accounts = accounts;

            var document = ManualLibrary.TryBuild(moduleId);
            if (document != null)
                Manual = new ManualViewModel(document);

            // Only a real control panel has anything to gate - wiring up the account/
            // manual subscriptions for an empty, unsupported, or passive slot would just
            // be dead weight that Detach() then has to unwind for no reason.
            if (Content is ModulePanelViewModelBase)
            {
                _accounts.Changed += RecomputeLock;
                if (Manual != null)
                    Manual.PropertyChanged += OnManualPropertyChanged;
                RecomputeLock();
            }
        }

        private void RecomputeLock()
        {
            if (!_accounts.IsSignedIn)
            {
                IsControlLocked = true;
                LockMessage = "Sign in to control this ProtoMod.";
                return;
            }

            if (Manual == null || _accounts.HasReachedSetup(ModuleId))
            {
                IsControlLocked = false;
                return;
            }

            IsControlLocked = true;
            var setupTitle = Manual.Sections.FirstOrDefault(s => s.Id == "setup")?.Title ?? "Set up and try it";
            LockMessage = $"Read through \"{setupTitle}\" in the manual below to unlock this ProtoMod's controls.";
        }

        /// <summary>Fires on every scroll position change while the manual is open - see
        /// <see cref="Views.ManualView.OnManualScrolled"/>. Marks setup reached the first
        /// time the scrolled-to section is "Set up and try it" or anything after it, so
        /// it fires whether the learner clicked the table of contents or just scrolled
        /// down normally, and whether or not they pause exactly on that section.</summary>
        private void OnManualPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ManualViewModel.SelectedSection) || !IsControlLocked || Manual?.SelectedSection == null)
                return;

            var sections = Manual.Sections;
            int setupIndex = -1, currentIndex = -1;
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].Id == "setup")
                    setupIndex = i;
                if (ReferenceEquals(sections[i], Manual.SelectedSection))
                    currentIndex = i;
            }

            if (setupIndex == -1 || currentIndex < setupIndex)
                return;

            // Persists and raises Changed, which RecomputeLock (subscribed above) turns
            // into IsControlLocked flipping to false - no need to set it here too.
            _accounts.MarkSetupReached(ModuleId);
        }

        /// <summary>Unsubscribes from the account store and manual so a slot discarded by
        /// a hot-swap rebuild (see <see cref="MainViewModel.DetachModulePanels"/>) stops
        /// reacting to sign-in/out and can be collected. Safe to call on a slot that
        /// never subscribed in the first place (empty/unsupported/passive).</summary>
        public void Detach()
        {
            _accounts.Changed -= RecomputeLock;
            if (Manual != null)
                Manual.PropertyChanged -= OnManualPropertyChanged;
        }

        /// <summary>An empty slot. Kept as a factory so the navigator's "nothing here"
        /// wording lives in one place.</summary>
        public static SlotViewModel Empty(int index, AccountStore accounts) =>
            new(index, new EmptySlotViewModel(), ProtoModId.None, SlotState.Empty, "Empty", accounts);
    }
}
