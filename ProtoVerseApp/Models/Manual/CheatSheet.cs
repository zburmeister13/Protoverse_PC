using System.Collections.Generic;
using System.Linq;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>One formula or spec worth remembering, lifted from a Tech note.</summary>
    public record CheatSheetNote(string? Title, string Text);

    /// <summary>A group of key takeaways, with the section they came from.</summary>
    public record CheatSheetTakeaways(string SectionTitle, IReadOnlyList<string> Items);

    /// <summary>
    /// A one-page reference for a ProtoMod: just the formulas and the takeaways, for
    /// recall after the full manual has already been worked through.
    ///
    /// Deliberately DERIVED from the manual rather than authored separately. A
    /// hand-written second copy of the same facts is a copy that drifts - and in this
    /// project a drifting copy of a technical claim is exactly the failure the
    /// no-fabrication rule exists to prevent. If a manual's tech note is corrected, the
    /// cheat sheet is corrected with it, for free.
    ///
    /// The trade is that a manual gets a good cheat sheet by writing good Tech note
    /// callouts and Key takeaways bullets, not by writing a cheat sheet. That seems
    /// right: those are the parts worth remembering by definition.
    /// </summary>
    public class CheatSheet
    {
        public string ModuleCode { get; }
        public string ModuleName { get; }
        public string Tagline { get; }
        public IReadOnlyList<CheatSheetNote> Notes { get; }
        public IReadOnlyList<CheatSheetTakeaways> Takeaways { get; }

        /// <summary>True when the manual carries nothing worth putting on a cheat
        /// sheet. Shown as an explicit message rather than an empty window.</summary>
        public bool IsEmpty => Notes.Count == 0 && Takeaways.Count == 0;

        private CheatSheet(ManualDocument document)
        {
            ModuleCode = document.ModuleCode;
            ModuleName = document.Header.Name;
            Tagline = document.Header.Tagline;

            var notes = new List<CheatSheetNote>();
            var takeaways = new List<CheatSheetTakeaways>();

            foreach (var section in document.Sections)
            {
                // Appendices are facilitator material and answer keys - not recall
                // material for the person holding the board.
                if (section.IsAppendix)
                    continue;

                foreach (var block in section.Blocks)
                {
                    switch (block)
                    {
                        // Only TechNote. Observe prompts are questions, Placeholder and
                        // NeedsReview are notes to whoever maintains the content, and
                        // Discrepancy is a warning - none of them are things to recall.
                        case CalloutBlock { Kind: CalloutKind.TechNote } callout:
                            notes.Add(new CheatSheetNote(callout.Title, callout.Text));
                            break;

                        case BulletsBlock bullets when IsKeyTakeaways(bullets.Heading):
                            takeaways.Add(new CheatSheetTakeaways(section.Title, bullets.Items));
                            break;
                    }
                }
            }

            Notes = notes;
            Takeaways = takeaways;
        }

        /// <summary>Matched on the heading rather than a dedicated block type, because
        /// "Key takeaways" is already the established convention across every manual
        /// and adding a block type would mean rewriting all of them to gain nothing.
        /// Case- and punctuation-tolerant so a manual saying "Key Takeaways" still
        /// contributes.</summary>
        private static bool IsKeyTakeaways(string? heading) =>
            heading != null && heading.Replace(" ", "").StartsWith("keytakeaway",
                System.StringComparison.OrdinalIgnoreCase);

        public static CheatSheet Build(ManualDocument document) => new(document);
    }
}
