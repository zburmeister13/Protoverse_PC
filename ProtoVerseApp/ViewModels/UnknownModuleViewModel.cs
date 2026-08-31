using System.Linq;
using ProtoVerseApp.Models;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Placeholder for a slot this app can't show a real panel for. Covers two
    /// conceptually different situations that happen to share one view model and one
    /// status-dot appearance (both orange/Unsupported - something needs attention,
    /// but neither is a crash or a silently-dropped slot):
    ///
    ///   1. A real, firmware-known ProtoModId this build just has no panel registered
    ///      for yet (see ModuleCatalog) - there will eventually be far more ProtoMod
    ///      types than any given app build ships panels for, so this is expected and
    ///      routine. Fix: update this app.
    ///   2. ProtoModId.Unknown (0xFFE0) - ProtoCore itself couldn't identify what's in
    ///      the slot (a valid/plausible EEPROM read that didn't match anything in
    ///      firmware's own catalog). This app has no panel for it either, but for a
    ///      completely different reason - the board isn't even recognized at the
    ///      platform level. Fix: update firmware's catalog (or check the board/EEPROM
    ///      itself). These two used to be visually identical, which caused real
    ///      confusion troubleshooting a physical board (see CHANGELOG 2026-08-30) -
    ///      the message below is deliberately different for each case now.
    /// </summary>
    public class UnknownModuleViewModel
    {
        public ProtoModId ModuleId { get; }
        public string DisplayName { get; }
        public SlotState SlotState => SlotState.Unsupported;

        public UnknownModuleViewModel(ProtoModId moduleId)
        {
            ModuleId = moduleId;
            DisplayName = moduleId == ProtoModId.Unknown
                ? "Something's plugged in here, but ProtoCore doesn't recognize its EEPROM identity"
                : BuildUnsupportedMessage(moduleId);
        }

        /// <summary>The raw ProtoModId (e.g. "0x0004") means nothing to a person -
        /// what's actually printed/programmed onto the physical board is its circuit
        /// code (e.g. "F02", via ProtoMod_Programmer.ino's EEPROM layout), so lead
        /// with that instead whenever this app's ProtoModBoardCatalog mirror knows it.
        /// Falls back to the raw hex ID only for a type that's genuinely uncataloged
        /// on this side too (this app's ProtoModBoardCatalog.cs hasn't been updated to
        /// mirror firmware's catalog yet) - still useful to show something rather than
        /// nothing in that case.</summary>
        private static string BuildUnsupportedMessage(ProtoModId moduleId)
        {
            var circuitCode = ProtoModBoardCatalog.Entries.FirstOrDefault(e => e.Id == moduleId)?.CircuitCode;
            return circuitCode != null
                ? $"Unsupported module: {moduleId} (circuit code {circuitCode})"
                : $"Unsupported module: {moduleId} (0x{(ushort)moduleId:X4})";
        }
    }
}
