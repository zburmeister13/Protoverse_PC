using CommunityToolkit.Mvvm.ComponentModel;
using ProtoVerseApp.Models;
using ProtoVerseApp.Models.Manual;

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

        public SlotViewModel(int index, object content, ProtoModId moduleId, SlotState slotState, string label)
        {
            Index = index;
            Content = content;
            ModuleId = moduleId;
            SlotState = slotState;
            Label = label;

            var document = ManualLibrary.TryBuild(moduleId);
            if (document != null)
                Manual = new ManualViewModel(document);
        }

        /// <summary>An empty slot. Kept as a factory so the navigator's "nothing here"
        /// wording lives in one place.</summary>
        public static SlotViewModel Empty(int index) =>
            new(index, new EmptySlotViewModel(), ProtoModId.None, SlotState.Empty, "Empty");
    }
}
