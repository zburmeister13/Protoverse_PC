using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

    /// <summary>How one multiple-choice option should read once the question has been
    /// answered. Deliberately one enum rather than three bools, so the XAML can pick a
    /// treatment with a single DataTrigger instead of a stack of MultiDataTriggers.</summary>
    public enum ChoiceState
    {
        /// <summary>Unanswered, or answered and this option was neither picked nor
        /// correct.</summary>
        Idle,

        /// <summary>The correct answer, once revealed.</summary>
        Right,

        /// <summary>What the learner picked, when it wasn't the correct answer.</summary>
        Wrong
    }

    /// <summary>One selectable answer. Holds its own command so the XAML binds
    /// <c>Command="{Binding SelectCommand}"</c> with no RelativeSource walk back up to
    /// the question.</summary>
    public partial class ChoiceOptionViewModel : ObservableObject
    {
        private readonly ChoiceQuestionViewModel _question;

        public string Text { get; }
        public string Letter { get; }
        public bool IsCorrect { get; }

        [ObservableProperty]
        private ChoiceState _state = ChoiceState.Idle;

        public ChoiceOptionViewModel(ChoiceQuestionViewModel question, string text, string letter, bool isCorrect)
        {
            _question = question;
            Text = text;
            Letter = letter;
            IsCorrect = isCorrect;
        }

        [RelayCommand]
        private void Select() => _question.Answer(this);
    }

    /// <summary>One multiple-choice question and its marking state.</summary>
    public partial class ChoiceQuestionViewModel : ObservableObject
    {
        public string Key { get; }
        public string Text { get; }
        public string Explanation { get; }
        public IReadOnlyList<ChoiceOptionViewModel> Options { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ResultText))]
        private bool _isAnswered;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ResultText))]
        private bool _answeredCorrectly;

        public string ResultText => !IsAnswered
            ? ""
            : AnsweredCorrectly ? "Correct" : "Not quite";

        public ChoiceQuestionViewModel(string key, ManualChoiceQuestion question)
        {
            Key = key;
            Text = question.Text;
            Explanation = question.Explanation;

            var options = new List<ChoiceOptionViewModel>();
            for (int i = 0; i < question.Options.Count; i++)
            {
                options.Add(new ChoiceOptionViewModel(
                    this,
                    question.Options[i],
                    // A, B, C... - a label to refer to out loud, which matters when
                    // someone is being helped through a module by a person beside them.
                    ((char)('A' + i)).ToString(),
                    isCorrect: i == question.CorrectIndex));
            }

            Options = options;
        }

        /// <summary>Marks the question. First answer stands: the point is to find out
        /// what the learner thought, and letting them click on until it goes green
        /// would turn the question into a lock to pick.</summary>
        public void Answer(ChoiceOptionViewModel chosen)
        {
            if (IsAnswered)
                return;

            IsAnswered = true;
            AnsweredCorrectly = chosen.IsCorrect;

            foreach (var option in Options)
            {
                // The correct answer always shows, right or wrong. A learner who
                // guessed wrong needs to see which one it was, next to why.
                option.State = option.IsCorrect ? ChoiceState.Right
                    : option == chosen ? ChoiceState.Wrong
                    : ChoiceState.Idle;
            }
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

        /// <summary>Full path to this module's schematic PDF beside the executable, or
        /// null if the manual declares none or the file didn't ship.</summary>
        public string? SchematicPath
        {
            get
            {
                if (Document.SchematicFile == null)
                    return null;

                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Schematics", Document.SchematicFile);
                return File.Exists(path) ? path : null;
            }
        }

        public bool HasSchematic => SchematicPath != null;

        /// <summary>Opens the schematic in whatever the system uses for PDFs. The app
        /// deliberately doesn't render the PDF itself: a schematic is something a
        /// learner wants to zoom, pan and keep open beside the app, which a dedicated
        /// viewer already does far better than an embedded control would.</summary>
        [RelayCommand]
        private void OpenSchematic()
        {
            var path = SchematicPath;
            if (path == null)
                return;

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
            {
                // No PDF handler registered, or the shell refused. Not worth a crash
                // dialog over a reference document - the path is shown in the tooltip,
                // so the learner can still open it by hand.
                SchematicError = "Couldn't open the schematic - no PDF viewer is associated with .pdf files.";
            }
        }

        [ObservableProperty]
        private string? _schematicError;

        public string PlaceholderWarning =>
            $"{Document.PlaceholderCount} section(s) of this manual haven't been written yet and show as placeholders.";

        public bool HasNeedsReview => Document.NeedsReviewCount > 0;

        public string NeedsReviewWarning =>
            $"{Document.NeedsReviewCount} passage(s) below were written for the app and aren't in this module's source manual. " +
            "They're marked in place and need review before this ships.";

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

                    case MultipleChoiceBlock choices:
                        result.Add(new MultipleChoiceViewModel(
                            choices.Heading,
                            choices.Questions.Select((question, index) =>
                                new ChoiceQuestionViewModel($"{choices.Id}/{index}", question)).ToList()));
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

    public class MultipleChoiceViewModel
    {
        public string? Heading { get; }
        public IReadOnlyList<ChoiceQuestionViewModel> Questions { get; }
        public bool HasHeading => Heading != null;

        public MultipleChoiceViewModel(string? heading, IReadOnlyList<ChoiceQuestionViewModel> questions)
        {
            Heading = heading;
            Questions = questions;
        }
    }
}
