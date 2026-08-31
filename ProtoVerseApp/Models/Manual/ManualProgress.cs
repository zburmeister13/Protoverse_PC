using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// A learner's work inside one module's manual: which steps they've ticked off,
    /// what they measured in a value table, and what they wrote against the reflection
    /// questions.
    ///
    /// DESIGNED NOW, NOT YET PERSISTED. The user asked (2026-08-31) that progress be
    /// retained per user, while noting it needn't be implemented in this spike. This
    /// type exists so the shape is settled and the seam is real - the renderer already
    /// reads and writes these values in memory, and turning that into saved progress is
    /// a matter of hanging one of these off each account's ModuleRecord and calling
    /// save. See EVALUATION.md for the effort estimate.
    ///
    /// WHERE IT BELONGS: `Models/UserAccount.ModuleRecord` already exists per account
    /// per module and is already persisted by `Services/AccountStore` to
    /// %AppData%\ProtoVerse\accounts.json. Adding `ManualProgress? ManualProgress` to
    /// that record is the whole storage story - no new file, no new lifecycle, and it
    /// inherits the account switching, the sign-out behaviour, and the
    /// degrade-to-nothing-on-IO-error handling that store already has.
    ///
    /// WHY KEYED BY STRING: every id here (<see cref="ManualSection.Id"/>,
    /// <see cref="ValueTableBlock.Id"/>, <see cref="QuestionsBlock.Id"/>) is authored
    /// in the manual content, not derived from list position, so inserting a step or
    /// reordering a section doesn't silently reattach someone's saved answer to a
    /// different question. That is the main reason the content model carries explicit
    /// ids at all.
    /// </summary>
    public class ManualProgress
    {
        /// <summary>Ticked checklist/step items, as "sectionId/blockIndex/itemIndex"
        /// keys. Position-based within a block on purpose: a step's *text* can be
        /// reworded without invalidating a tick, whereas inserting a step legitimately
        /// should invalidate the ones after it.</summary>
        [JsonPropertyName("checkedItems")]
        public List<string> CheckedItems { get; set; } = new();

        /// <summary>Cell contents for fillable value tables, keyed
        /// "tableId/row/column". Sparse - only cells the learner actually filled.</summary>
        [JsonPropertyName("tableCells")]
        public Dictionary<string, string> TableCells { get; set; } = new();

        /// <summary>Free-text answers to reflection questions, keyed
        /// "questionsBlockId/index".</summary>
        [JsonPropertyName("answers")]
        public Dictionary<string, string> Answers { get; set; } = new();

        /// <summary>Which sections the learner has revealed (the gated answer key).
        /// Remembered so a facilitator isn't re-gated every session.</summary>
        [JsonPropertyName("revealedSections")]
        public List<string> RevealedSections { get; set; } = new();

        /// <summary>Last section they were reading, to restore scroll position.</summary>
        [JsonPropertyName("lastSectionId")]
        public string? LastSectionId { get; set; }

        [JsonPropertyName("lastOpenedUtc")]
        public DateTimeOffset? LastOpenedUtc { get; set; }
    }
}
