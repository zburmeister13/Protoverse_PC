using System;
using System.Collections.Generic;
using System.Linq;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// The single place that knows which ProtoMod types this build of the app can
    /// render a dedicated panel for. MainViewModel never hardcodes a fixed lineup of
    /// modules - it just asks this catalog to build whatever PresenceReport says is
    /// actually plugged in. Supporting a new ProtoMod means adding one line here (plus
    /// its panel view model/view/DataTemplate) - nothing about the slot-population
    /// logic changes.
    /// </summary>
    public static class ModuleCatalog
    {
        private static readonly Dictionary<ProtoModId, (string DisplayName, Func<FrameDispatcher, ModulePanelViewModelBase> Factory)> Registrations = new()
        {
            [ProtoModId.BlinkyLed] = ("Blinky LED", dispatcher => new BlinkyLedViewModel(dispatcher)),
            [ProtoModId.AccelTemp] = ("Accelerometer + Temperature", dispatcher => new AccelTempViewModel(dispatcher)),
            [ProtoModId.ElectronicLoad] = ("Electronic Load", dispatcher => new ElectronicLoadViewModel(dispatcher)),
        };

        /// <summary>ProtoMod types that have no software controls *by design* - every
        /// input is a switch or jumper on the board, so there is nothing to command and
        /// never will be. These deliberately do NOT get a registration above: they have
        /// no commands, so a <see cref="ModulePanelViewModelBase"/> (which exists to
        /// send and parse frames) would be the wrong shape entirely.
        ///
        /// They are listed here rather than inferred, because "passive by design" and
        /// "this build hasn't caught up yet" are indistinguishable from
        /// <see cref="TryCreate"/> returning null - and they are opposite facts, only
        /// one of which is a gap to close. See
        /// <see cref="PassiveModuleViewModel"/>.</summary>
        private static readonly Dictionary<ProtoModId, string> Passive = new()
        {
            [ProtoModId.BasicLed] = "Simple LED",
        };

        public static bool IsPassive(ProtoModId moduleId) => Passive.ContainsKey(moduleId);

        /// <summary>Display name for a passive board, or null if it isn't one.</summary>
        public static string? PassiveName(ProtoModId moduleId) =>
            Passive.TryGetValue(moduleId, out var name) ? name : null;

        /// <summary>Builds the panel view model for a detected ProtoMod, or null if
        /// this build has no panel registered for that type yet. Also returns null for
        /// a passive board - check <see cref="IsPassive"/> to tell the two apart.</summary>
        public static ModulePanelViewModelBase? TryCreate(ProtoModId moduleId, FrameDispatcher dispatcher) =>
            Registrations.TryGetValue(moduleId, out var reg) ? reg.Factory(dispatcher) : null;

        /// <summary>Every ProtoMod type this build can show a real panel for, with its
        /// display name - for the Help tab's "Currently supported ProtoMods" list.
        /// Reads straight from the same registrations TryCreate uses, so it can never
        /// drift out of sync with what's actually supported.</summary>
        public static IReadOnlyList<(ProtoModId Id, string DisplayName)> SupportedModules =>
            Registrations.Select(kvp => (kvp.Key, kvp.Value.DisplayName)).ToList();
    }
}
