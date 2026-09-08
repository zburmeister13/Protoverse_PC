using System;
using System.Collections.Generic;
using System.Linq;
using ProtoVerseApp.Models;
using ProtoVerseApp.Models.Registry;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Which ProtoMod types this build of the app can render a dedicated panel for.
    /// MainViewModel never hardcodes a fixed lineup of modules - it just asks this
    /// catalog to build whatever PresenceReport says is actually plugged in.
    ///
    /// WHICH ID USES WHICH CONTROL SCHEME now lives in
    /// <c>Models/Registry/ProtoModRegistry.json</c> (the single source of truth -
    /// see <see cref="ProtoModRegistryService"/>), not here. This class is only the
    /// other half a registry entry can't express in data: the actual panel
    /// constructors, keyed by the <c>ControlScheme</c> string a registry entry
    /// names. Adding a new ProtoMod that reuses an *existing* control scheme (e.g.
    /// another Blinky-shaped board) is purely a registry row - nothing here
    /// changes. Adding one with a genuinely new control scheme still means writing
    /// that panel/ViewModel and registering its constructor below, which no amount
    /// of data-driving removes - a real, new interaction needs real, new code
    /// somewhere.
    /// </summary>
    public static class ModuleCatalog
    {
        /// <summary>Registry entries whose <c>ControlScheme</c> is this value have no
        /// software controls by design (every input is a switch/jumper on the
        /// board) - see <see cref="PassiveModuleViewModel"/>. Not a factory
        /// registration below, on purpose: a <see cref="ModulePanelViewModelBase"/>
        /// (which exists to send and parse frames) would be the wrong shape
        /// entirely for a board with no commands.</summary>
        private const string PassiveControlScheme = "Passive";

        private static readonly Dictionary<string, Func<FrameDispatcher, ModulePanelViewModelBase>> Factories = new()
        {
            ["BlinkyLedViewModel"] = dispatcher => new BlinkyLedViewModel(dispatcher),
            ["AccelTempViewModel"] = dispatcher => new AccelTempViewModel(dispatcher),
            ["ElectronicLoadViewModel"] = dispatcher => new ElectronicLoadViewModel(dispatcher),
        };

        public static bool IsPassive(ProtoModId moduleId) =>
            ProtoModRegistryService.FindByIdAssumingDefaultRevision(moduleId)?.ControlScheme == PassiveControlScheme;

        /// <summary>Display name for a passive board, or null if it isn't one.</summary>
        public static string? PassiveName(ProtoModId moduleId)
        {
            var entry = ProtoModRegistryService.FindByIdAssumingDefaultRevision(moduleId);
            return entry?.ControlScheme == PassiveControlScheme ? entry.Name : null;
        }

        /// <summary>Builds the panel view model for a detected ProtoMod, or null if
        /// this build has no panel registered for that type yet. Also returns null for
        /// a passive board - check <see cref="IsPassive"/> to tell the two apart.</summary>
        public static ModulePanelViewModelBase? TryCreate(ProtoModId moduleId, FrameDispatcher dispatcher)
        {
            var entry = ProtoModRegistryService.FindByIdAssumingDefaultRevision(moduleId);
            if (entry == null || entry.ControlScheme == PassiveControlScheme)
                return null;

            return Factories.TryGetValue(entry.ControlScheme, out var factory) ? factory(dispatcher) : null;
        }

        /// <summary>Every ProtoMod type this build can show a real panel for, with its
        /// display name - for the Help tab's "Currently supported ProtoMods" list.
        /// Derived from the same registry entries and the same Factories dictionary
        /// TryCreate uses, so it can never drift out of sync with what's actually
        /// supported.</summary>
        public static IReadOnlyList<(ProtoModId Id, string DisplayName)> SupportedModules =>
            ProtoModRegistryService.Entries
                .Where(e => e.Revision == ProtoModRegistryService.DefaultRevision && Factories.ContainsKey(e.ControlScheme))
                .Select(e => (e.ProtoModId, e.Name))
                .ToList();
    }
}
