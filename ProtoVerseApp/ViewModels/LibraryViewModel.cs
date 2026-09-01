using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// How a catalog entry relates to the user's hardware. Deliberately three states,
    /// not an owned/not-owned bool: without <see cref="PreviouslyConnected"/> the
    /// Library forgets a module the instant it's unplugged, which is both wrong and
    /// makes the tab worse the moment anyone hot-swaps.
    ///
    /// None of these is an ownership claim. Plugging in a borrowed board once moves it
    /// to PreviouslyConnected permanently, and there's no un-see - so the wording
    /// throughout is about connection history, never about what the user owns or
    /// bought (settled with the user, 2026-08-31). If real ownership is ever needed
    /// it's account data, layered on top of this rather than read out of it.
    /// </summary>
    public enum ModuleConnectionState
    {
        /// <summary>Never seen by this installation.</summary>
        NeverConnected,

        /// <summary>Seen plugged into a ProtoCore at some point, but not in a slot
        /// right now (or not connected at all this session).</summary>
        PreviouslyConnected,

        /// <summary>Reported present by the ProtoCore's most recent PresenceReport.</summary>
        ConnectedNow
    }

    /// <summary>
    /// One button in the Library's family filter row. <see cref="Series"/> is null for
    /// the "All" option; every other option pins the list to one of the three ProtoMod
    /// families (F/E/A - see <see cref="ProtoModSeries"/>).
    /// </summary>
    public partial class SeriesFilterViewModel : ObservableObject
    {
        private readonly LibraryViewModel _library;

        public ProtoModSeries? Series { get; }
        public string Label { get; }

        [ObservableProperty]
        private bool _isSelected;

        public SeriesFilterViewModel(LibraryViewModel library, ProtoModSeries? series, string label, int count)
        {
            _library = library;
            Series = series;
            // Count is baked in at construction: the catalog is static, so how many
            // modules a family holds never changes at runtime (only how many of them
            // are in the user's kit does, which the badges show per-card).
            Label = $"{label} ({count})";
        }

        [RelayCommand]
        private void Apply() => _library.ApplySeriesFilter(Series);
    }

    /// <summary>
    /// One clickable "leads into" link on a Library card. Carries the evidence
    /// sentence from <see cref="ProtoModNextStep"/> through to a tooltip, so a person
    /// can see why the app claims one module leads into another.
    /// </summary>
    public partial class NextStepLinkViewModel
    {
        private readonly LibraryViewModel _library;

        public string Code { get; }
        public string Label { get; }
        public string Evidence { get; }

        public NextStepLinkViewModel(LibraryViewModel library, ProtoModNextStep step)
        {
            _library = library;
            Code = step.Code;
            Evidence = step.Evidence;

            var target = ProtoModLibraryCatalog.FindByCode(step.Code);
            Label = target != null ? $"{target.Name} ({target.Code})" : step.Code;
        }

        [RelayCommand]
        private void Follow() => _library.SelectByCode(Code);
    }

    /// <summary>
    /// One ProtoMod card in the Library. Wraps a <see cref="ProtoModCatalogEntry"/>
    /// and turns its nullable fields into the "…coming soon" strings the UI shows, so
    /// a missing manual or an undrawn schematic reads as "more is on the way" rather
    /// than as a broken card. The only mutable state here is
    /// <see cref="IsInKit"/> - everything else is fixed catalog content.
    /// </summary>
    public partial class LibraryEntryViewModel : ObservableObject
    {
        private readonly LibraryViewModel _library;

        public ProtoModCatalogEntry Entry { get; }

        public string Code => Entry.Code;
        public string Title => $"{Entry.Name} ({Entry.Code})";
        public string SeriesLabel => $"{Entry.Series} series";

        public string DifficultyLabel => Entry.Difficulty ?? "Difficulty not yet rated";
        public string? TimeEstimate => Entry.TimeEstimate;
        public bool HasTimeEstimate => Entry.TimeEstimate != null;

        public string Description => Entry.Description ?? "A written overview of this ProtoMod is coming soon.";
        public bool HasDescription => Entry.Description != null;
        public string? DescriptionSource => Entry.DescriptionSource;

        public string SchematicSummary => Entry.SchematicSummary ?? "Schematic walkthrough coming soon.";
        public bool HasSchematicSummary => Entry.SchematicSummary != null;
        public string? SchematicSummarySource => Entry.SchematicSummarySource;

        /// <summary>None of the board schematics are bundled with the app as images
        /// yet - they exist only as KiCad-exported PDFs in the hardware tree - so the
        /// card shows a placeholder tile plus this reference rather than a real
        /// thumbnail. When drawings do get bundled, this is the field to replace.</summary>
        public string SchematicDrawingLabel => Entry.SchematicDrawingReference ?? "No schematic drawing on file yet.";
        public bool HasSchematicDrawing => Entry.SchematicDrawingReference != null;

        public IReadOnlyList<ProtoModIdea> Ideas => Entry.Ideas;
        public bool HasIdeas => Entry.Ideas.Count > 0;

        public IReadOnlyList<NextStepLinkViewModel> NextSteps { get; }
        public bool HasNextSteps => NextSteps.Count > 0;

        public string? ManualReference => Entry.ManualReference;
        public bool HasManual => Entry.ManualReference != null;

        /// <summary>False for a board that has no <see cref="ProtoModId"/> assigned on
        /// either side of the wire protocol yet - ProtoCore literally cannot report it,
        /// so "not yet connected" would be overstating what the app actually knows.
        /// Such a board also can't be tracked in a kit yet, since a kit record is keyed
        /// by ProtoModId and there isn't one - the card says so in its own footnote.
        /// (Keying kit records by circuit code instead would fix that, at the cost of
        /// a second identity scheme in the store; not worth it until a real board needs
        /// it.)</summary>
        public bool IsDetectable => Entry.ProtocolId != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsConnectedNow), nameof(IsPreviouslyConnected),
            nameof(StatusLabel), nameof(ShowKitPrompt), nameof(ShowChangeKitAnswer))]
        private ModuleConnectionState _connectionState;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInKit), nameof(IsNotMine), nameof(StatusLabel),
            nameof(ShowKitPrompt), nameof(ShowChangeKitAnswer), nameof(ChangeKitAnswerLabel))]
        private KitStatus _kitStatus;

        /// <summary>False while signed out - with nobody to attribute a kit to, the
        /// Library hides the ownership question entirely rather than asking a question
        /// it can't store the answer to.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowKitPrompt), nameof(ShowChangeKitAnswer))]
        private bool _isSignedIn;

        public bool IsConnectedNow => ConnectionState == ModuleConnectionState.ConnectedNow;
        public bool IsPreviouslyConnected => ConnectionState == ModuleConnectionState.PreviouslyConnected;
        public bool IsInKit => KitStatus == KitStatus.InKit;
        public bool IsNotMine => KitStatus == KitStatus.NotMine;

        /// <summary>Badge text. Ownership is only ever stated when the *user* has said
        /// so - being plugged in never promotes a card to "In your kit" on its own.</summary>
        public string StatusLabel => (ConnectionState, KitStatus) switch
        {
            (ModuleConnectionState.ConnectedNow, _) => "Connected now",
            (_, KitStatus.InKit) => "In your kit",
            (_, KitStatus.NotMine) => "Not in your kit",
            (ModuleConnectionState.PreviouslyConnected, _) => "Connected before",
            _ => "Not yet connected"
        };

        /// <summary>The "is this one yours?" prompt appears once the app has actually
        /// seen the board and the user hasn't answered yet - asking about a module
        /// nobody has ever plugged in would be noise on every card in the catalog.</summary>
        public bool ShowKitPrompt =>
            IsSignedIn &&
            KitStatus == KitStatus.Unanswered &&
            ConnectionState != ModuleConnectionState.NeverConnected;

        /// <summary>Once answered, the prompt collapses to a single "change that"
        /// affordance. This is what makes a one-time borrowed board correctable instead
        /// of stuck in someone's kit forever.</summary>
        public bool ShowChangeKitAnswer => IsSignedIn && KitStatus != KitStatus.Unanswered;

        public string ChangeKitAnswerLabel =>
            KitStatus == KitStatus.InKit ? "Not mine after all" : "Actually, this is mine";

        public LibraryEntryViewModel(LibraryViewModel library, ProtoModCatalogEntry entry)
        {
            _library = library;
            Entry = entry;
            NextSteps = entry.NextSteps.Select(s => new NextStepLinkViewModel(library, s)).ToList();
        }

        [RelayCommand]
        private void ClaimKit() => _library.SetKitStatus(this, KitStatus.InKit);

        [RelayCommand]
        private void DisclaimKit() => _library.SetKitStatus(this, KitStatus.NotMine);

        /// <summary>Flips a previous answer. Deliberately a toggle rather than a reset
        /// to Unanswered: the user has an opinion either way, and re-asking them the
        /// same question would be a step backwards.</summary>
        [RelayCommand]
        private void ToggleKit() =>
            _library.SetKitStatus(this, KitStatus == KitStatus.InKit ? KitStatus.NotMine : KitStatus.InKit);
    }

    /// <summary>
    /// Backs the Library tab: the whole ProtoMod catalog, not just what's plugged in.
    /// Read-only by design - it sends nothing, parses nothing, and never touches the
    /// wire protocol. Its one live input is <see cref="UpdateInstalled"/>, which
    /// MainViewModel calls whenever the slot lineup changes.
    ///
    /// Each card shows one of three connection states (see
    /// <see cref="ModuleConnectionState"/>), combining that live report with the
    /// persistent history in <see cref="ModuleHistoryStore"/> so a module doesn't
    /// vanish from the user's library the moment it's unplugged. Note the states
    /// describe connection history, not ownership.
    ///
    /// Catalog content itself is hardcoded for v1 - see
    /// <see cref="ProtoModLibraryCatalog"/> for why, and for the note about moving it
    /// to a manifest shared with the manual documents.
    /// </summary>
    public partial class LibraryViewModel : ObservableObject
    {
        public ObservableCollection<LibraryEntryViewModel> Entries { get; } = new();

        /// <summary>What the card grid actually binds to - <see cref="Entries"/> seen
        /// through the current family filter. A view rather than a second collection so
        /// filtering never disturbs card state (an entry's connection state, or which
        /// one is selected) - the cards are the same objects either way.</summary>
        public ICollectionView EntriesView { get; }

        private readonly AccountStore _accounts;

        /// <summary>Last PresenceReport's module types, kept so the card states can be
        /// recomputed when the signed-in account changes without waiting for the next
        /// report from the board.</summary>
        private HashSet<ProtoModId> _present = new();

        /// <summary>The family filter row: All, plus one option per ProtoMod family.</summary>
        public IReadOnlyList<SeriesFilterViewModel> SeriesFilters { get; }

        private ProtoModSeries? _activeSeries;

        /// <summary>Free-text filter over module name and circuit code only -
        /// deliberately not full-text over descriptions. Someone typing here is looking
        /// for a board they can name ("blinky", "E05"), and matching description prose
        /// would surface cards whose visible title has nothing to do with what they
        /// typed.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSearchText))]
        private string _searchText = "";

        public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

        partial void OnSearchTextChanged(string value)
        {
            EntriesView.Refresh();
            OnPropertyChanged(nameof(HasNoMatches));
            OnPropertyChanged(nameof(NoMatchesMessage));
        }

        /// <summary>True when the current filters hide every card - otherwise the grid
        /// is just silently blank, which reads as a broken tab rather than a search
        /// that found nothing.</summary>
        public bool HasNoMatches => EntriesView != null && EntriesView.IsEmpty;

        public string NoMatchesMessage => HasSearchText
            ? $"No ProtoMod matches “{SearchText.Trim()}” in this family."
            : "No ProtoMods in this family yet.";

        [RelayCommand]
        private void ClearSearch() => SearchText = "";

        /// <summary>Drives both the ListBox selection (which scrolls the card into
        /// view) and the highlight ring on the card itself. Set by clicking a card or
        /// by following a "leads into" link.</summary>
        [ObservableProperty]
        private LibraryEntryViewModel? _selectedEntry;

        [ObservableProperty]
        private string _kitSummary = "";

        /// <param name="accounts">Injectable so this can be pointed at a temp file for
        /// testing, or at server-backed accounts later, without the Library caring.
        /// Defaults to the shared per-machine file under %AppData%.</param>
        public LibraryViewModel(AccountStore? accounts = null)
        {
            _accounts = accounts ?? new AccountStore();
            _accounts.Changed += () => RefreshConnectionStates(_present);

            foreach (var entry in ProtoModLibraryCatalog.Entries)
                Entries.Add(new LibraryEntryViewModel(this, entry));

            EntriesView = new ListCollectionView(Entries) { Filter = PassesFilters };

            // "All" first and selected by default: the Library's whole point is showing
            // the full catalog, so a filter is something the user opts into.
            var filters = new List<SeriesFilterViewModel>
            {
                new(this, null, "All", Entries.Count)
            };
            foreach (ProtoModSeries series in System.Enum.GetValues<ProtoModSeries>())
                filters.Add(new SeriesFilterViewModel(this, series, series.ToString(),
                    Entries.Count(e => e.Entry.Series == series)));

            SeriesFilters = filters;
            filters[0].IsSelected = true;

            // Paint whatever the signed-in account already knows before the first
            // connection, so the tab isn't blank about modules plugged in on other days.
            RefreshConnectionStates(_present);
        }

        /// <summary>Family filter and search combine with AND: picking "Explorers" and
        /// typing "load" shows Explorers modules matching "load", not both sets.</summary>
        private bool PassesFilters(object o)
        {
            if (o is not LibraryEntryViewModel entry)
                return false;

            if (_activeSeries != null && entry.Entry.Series != _activeSeries)
                return false;

            var query = SearchText?.Trim();
            if (string.IsNullOrEmpty(query))
                return true;

            // Name or circuit code, case-insensitive substring. Substring rather than
            // prefix so "led" finds both Blinky's "LED" and "Simple LED".
            return entry.Entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || entry.Entry.Code.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>Pins the grid to one ProtoMod family, or shows everything when
        /// <paramref name="series"/> is null.</summary>
        public void ApplySeriesFilter(ProtoModSeries? series)
        {
            _activeSeries = series;
            foreach (var filter in SeriesFilters)
                filter.IsSelected = filter.Series == series;

            EntriesView.Refresh();
            OnPropertyChanged(nameof(HasNoMatches));
            OnPropertyChanged(nameof(NoMatchesMessage));
        }

        /// <summary>Marks which catalog entries are physically plugged into the
        /// ProtoCore right now, and folds them into the persistent connection history.
        /// Takes the same <see cref="ProtoModId"/> values MainViewModel just built its
        /// slot panels from, so the Library can never disagree with what the slots are
        /// showing.</summary>
        public void UpdateInstalled(IEnumerable<ProtoModId> installed)
        {
            _present = new HashSet<ProtoModId>(installed);

            // Record before refreshing: anything present is by definition now part of
            // this account's history. No-ops when signed out - a sighting with nobody to
            // attribute it to isn't worth storing, and the cards say so.
            _accounts.RecordSightings(_present);
            RefreshConnectionStates(_present);
        }

        /// <summary>Drops back to history-only - used on disconnect, where the app no
        /// longer knows what's currently installed. Anything that was connected stays
        /// visible as "Connected before" rather than reverting to never-seen: unplugging
        /// a board isn't the same as never having had it.</summary>
        public void ClearInstalled()
        {
            _present = new HashSet<ProtoModId>();
            RefreshConnectionStates(_present);
        }

        /// <summary>Records the user's answer to "is this one yours?" and repaints.
        /// Routed through here rather than each card touching the store directly, so
        /// there's one place where a kit answer is written.</summary>
        public void SetKitStatus(LibraryEntryViewModel entry, KitStatus status)
        {
            if (entry.Entry.ProtocolId is not { } id)
                return;

            _accounts.SetKitStatus(id, status);  // raises Changed -> RefreshConnectionStates
        }

        private void RefreshConnectionStates(HashSet<ProtoModId> present)
        {
            bool signedIn = _accounts.IsSignedIn;

            foreach (var entry in Entries)
            {
                entry.IsSignedIn = signedIn;

                var record = entry.Entry.ProtocolId is { } id ? _accounts.FindRecord(id) : null;

                // A record can exist with no sighting behind it - the user can claim a
                // board they own but have never plugged in - so "seen" is the timestamp
                // being set, not merely the record existing.
                bool seen = record != null && record.FirstSeenUtc != default;

                entry.ConnectionState = entry.Entry.ProtocolId switch
                {
                    // A board with no assigned ProtoModId can never be reported, so it
                    // can never be anything but NeverConnected - the card says as much
                    // in its own footnote rather than implying the user doesn't have it.
                    null => ModuleConnectionState.NeverConnected,
                    { } id2 when present.Contains(id2) => ModuleConnectionState.ConnectedNow,
                    _ when seen => ModuleConnectionState.PreviouslyConnected,
                    _ => ModuleConnectionState.NeverConnected
                };

                entry.KitStatus = record?.KitStatus ?? KitStatus.Unanswered;
            }

            KitSummary = BuildSummary();
        }

        private string BuildSummary()
        {
            if (!_accounts.IsSignedIn)
            {
                return _accounts.LastError is { } signedOutError
                    ? $"Sign in (top right) to track which of these are in your kit.  ({signedOutError})"
                    : "Sign in (top right) to track which of these are in your kit.";
            }

            int now = Entries.Count(e => e.IsConnectedNow);
            int inKit = Entries.Count(e => e.IsInKit);
            var who = _accounts.ActiveAccount?.DisplayName ?? "";

            // Two independent numbers, deliberately reported separately rather than
            // rolled together: what's plugged in this second, and what this person has
            // actually said is theirs.
            var parts = new List<string>();
            if (now > 0)
                parts.Add($"{now} plugged in right now");
            if (inKit > 0)
                parts.Add($"{inKit} of {Entries.Count} confirmed in {who}'s kit");

            var summary = parts.Count > 0
                ? string.Join(" · ", parts)
                : $"Nothing confirmed in {who}'s kit yet - plug a ProtoMod in, or claim one below.";

            // Surface a broken accounts file rather than quietly showing the user an
            // emptier library than they actually have.
            return _accounts.LastError is { } error ? $"{summary}  ({error})" : summary;
        }

        /// <summary>Selects the card for a circuit code, which the view turns into a
        /// scroll-into-view plus a highlight ring. No-ops for a code that isn't in the
        /// catalog rather than clearing the current selection.</summary>
        public void SelectByCode(string code)
        {
            var match = Entries.FirstOrDefault(e => e.Code == code);
            if (match == null)
                return;

            // A "leads into" link can point across families (nothing says a
            // Fundamentals board can't lead into an Explorers one), and either filter
            // could be hiding the target - selecting it would then highlight a card
            // nobody can see. Following a link is an explicit request to go look at
            // that module, so it clears whatever is in the way.
            if (!PassesFilters(match))
            {
                SearchText = "";
                ApplySeriesFilter(null);
            }

            SelectedEntry = match;
        }
    }
}
