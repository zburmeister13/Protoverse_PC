using System;
using System.Collections.Generic;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// Which ProtoMod types have an in-app manual in this build. Same one-line-per-type
    /// registration shape as <see cref="ViewModels.ModuleCatalog"/>, for the same
    /// reason: adding a manual should touch this file and a content file, nothing else.
    ///
    /// Three are registered. E05 was built first to prove the renderer end to end; F01
    /// and F02 were written afterwards against E05 as a template, and between them they
    /// cover both ends of the difficulty range the F/E/A families imply.
    ///
    /// F02 is the odd one and worth knowing about before adding a fourth: it is the
    /// first manual for a board with no software controls at all, so its slot shows a
    /// <see cref="ViewModels.PassiveModuleViewModel"/> rather than a panel, and it is
    /// the first manual assembled from a pre-template source document - which is why
    /// parts of it carry <see cref="CalloutKind.NeedsReview"/> markers. Expect both
    /// situations again; most of the catalog's manuals predate the current template.
    ///
    /// A module with no entry here simply shows no Manual pane, which is the state every
    /// other module is in today.
    /// </summary>
    public static class ManualLibrary
    {
        private static readonly Dictionary<ProtoModId, Func<ManualDocument>> Manuals = new()
        {
            [ProtoModId.BlinkyLed] = BlinkyManual.Build,
            [ProtoModId.BasicLed] = SimpleLedManual.Build,
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
