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

        /// <summary>Builds the panel view model for a detected ProtoMod, or null if
        /// this build has no panel registered for that type yet.</summary>
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
