using System;
using System.Collections.Generic;
using System.IO;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// The content model for an in-app ProtoMod manual. Mirrors
    /// `PROTOVERSE/Manuals/Gen2/ProtoVerse_ProtoMod_Manual_Template.docx` section for
    /// section, so every module's manual has the same predictable shape.
    ///
    /// Deliberately a data model with no UI in it: the renderer
    /// (`Views/ManualView.xaml`) is one set of DataTemplates keyed by block type, so
    /// adding a module means adding data, not XAML. That's the whole point of the
    /// evaluation - see EVALUATION.md for what this implies about per-manual cost.
    ///
    /// A block type exists for each *repeating pattern* in the template rather than
    /// for each visual arrangement, which is why callouts and value tables are
    /// first-class here instead of being paragraphs with formatting.
    /// </summary>
    public abstract record ManualBlock;

    /// <summary>Body text.</summary>
    public record ParagraphBlock(string Text) : ManualBlock;

    /// <summary>A plain bullet list ("Key takeaways", "What you should see").</summary>
    public record BulletsBlock(IReadOnlyList<string> Items, string? Heading = null) : ManualBlock;

    /// <summary>A minor heading inside a section (the template's "one subsection per
    /// concept" in Background &amp; Theory).</summary>
    public record SubheadingBlock(string Text) : ManualBlock;

    public enum CalloutKind
    {
        /// <summary>Formula, spec or fact set apart from body text.</summary>
        TechNote,

        /// <summary>Follows a non-obvious step in "Now try this".</summary>
        Observe,

        /// <summary>The Creative Challenge's "no single correct answer" reassurance.</summary>
        Reassurance,

        /// <summary>Marks content that doesn't exist yet. Distinct from the others so
        /// it can be styled as obviously-not-real and counted programmatically - see
        /// <see cref="ManualDocument.PlaceholderCount"/>.</summary>
        Placeholder,

        /// <summary>Marks content that was written for the app and has no source
        /// document behind it, and so has not been signed off. Distinct from
        /// <see cref="Placeholder"/>, which means nothing is there at all: this is real,
        /// finished-looking content, which is exactly why it needs to announce itself -
        /// an unreviewed paragraph is indistinguishable from a sourced one once it is
        /// rendered. Counted by <see cref="ManualDocument.NeedsReviewCount"/>.</summary>
        NeedsReview,

        /// <summary>Marks a place where the manual and the hardware (or this app)
        /// disagree. Not authored content - an app-side note, always attributed as
        /// such. Exists because silently rendering a manual that describes a different
        /// board than the one plugged in would be the worst possible outcome of moving
        /// manuals in-app: the learner would trust it precisely because it appeared
        /// next to live hardware state.</summary>
        Discrepancy
    }

    /// <summary>One of the template's single-row callout tables.</summary>
    public record CalloutBlock(CalloutKind Kind, string Text, string? Title = null) : ManualBlock;

    /// <summary>A figure the manual references. No manual in this project has real
    /// photos yet - both the template and every filled manual carry literal
    /// "[ figure / photo ]" markers - so this always renders as a placeholder frame
    /// with its caption. For a real picture, see <see cref="ImageBlock"/>.</summary>
    public record FigureBlock(string Caption) : ManualBlock;

    /// <summary>A real image shipped with the app, from `Assets/Schematics/`.
    /// Currently that means the cropped circuit rasters built by
    /// `tools/build_schematics.ps1`.</summary>
    public record ImageBlock(string FileName, string? Caption = null) : ManualBlock
    {
        public string FullPath =>
            Path.Combine(AppContext.BaseDirectory, "Assets", "Schematics", FileName);

        /// <summary>False if the asset didn't ship - the renderer then shows nothing
        /// rather than a broken-image box.</summary>
        public bool Exists => System.IO.File.Exists(FullPath);
    }

    /// <summary>The "You'll need" checklist. Checkable in the UI; see
    /// <see cref="ManualProgress"/> for how that state would persist.</summary>
    public record ChecklistBlock(IReadOnlyList<string> Items, string? Heading = null) : ManualBlock;

    /// <summary>One guided step, optionally followed by an "Observe" prompt - the
    /// template pairs these, so they're modeled together rather than as two
    /// independent blocks that happen to be adjacent.</summary>
    public record ManualStep(string Text, string? Observe = null);

    /// <summary>"Assembly steps" or "Now try this". <paramref name="Numbered"/>
    /// distinguishes ordered procedure from a menu of things to try.</summary>
    public record StepsBlock(IReadOnlyList<ManualStep> Steps, bool Numbered = true, string? Heading = null) : ManualBlock;

    /// <summary>The template's fillable value table (value tested / predicted /
    /// observed / notes). <paramref name="Columns"/> is the header row;
    /// <paramref name="Rows"/> gives each row's fixed leading cells, with the
    /// remaining columns left for the learner to fill in.
    ///
    /// This is the block the evaluation is really about: on paper it's a table you
    /// print and write on, in-app it can capture what the learner actually measured.
    /// <paramref name="Id"/> exists so those values can be keyed and persisted.</summary>
    public record ValueTableBlock(
        string Id,
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<string>> Rows,
        string? Heading = null) : ManualBlock;

    /// <summary>Follow-up &amp; reflection questions. Rendered with a free-text answer
    /// field per question; <paramref name="Id"/> keys those answers for persistence.
    /// Answers never appear here - they live in a gated answer-key appendix.
    ///
    /// Kept for manuals whose questions genuinely have no checkable answer. Where an
    /// answer can be checked, prefer <see cref="MultipleChoiceBlock"/>: free text puts
    /// the burden of marking on the learner, who is the person least able to do
    /// it.</summary>
    public record QuestionsBlock(string Id, IReadOnlyList<string> Questions) : ManualBlock;

    /// <summary>One multiple-choice question. <paramref name="CorrectIndex"/> indexes
    /// <paramref name="Options"/>; <paramref name="Explanation"/> is shown once the
    /// learner has answered, whether they got it right or not - being told you are
    /// wrong without being told why is the least useful possible feedback.</summary>
    public record ManualChoiceQuestion(
        string Text,
        IReadOnlyList<string> Options,
        int CorrectIndex,
        string Explanation);

    /// <summary>Checkable follow-up questions.
    ///
    /// This is the block that makes an in-app manual do something a printed one can't:
    /// the answer is known to the app, so the learner gets it marked the instant they
    /// choose, with the reasoning attached. That also removes the need for a separate
    /// answer-key appendix - one place for an answer instead of two that can drift
    /// apart.
    ///
    /// <paramref name="Id"/> keys each answer for persistence (see
    /// <see cref="ManualProgress"/>).</summary>
    public record MultipleChoiceBlock(
        string Id,
        IReadOnlyList<ManualChoiceQuestion> Questions,
        string? Heading = null) : ManualBlock;

    /// <summary>
    /// One numbered section of the manual.
    /// </summary>
    /// <param name="Id">Stable key for TOC jump and for progress records. Must not
    /// change once a manual ships, or saved progress stops lining up.</param>
    /// <param name="IsAppendix">Appendices are separated from the learner flow in the
    /// UI rather than just being sections 10-12.</param>
    /// <param name="IsSpoiler">Hidden behind an explicit reveal. The template is
    /// emphatic that nothing spoiling an answer may appear before the follow-up
    /// questions, so the answer key opts into this.</param>
    public record ManualSection(
        string Id,
        string Title,
        IReadOnlyList<ManualBlock> Blocks,
        bool IsAppendix = false,
        bool IsSpoiler = false);

    /// <summary>The header block: series, code, name, one-liner, and the
    /// difficulty/time/prerequisites metadata row. Any of the metadata may be null
    /// where the source manual doesn't state it.</summary>
    public record ManualHeader(
        string Series,
        string Code,
        string Name,
        string Tagline,
        string? Difficulty,
        string? Time,
        string? Prerequisites);

    /// <summary>
    /// A complete in-app manual.
    /// </summary>
    /// <param name="SourceNote">Where this manual's content came from, shown in the
    /// UI. Non-optional on purpose: this project does not ship module content that
    /// can't say where it came from, and a manual assembled partly from placeholders
    /// has to admit it.</param>
    /// <param name="SchematicFile">File name of this module's schematic PDF under
    /// `Assets/Schematics/`, or null if none has been exported. Linked from the top of
    /// the manual rather than buried in an appendix - it's the reference a learner
    /// reaches for mid-task, not something read once in order.</param>
    public record ManualDocument(
        string ModuleCode,
        ManualHeader Header,
        IReadOnlyList<ManualSection> Sections,
        string SourceNote,
        string? SchematicFile = null)
    {
        /// <summary>How many placeholder callouts this manual contains - i.e. how much
        /// of it is scaffolding rather than written content. Surfaced in the UI so a
        /// half-written manual is never mistaken for a finished one.</summary>
        public int PlaceholderCount => CountCallouts(CalloutKind.Placeholder);

        /// <summary>How many passages this manual contains that were written for the app
        /// with no source document behind them and have not been signed off. Surfaced in
        /// the UI for the same reason as <see cref="PlaceholderCount"/>: unsourced
        /// content that looks finished is the failure this project's no-fabrication rule
        /// exists to prevent, and it can only be caught if it says so itself.</summary>
        public int NeedsReviewCount => CountCallouts(CalloutKind.NeedsReview);

        private int CountCallouts(CalloutKind kind)
        {
            int count = 0;
            foreach (var section in Sections)
                foreach (var block in section.Blocks)
                    if (block is CalloutBlock callout && callout.Kind == kind)
                        count++;
            return count;
        }
    }
}
