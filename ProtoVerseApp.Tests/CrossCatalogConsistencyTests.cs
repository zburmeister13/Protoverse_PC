using ProtoVerseApp.Models;
using ProtoVerseApp.Models.Registry;
using Xunit;

namespace ProtoVerseApp.Tests
{
    /// <summary>
    /// ProtoModLibraryCatalog is deliberately NOT folded into the identity registry -
    /// it carries rich, sourced editorial content (descriptions, project ideas,
    /// progression links, each with its own citation) that has nothing to do with
    /// "what wire ID maps to what control scheme," and conflating the two would mean
    /// mangling carefully-cited prose to fit a data-only schema. It also covers real
    /// boards that don't have a ProtoModId yet at all (A01, F00) - a case the
    /// registry, keyed on a wire-level ID, cannot represent by definition.
    ///
    /// What it CAN and must do is agree with the registry wherever the two overlap:
    /// an entry that claims a <see cref="ProtoModCatalogEntry.ProtocolId"/> is
    /// claiming to be the same physical board the registry describes for that ID,
    /// and if their circuit codes disagree, one of the two catalogs is simply wrong
    /// about which board it means - exactly the class of bug the AccelTemp/F02
    /// mixup was (see CLAUDE.md). This test is the guardrail for that, without
    /// requiring the two catalogs' data to be unified.
    /// </summary>
    public class CrossCatalogConsistencyTests
    {
        [Fact]
        public void EveryLibraryEntryWithAProtocolId_HasAMatchingRegistryEntry()
        {
            foreach (var libraryEntry in ProtoModLibraryCatalog.Entries)
            {
                if (libraryEntry.ProtocolId is not { } protocolId)
                    continue; // A01/F00-shaped entries: real hardware, no wire ID yet - nothing to cross-check.

                var registryEntry = ProtoModRegistryService.FindByIdAssumingDefaultRevision(protocolId);

                Assert.True(registryEntry != null,
                    $"Library entry \"{libraryEntry.Code}\" ({libraryEntry.Name}) claims ProtocolId " +
                    $"{protocolId}, but the registry has no entry for that id.");

                Assert.True(registryEntry!.CircuitCode == libraryEntry.Code,
                    $"Library entry claims circuit code \"{libraryEntry.Code}\" for {protocolId}, " +
                    $"but the registry says that id's circuit code is \"{registryEntry.CircuitCode}\" - " +
                    "these must agree; see the AccelTemp/F02 mixup this check exists to catch again.");
            }
        }

        [Fact]
        public void EveryRegistryEntry_HasAMatchingLibraryEntry()
        {
            // The reverse direction: a ProtoMod the app can identify and (maybe)
            // control but that the Library has never heard of would be a real,
            // wire-identifiable board invisible on the one screen meant to show the
            // whole catalog. Every currently-shipped id should have a Library entry
            // even if that entry's own content is still mostly "coming soon" nulls.
            foreach (var registryEntry in ProtoModRegistryService.Entries)
            {
                var libraryEntry = ProtoModLibraryCatalog.FindByCode(registryEntry.CircuitCode);

                Assert.True(libraryEntry != null,
                    $"Registry entry for id {registryEntry.ProtoModId} (circuit code " +
                    $"\"{registryEntry.CircuitCode}\") has no corresponding ProtoModLibraryCatalog entry.");

                Assert.True(libraryEntry!.ProtocolId == registryEntry.ProtoModId,
                    $"Library entry \"{registryEntry.CircuitCode}\" exists but its ProtocolId " +
                    $"({libraryEntry.ProtocolId}) doesn't match the registry's ({registryEntry.ProtoModId}).");
            }
        }
    }
}
