using CommunityToolkit.Mvvm.ComponentModel;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Base class for every ProtoMod's panel view model. An instance only ever exists
    /// while ProtoCore reports the corresponding module present - MainViewModel builds
    /// one (via ModuleCatalog) when a PresenceReport says so, and discards it (calling
    /// Detach) the moment that's no longer true. So there's no separate "am I present"
    /// flag to track here: existing IS being present.
    ///
    /// Handles the plumbing that's identical for every module: subscribing to the
    /// dispatcher and filtering incoming frames to only the ones addressed to this
    /// module.
    ///
    /// A concrete panel (e.g. BlinkyLedViewModel) only needs to:
    ///   1. Set ModuleId
    ///   2. Override OnFrameReceived to interpret its own Response/StreamData payloads
    ///   3. Call SendCommand(...) to talk back
    /// </summary>
    public abstract partial class ModulePanelViewModelBase : ObservableObject
    {
        protected readonly FrameDispatcher Dispatcher;

        public abstract ProtoModId ModuleId { get; }

        /// <summary>Human-readable name shown as the panel header.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Always Occupied - a real module panel VM is never constructed for an
        /// empty or unsupported slot. Exists so the shared slot header in MainWindow.xaml
        /// can bind the same property name across every kind of slot content.</summary>
        public SlotState SlotState => SlotState.Occupied;

        protected ModulePanelViewModelBase(FrameDispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            Dispatcher.FrameReceived += OnAnyFrameReceived;
        }

        /// <summary>Unsubscribes from the dispatcher. Call this when the module is no
        /// longer reported present and this view model is being discarded, so it stops
        /// processing frames for a slot it no longer occupies and can be collected.
        /// Virtual so a panel holding its own resources (e.g. a local animation timer)
        /// can release them too - a timer isn't cleaned up just because nothing is
        /// visually bound to it anymore, so an override must stop it explicitly.</summary>
        public virtual void Detach() => Dispatcher.FrameReceived -= OnAnyFrameReceived;

        private void OnAnyFrameReceived(ProtocolFrame frame)
        {
            if (frame.ModuleId != ModuleId)
                return; // not for us - every other panel ignores this the same way

            OnFrameReceived(frame);
        }

        /// <summary>Called only for frames addressed to this module. Override to
        /// interpret Response/StreamData payloads specific to this ProtoMod.</summary>
        protected abstract void OnFrameReceived(ProtocolFrame frame);

        /// <summary>Sends a Command-type frame addressed to this module.</summary>
        protected void SendCommand(byte[] payload) =>
            Dispatcher.Send(ModuleId, MsgType.Command, payload);
    }
}
