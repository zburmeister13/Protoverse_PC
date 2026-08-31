using System;
using System.Collections.Generic;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// Which ProtoMod types have an in-app manual in this build. Same one-line-per-type
    /// registration shape as <see cref="ViewModels.ModuleCatalog"/>, for the same
    /// reason: adding a manual should touch this file and a content file, nothing else.
    ///
    /// Only E05 is registered - this is an evaluation branch, and the point was to
    /// build one manual end to end rather than four shallow ones. A module with no
    /// entry here simply shows no Manual pane, which is the state every other module
    /// is in today.
    /// </summary>
    public static class ManualLibrary
    {
        private static readonly Dictionary<ProtoModId, Func<ManualDocument>> Manuals = new()
        {
            [ProtoModId.ElectronicLoad] = ElectronicLoadManual.Build,
        };

        public static bool HasManual(ProtoModId moduleId) => Manuals.ContainsKey(moduleId);

        /// <summary>Builds the manual for a module type, or null if this build has
        /// none. Built fresh per call rather than cached: a manual carries the
        /// learner's in-progress answers once progress is wired up, so two slots
        /// holding the same module type must not share one instance.</summary>
        public static ManualDocument? TryBuild(ProtoModId moduleId) =>
            Manuals.TryGetValue(moduleId, out var factory) ? factory() : null;
    }
}
