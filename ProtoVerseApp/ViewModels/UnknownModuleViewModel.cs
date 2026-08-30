using ProtoVerseApp.Models;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Placeholder for a slot occupied by a ProtoMod type ProtoCore reports but that
    /// this build of the app has no panel registered for (see ModuleCatalog). There
    /// will eventually be far more ProtoMod types than this app ships panels for at
    /// any given time, so an unrecognized one needs to degrade gracefully instead of
    /// crashing or silently disappearing from the slot list.
    /// </summary>
    public class UnknownModuleViewModel
    {
        public ProtoModId ModuleId { get; }
        public string DisplayName { get; }
        public SlotState SlotState => SlotState.Unsupported;

        public UnknownModuleViewModel(ProtoModId moduleId)
        {
            ModuleId = moduleId;
            DisplayName = $"Unsupported module ({moduleId}, 0x{(ushort)moduleId:X4})";
        }
    }
}
