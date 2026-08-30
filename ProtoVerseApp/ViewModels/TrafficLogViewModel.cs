using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Backs the collapsible "Traffic Log" debug panel: every frame sent or received
    /// on the current transport (real serial or simulator), plus reader errors, shown
    /// as hex + decoded fields. Invaluable once real hardware is involved and
    /// something doesn't behave as expected.
    /// </summary>
    public partial class TrafficLogViewModel : ObservableObject
    {
        private const int MaxEntries = 500;

        public ObservableCollection<TrafficLogEntry> Entries { get; } = new();

        [ObservableProperty]
        private bool _isExpanded;

        public TrafficLogViewModel(FrameDispatcher dispatcher)
        {
            dispatcher.FrameSent += frame => Add(new TrafficLogEntry(TrafficDirection.Sent, frame));
            dispatcher.FrameReceived += frame => Add(new TrafficLogEntry(TrafficDirection.Received, frame));
            dispatcher.FrameError += message => Add(new TrafficLogEntry(message));
            dispatcher.Disconnected += reason => Add(new TrafficLogEntry($"Disconnected: {reason}"));
        }

        [RelayCommand]
        private void Clear() => Entries.Clear();

        private void Add(TrafficLogEntry entry)
        {
            Entries.Add(entry);
            if (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
        }
    }
}
