using System;
using System.Collections.Generic;
using ProtoVerseApp.Models.Registry;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// Which ProtoMod types have an in-app manual in this build.
    ///
    /// WHICH ID USES WHICH MANUAL now lives in
    /// <c>Models/Registry/ProtoModRegistry.json</c> (the single source of truth -
    /// see <see cref="ProtoModRegistryService"/>), not here, the same split
    /// <see cref="ViewModels.ModuleCatalog"/> uses for control schemes: the registry
    /// says *which* manual reference a given ProtoMod uses, this class is only the
    /// other half a registry entry can't express in data - the actual content
    /// factory a manual reference string names. Adding a manual still means writing
    /// its content and registering the factory below; the registry just stops that
    /// registration from being a second, independently-drifting place to say which
    /// ProtoMod it belongs to.
    ///
    /// A module with no matching registry entry, or whose ManualReference doesn't
    /// match a key below, simply shows no Manual pane - the state every module
    /// without a written manual is in today.
    /// </summary>
    public static class ManualLibrary
    {
        private static readonly Dictionary<string, Func<ManualDocument>> Factories = new()
        {
            ["BlinkyManual"] = BlinkyManual.Build,
            ["SimpleLedManual"] = SimpleLedManual.Build,
            ["ElectronicLoadManual"] = ElectronicLoadManual.Build,
        };

        public static bool HasManual(ProtoModId moduleId) => TryGetFactory(moduleId) != null;

        /// <summary>Builds the manual for a module type, or null if this build has
        /// none. Built fresh per call rather than cached: a manual carries the
        /// learner's in-progress answers once progress is wired up, so two slots
        /// holding the same module type must not share one instance.</summary>
        public static ManualDocument? TryBuild(ProtoModId moduleId) => TryGetFactory(moduleId)?.Invoke();

        private static Func<ManualDocument>? TryGetFactory(ProtoModId moduleId)
        {
            var reference = ProtoModRegistryService.FindByIdAssumingDefaultRevision(moduleId)?.ManualReference;
            return reference != null && Factories.TryGetValue(reference, out var factory) ? factory : null;
        }
    }
}
