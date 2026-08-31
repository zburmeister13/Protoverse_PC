using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtoVerseApp.Models.Manual;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>One row of a fillable value table. The fixed cells come from the
    /// content; the editable ones are the learner's.</summary>
    public partial class ValueRowViewModel : ObservableObject
    {
        public IReadOnlyList<string> FixedCells { get; }
        public ObservableCollection<ValueCellViewModel> EditableCells { get; }

        public ValueRowViewModel(IReadOnlyList<string> fixedCells, IEnumerable<ValueCellViewModel> editable)
        {
            FixedCells = fixedCells;
            EditableCells = new ObservableCollection<ValueCellViewModel>(editable);
        }
    }

    /// <summary>One editable cell. <see cref="Key"/> is the persistence key described
    /// in <see cref="ManualProgress"/> - held now even though nothing saves yet, so
    /// wiring persistence later doesn't require re-deriving keys.</summary>
    public partial class ValueCellViewModel : ObservableObject
    {
        public string Key { get; }

        [ObservableProperty]
        private string _value = "";

        public ValueCellViewModel(string key) => Key = key;
    }

    /// <summary>One reflection question plus the learner's answer.</summary>
    public partial class QuestionViewModel : ObservableObject
    {
        public string Key { get; }
        public string Text { get; }

        [ObservableProperty]
        private string _answer = "";

        public QuestionViewModel(string key, string text)
        {
            Key = key;
            Text = text;
        }
    }

    /// <summary>A checkable item in a "You'll need" list or a step list.</summary>
    public partial class CheckableItemViewModel : ObservableObject
    {
        public string Key { get; }
        public string Text { get; }
        public string? Observe { get; }
        public string? Ordinal { get; }
        public bool HasObserve => Observe != null;

        [ObservableProperty]
        private bool _isChecked;

        public CheckableItemViewModel(string key, string text, string? observe = null, string? ordinal = null)
        {
            Key = key;
            Text = text;
            Observe = observe;
            Ordinal = ordinal;
        }
    }

    /// <summary>A manual section as the UI sees it: its blocks, plus the reveal state
    /// for a spoiler section.</summary>
    public partial class ManualSectionViewModel : ObservableObject
    {
        public ManualSection Section { get; }
        public string Id => Section.Id;
        public string Title => Section.Title;
        public bool IsAppendix => Section.IsAppendix;
        public bool IsSpoiler => Section.IsSpoiler;

        /// <summary>Blocks, with the interactive ones already turned into view models.
        /// Non-interactive blocks pass through as the record types themselves and are
        /// matched by DataTemplate.</summary>
        public IReadOnlyList<object> Blocks { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowContent), nameof(ShowRevealButton))]
        private bool _isRevealed;

        /// <summary>Content shows unless this is a spoiler section that hasn't been
        /// revealed. The template is emphatic that nothing spoiling an answer may be
        /// visible before the follow-up questions.</summary>
        public bool ShowContent => !IsSpoiler || IsRevealed;
        public bool ShowRevealButton => IsSpoiler && !IsRevealed;

        public ManualSectionViewModel(ManualSection section, IReadOnlyList<object> blocks)
        {
            Section = section;
            Blocks = blocks;
        }

        [RelayCommand]
        private void Reveal() => IsRevealed = true;
    }

    /// <summary>
    /// Renders one ProtoMod manual. Turns the content model
    /// (<see cref="ManualDocument"/>) into bindable state: a table of contents, the
    /// section list, and view models for the interactive blocks.
    ///
    /// The learner's input (ticked steps, table cells, written answers) lives here in
    /// memory and is deliberately keyed for persistence but not yet saved - see
    /// <see cref="ManualProgress"/> for where it would go and why that's a small step.
    /// </summary>
    public partial class ManualViewModel : ObservableObject
    {
        public ManualDocument Document { get; }
        public ManualHeader Header => Document.Header;
        public string SourceNote => Document.SourceNote;

        public IReadOnlyList<ManualSectionViewModel> Sections { get; }

        /// <summary>Main learner flow - everything before the appendices.</summary>
        public IReadOnlyList<ManualSectionViewModel> MainSections { get; }

        /// <summary>Appendices, kept structurally separate from the learner flow
        /// rather than being sections 10-12 of one list.</summary>
        public IReadOnlyList<ManualSectionViewModel> Appendices { get; }

        public bool HasPlaceholders => Document.PlaceholderCount > 0;

        public string PlaceholderWarning =>
            $"{Document.PlaceholderCount} section(s) of this manual haven't been written yet and show as placeholders.";

        /// <summary>Set by the TOC; the view scrolls to it. Not a filter - the manual
        /// stays one continuous scroll, since a learner mid-task flips back and forth
        /// between setup and observations.</summary>
        [ObservableProperty]
        private ManualSectionViewModel? _selectedSection;

        public ManualViewModel(ManualDocument document)
        {
            Document = document;

            var sections = new List<ManualSectionViewModel>();
            foreach (var section in document.Sections)
                sections.Add(new ManualSectionViewModel(section, BuildBlocks(section)));

            Sections = sections;
            MainSections = sections.Where(s => !s.IsAppendix).ToList();
            Appendices = sections.Where(s => s.IsAppendix).ToList();
        }

        /// <summary>Converts content blocks into what the view binds to. Static content
        /// passes straight through - the DataTemplates match the record types directly,
        /// so there's no wrapper view model for a paragraph. Only blocks that hold
        /// learner input get one.</summary>
        private static IReadOnlyList<object> BuildBlocks(ManualSection section)
        {
            var result = new List<object>();

            for (int i = 0; i < section.Blocks.Count; i++)
            {
                var block = section.Blocks[i];
                var blockKey = $"{section.Id}/{i}";

                switch (block)
                {
                    case ChecklistBlock checklist:
                        result.Add(new CheckableListViewModel(
                            checklist.Heading,
                            checklist.Items.Select((text, index) =>
                                new CheckableItemViewModel($"{blockKey}/{index}", text)).ToList(),
                            numbered: false));
                        break;

                    case StepsBlock steps:
                        result.Add(new CheckableListViewModel(
                            steps.Heading,
                            steps.Steps.Select((step, index) => new CheckableItemViewModel(
                                $"{blockKey}/{index}",
                                step.Text,
                                step.Observe,
                                steps.Numbered ? $"{index + 1}." : null)).ToList(),
                            numbered: steps.Numbered));
                        break;

                    case ValueTableBlock table:
                        result.Add(BuildTable(table));
                        break;

                    case QuestionsBlock questions:
                        result.Add(new QuestionListViewModel(
                            questions.Questions.Select((text, index) =>
                                new QuestionViewModel($"{questions.Id}/{index}", text)).ToList()));
                        break;

                    default:
                        result.Add(block);
                        break;
                }
            }

            return result;
        }

        private static ValueTableViewModel BuildTable(ValueTableBlock table)
        {
            var rows = new List<ValueRowViewModel>();

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var fixedCells = table.Rows[r];
                var editable = new List<ValueCellViewModel>();

                // Every column past the ones the content fills in is the learner's.
                for (int c = fixedCells.Count; c < table.Columns.Count; c++)
                    editable.Add(new ValueCellViewModel($"{table.Id}/{r}/{c}"));

                rows.Add(new ValueRowViewModel(fixedCells, editable));
            }

            return new ValueTableViewModel(table.Heading, table.Columns, rows);
        }
    }

    public class CheckableListViewModel
    {
        public string? Heading { get; }
        public IReadOnlyList<CheckableItemViewModel> Items { get; }
        public bool Numbered { get; }
        public bool HasHeading => Heading != null;

        public CheckableListViewModel(string? heading, IReadOnlyList<CheckableItemViewModel> items, bool numbered)
        {
            Heading = heading;
            Items = items;
            Numbered = numbered;
        }
    }

    public class ValueTableViewModel
    {
        public string? Heading { get; }
        public IReadOnlyList<string> Columns { get; }
        public IReadOnlyList<ValueRowViewModel> Rows { get; }
        public bool HasHeading => Heading != null;

        public ValueTableViewModel(string? heading, IReadOnlyList<string> columns, IReadOnlyList<ValueRowViewModel> rows)
        {
            Heading = heading;
            Columns = columns;
            Rows = rows;
        }
    }

    public class QuestionListViewModel
    {
        public IReadOnlyList<QuestionViewModel> Questions { get; }
        public QuestionListViewModel(IReadOnlyList<QuestionViewModel> questions) => Questions = questions;
    }
}
