using System.Linq;
using ProtoVerseApp.Models;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Stands in for a ProtoMod that has no software controls *by design* - every input
    /// is a switch or jumper on the board itself, so there is nothing for ProtoCore to
    /// command and never will be.
    ///
    /// This exists because the alternative was actively misleading. Such a board falls
    /// through <see cref="ModuleCatalog.TryCreate"/> exactly like one this app hasn't
    /// caught up with yet, so it used to render as
    /// <see cref="UnknownModuleViewModel"/>: an orange "unsupported" dot and "this
    /// ProtoMod isn't supported by this version of the app yet". For Simple LED (F02)
    /// that reads as the app being broken, when in fact the board is working perfectly
    /// and the learner should be looking at the hardware. It matters more now that a
    /// manual can appear beneath it - being told the app doesn't support your board,
    /// directly above its manual, is a bad first impression of both.
    ///
    /// So the distinction is deliberate and worth keeping: "no panel because none is
    /// possible" is a different fact from "no panel yet", and only one of them is
    /// something to fix. The slot reports <see cref="SlotState.Occupied"/> - green, the
    /// same as any working module - because that is the truth: the board is present and
    /// functioning.
    /// </summary>
    public class PassiveModuleViewModel
    {
        public ProtoModId ModuleId { get; }

        /// <summary>Present and working. Nothing here is degraded, so nothing should
        /// look degraded.</summary>
        public SlotState SlotState => SlotState.Occupied;

        public string DisplayName { get; }

        /// <summary>Shown where a control panel would be. Deliberately points at the
        /// board rather than apologising for the app.</summary>
        public string Message { get; }

        public PassiveModuleViewModel(ProtoModId moduleId, string name)
        {
            ModuleId = moduleId;
            DisplayName = name;

            var code = ProtoModBoardCatalog.Entries.FirstOrDefault(e => e.Id == moduleId)?.CircuitCode;
            var codeSuffix = code != null ? $" ({code})" : "";
            Message = $"{name}{codeSuffix} has no software controls - everything on this board is set with the switches on the board itself. " +
                      "Nothing is missing here; work through the manual below and change the settings by hand.";
        }
    }
}
