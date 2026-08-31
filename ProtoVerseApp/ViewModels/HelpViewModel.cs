using System.Collections.Generic;
using System.Linq;
using ProtoVerseApp.Models;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>One row in the Help tab's "Currently supported ProtoMods" list.
    /// CircuitCode is null if ModuleCatalog has a panel registered for this type but
    /// ProtoModBoardCatalog doesn't have a matching entry yet - shouldn't normally
    /// happen since both should be updated together, but this displays gracefully
    /// rather than throwing if they ever drift.</summary>
    public record SupportedModuleInfo(ProtoModId Id, string DisplayName, string? CircuitCode);

    /// <summary>
    /// Backs the Help tab: a short, single-sentence-per-item revision history aimed at
    /// an end user (not CHANGELOG.md's full prompt/purpose/changes detail, which is
    /// aimed at whoever's developing this app), plus which ProtoMod types this build
    /// can actually show a real panel for - read straight from ModuleCatalog so it can
    /// never drift out of sync with what's actually registered there.
    /// </summary>
    public class HelpViewModel
    {
        /// <summary>Newest first. Add one line here whenever a change is worth telling
        /// an end user about - not every CHANGELOG.md entry needs a matching line here
        /// (routine internal/refactor changes don't), but anything that changes what
        /// they see or can do does.</summary>
        public IReadOnlyList<string> RevisionNotes { get; } = new[]
        {
            "Added a real ProtoVerse app icon (taskbar, title bar, Alt-Tab) instead of the generic default.",
            "Corrected the Accel+Temp ProtoMod's expected circuit code from F02 to E03, per the module's product manual.",
            "Supported ProtoMods now show their expected EEPROM circuit code alongside their name.",
            "Redesigned the accelerometer display into an X/Y tilt plot and a Z-axis fill gauge centered on -1g.",
            "Added live charts to the Accel+Temp and Electronic Load panels, built and tested against Simulator mode.",
            "Widened the wire protocol's module ID field so the ProtoMod catalog can grow past 1,000 types.",
            "Blinky LED gained pattern (Bounce/Chase/All/Random), direction, and manual LED controls, with a live animation preview.",
            "Applied a branded dark navy/teal/green theme across the whole app.",
            "Connecting now automatically checks which ProtoMods are installed, instead of requiring a separate click.",
            "Fixed Simulator mode's slots appearing empty even while traffic was visibly flowing.",
            "Slots now start empty and populate dynamically from whatever's actually plugged in, instead of assuming a fixed set of modules.",
            "Added Simulator mode and a raw traffic log for developing and testing without real hardware.",
        };

        public IReadOnlyList<SupportedModuleInfo> SupportedModules { get; } =
            ModuleCatalog.SupportedModules
                .Select(m => new SupportedModuleInfo(
                    m.Id,
                    m.DisplayName,
                    ProtoModBoardCatalog.Entries.FirstOrDefault(e => e.Id == m.Id)?.CircuitCode))
                .ToList();
    }
}
