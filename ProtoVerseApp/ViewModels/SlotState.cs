namespace ProtoVerseApp.ViewModels
{
    /// <summary>Coarse visual state of one of the (at most three) physical ProtoMod
    /// slots, independent of which specific module - if any - occupies it. Drives the
    /// status dot color in MainWindow.xaml.</summary>
    public enum SlotState
    {
        /// <summary>Nothing detected in this slot - either nothing has connected yet,
        /// or the last connection dropped and any previous answer can no longer be
        /// trusted (a module could be swapped while unplugged).</summary>
        Empty,

        /// <summary>A module is present and this app has a panel for it.</summary>
        Occupied,

        /// <summary>A module is present but this build of the app doesn't have a panel
        /// registered for its ProtoModId yet.</summary>
        Unsupported
    }
}
