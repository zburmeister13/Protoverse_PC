using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ProtoVerseApp.Models.Registry;
using Xunit;

namespace ProtoVerseApp.Tests
{
    /// <summary>
    /// Synthetic-scale simulation per the evaluation task: none of these ids/names
    /// are real ProtoMods (real product data lives only in the checked-in
    /// ProtoModRegistry.json - see RegistryValidationTests) - this exists purely to
    /// prove the registry approach doesn't fall over at the 100s-1000+ scale the
    /// product is actually planning for.
    /// </summary>
    public class ScaleStressTests
    {
        private const int SlotCount = 3;
        private const int SyntheticEntryCount = 1000;

        /// <summary>Deterministic (seeded) synthetic entries - every (Id, Revision)
        /// pair is unique by construction (id N, revisions "A".."C" cycling so some
        /// ids get more than one revision, exercising the same shape as the real
        /// catalog eventually will), spans cycle 1/1/1/2/3 (mostly single-slot, like
        /// the real product, with enough multi-slot entries to be meaningful).</summary>
        private static List<ProtoModRegistryEntry> GenerateSyntheticEntries(int count)
        {
            var entries = new List<ProtoModRegistryEntry>(count);
            string[] revisions = { "A", "B", "C" };
            int[] spans = { 1, 1, 1, 2, 3 };

            for (int i = 0; i < count; i++)
            {
                // Start well above the real catalog's ids (0x0001-0x0004) and the
                // reserved block at the top (Unknown/Core/Broadcast), so a synthetic
                // run can never collide with a real ProtoModId even by accident.
                ushort id = (ushort)(0x1000 + i / revisions.Length);
                string revision = revisions[i % revisions.Length];
                int span = spans[i % spans.Length];

                entries.Add(new ProtoModRegistryEntry(
                    id, revision, $"Synthetic module 0x{id:X4} rev {revision}",
                    ManualReference: null, ControlScheme: "SyntheticViewModel", SlotSpan: span));
            }

            return entries;
        }

        [Fact]
        public void OneThousandSyntheticEntries_AreAllUnique()
        {
            var entries = GenerateSyntheticEntries(SyntheticEntryCount);
            var distinctIdentities = entries.Select(e => (e.Id, e.Revision)).Distinct().Count();

            Assert.Equal(SyntheticEntryCount, entries.Count);
            Assert.Equal(SyntheticEntryCount, distinctIdentities); // sanity-check the generator itself
        }

        [Fact]
        public void OneThousandSyntheticEntries_PassValidationCleanly()
        {
            var entries = GenerateSyntheticEntries(SyntheticEntryCount);
            var errors = ProtoModRegistryValidator.Validate(entries, SlotCount);

            Assert.Empty(errors);
        }

        [Fact]
        public void Validator_CatchesInjectedDuplicatesAtScale()
        {
            var entries = GenerateSyntheticEntries(SyntheticEntryCount);

            // Deliberately corrupt 3 entries with duplicates of 3 others, mixed in
            // among 1000 otherwise-clean entries - proving the validator finds a
            // handful of real problems rather than just reporting "some problems
            // exist somewhere in this haystack."
            var corrupted = new List<ProtoModRegistryEntry>(entries)
            {
                entries[10], entries[500], entries[999]
            };

            var errors = ProtoModRegistryValidator.Validate(corrupted, SlotCount);

            Assert.Equal(3, errors.Count);
            Assert.All(errors, e => Assert.Contains("Duplicate entry", e));
        }

        [Fact]
        public void Validator_CatchesInjectedSlotSpanViolationAtScale()
        {
            var entries = GenerateSyntheticEntries(SyntheticEntryCount);
            var bad = entries[42] with { SlotSpan = 7 }; // more slots than any real or planned ProtoCore has
            var corrupted = new List<ProtoModRegistryEntry>(entries) { [42] = bad };

            var errors = ProtoModRegistryValidator.Validate(corrupted, SlotCount);

            Assert.Contains(errors, e => e.Contains("slot span 7"));
        }

        [Fact]
        public void Validator_CatchesDanglingCompatibilityOverrideAtScale()
        {
            var entries = GenerateSyntheticEntries(SyntheticEntryCount);
            var bad = entries[7] with { CompatibleWithRevision = "Z" }; // no such revision exists for this id
            var corrupted = new List<ProtoModRegistryEntry>(entries) { [7] = bad };

            var errors = ProtoModRegistryValidator.Validate(corrupted, SlotCount);

            Assert.Contains(errors, e => e.Contains("no entry for id"));
        }

        [Fact]
        public void OneThousandEntryCatalog_BuildsAndLooksUpFast()
        {
            var entries = GenerateSyntheticEntries(SyntheticEntryCount);

            var stopwatch = Stopwatch.StartNew();
            var catalog = new RevisionAwareModuleCatalog(entries);

            foreach (var entry in entries)
                catalog.Resolve(entry.Id, entry.Revision);

            // Also probe a few misses, since a dictionary miss can be a different
            // (sometimes slower) code path than a hit.
            catalog.Resolve(0xDEAD, "A");
            catalog.Resolve(entries[0].Id, "not-a-real-revision");

            stopwatch.Stop();

            // 1000 entries plus ~1000 lookups is trivial for a Dictionary - this
            // threshold is generous on purpose (protecting against an accidental
            // O(n^2) mistake, e.g. a List.Find where a Dictionary was intended, not
            // benchmarking exact performance).
            Assert.True(stopwatch.ElapsedMilliseconds < 1000,
                $"Building + looking up a {SyntheticEntryCount}-entry catalog took {stopwatch.ElapsedMilliseconds}ms - " +
                "expected near-instant for a dictionary-backed lookup at this scale.");
        }

        [Fact]
        public void MultiSlotResolver_HandlesSyntheticMultiSlotEntriesAtScale()
        {
            var entries = GenerateSyntheticEntries(SyntheticEntryCount);
            var catalog = new RevisionAwareModuleCatalog(entries);
            var resolver = new MultiSlotResolver(catalog, SlotCount);

            // A 2-slot synthetic entry (span cycle index 3 -> spans[3] == 2) anchored
            // at slot 0, nothing else detected.
            var twoSlotEntry = entries.First(e => e.SlotSpan == 2);
            var result = resolver.Resolve(new[]
            {
                new RawSlotDetection(0, twoSlotEntry.Id, twoSlotEntry.Revision),
                new RawSlotDetection(1, null, null),
                new RawSlotDetection(2, null, null),
            });

            var occupancy = Assert.Single(result.Occupancies);
            Assert.Equal(new[] { 0, 1 }, occupancy.OccupiedSlots);
            Assert.Empty(result.Conflicts);
        }
    }
}
