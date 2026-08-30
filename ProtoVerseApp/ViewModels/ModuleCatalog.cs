using System;
using System.Collections.Generic;
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
        private static readonly Dictionary<ProtoModId, Func<FrameDispatcher, ModulePanelViewModelBase>> Factories = new()
        {
            [ProtoModId.BlinkyLed] = dispatcher => new BlinkyLedViewModel(dispatcher),
            [ProtoModId.AccelTemp] = dispatcher => new AccelTempViewModel(dispatcher),
            [ProtoModId.ElectronicLoad] = dispatcher => new ElectronicLoadViewModel(dispatcher),
        };

        /// <summary>Builds the panel view model for a detected ProtoMod, or null if
        /// this build has no panel registered for that type yet.</summary>
        public static ModulePanelViewModelBase? TryCreate(ProtoModId moduleId, FrameDispatcher dispatcher) =>
            Factories.TryGetValue(moduleId, out var factory) ? factory(dispatcher) : null;
    }
}
