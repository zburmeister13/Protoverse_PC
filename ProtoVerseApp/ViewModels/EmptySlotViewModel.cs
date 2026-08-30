namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Placeholder shown for a physical ProtoMod slot with nothing detected in it.
    /// All three slots start out as this at boot (before any connection) and revert
    /// to it on disconnect - the app makes no assumption about which ProtoMods, if
    /// any, are plugged in until a PresenceReport says otherwise.
    /// </summary>
    public class EmptySlotViewModel
    {
        public string DisplayName => "Empty";
        public SlotState SlotState => SlotState.Empty;
    }
}
