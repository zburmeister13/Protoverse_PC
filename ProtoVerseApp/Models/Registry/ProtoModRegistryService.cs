using System;
using System.IO;
using System.Linq;

namespace ProtoVerseApp.Models.Registry
{
    /// <summary>
    /// The one loaded, validated instance of ProtoModRegistry.json the rest of the
    /// app reads from - ModuleCatalog, ManualLibrary, and
    /// <see cref="ProtoModBoardCatalog"/> are now thin views over
    /// <see cref="Entries"/> rather than three independently hand-maintained
    /// dictionaries. That consolidation - not raw lookup
    /// performance, which was already proven fine at 1000-entry synthetic scale in
    /// EVALUATION.md - is the actual "single source of truth" this class exists for:
    /// those three catalogs disagreeing about one ProtoMod's identity is a real bug
    /// this app already shipped once (the AccelTemp/F02 circuit-code mixup - see
    /// CLAUDE.md), and three separate dictionaries can drift again the same way a
    /// fourth one never can from itself.
    ///
    /// Loaded once, lazily, on first access - not in a static constructor, so a
    /// missing/corrupt registry file fails loudly the first time something actually
    /// needs it rather than crashing the whole process (including code paths that
    /// don't touch ProtoMods at all) before Main even runs.
    /// </summary>
    public static class ProtoModRegistryService
    {
        /// <summary>Mirrors the private SlotCount constant in
        /// ViewModels/MainViewModel.cs - kept as its own constant here rather than a
        /// cross-reference, since Models must not depend on ViewModels. If
        /// ProtoCore's physical slot count ever changes, both constants change
        /// together - see CLAUDE.md.</summary>
        public const int SlotCount = 3;

        /// <summary>Every currently-shipped board reports this revision - see
        /// EVALUATION.md's "wire-protocol gap" finding: PresenceReport has no
        /// revision field at all yet, so nothing in this app can ask hardware what
        /// its real revision is. This is the assumed value at every live call site
        /// until that wire-protocol extension happens; replace it with the real
        /// parsed byte at that one point when it does, rather than threading a fake
        /// revision through the rest of the app.</summary>
        public const string DefaultRevision = "A";

        private static readonly Lazy<ProtoModRegistryDocument> LazyDocument = new(LoadAndValidate);
        private static readonly Lazy<RevisionAwareModuleCatalog> LazyCatalog =
            new(() => new RevisionAwareModuleCatalog(Entries));

        public static System.Collections.Generic.IReadOnlyList<ProtoModRegistryEntry> Entries => LazyDocument.Value.Entries;

        public static RevisionAwareModuleCatalog Catalog => LazyCatalog.Value;

        /// <summary>Convenience for the majority of today's call sites, which only
        /// ever ask "what does this ID mean" against real, currently-connected
        /// hardware - all of which reports <see cref="DefaultRevision"/> today. Once
        /// PresenceReport carries a real revision, callers that need revision-aware
        /// behavior should go through <see cref="Catalog"/> directly instead.</summary>
        public static ProtoModRegistryEntry? FindByIdAssumingDefaultRevision(ProtoModId id) =>
            Entries.FirstOrDefault(e => e.ProtoModId == id && e.Revision == DefaultRevision);

        private static ProtoModRegistryDocument LoadAndValidate()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Models", "Registry", "ProtoModRegistry.json");
            var document = ProtoModRegistryLoader.LoadFromFile(path);
            var errors = ProtoModRegistryValidator.Validate(document.Entries, SlotCount);

            if (errors.Count > 0)
            {
                // A malformed registry means "which panel for which board" is
                // undefined - loud and immediate beats quietly rendering the wrong
                // (or no) panel for whatever board happens to be plugged in.
                throw new InvalidOperationException(
                    $"{path} failed validation:\n" + string.Join("\n", errors));
            }

            return document;
        }
    }
}
