using System.Collections.Generic;
using System.Linq;
using ProtoVerseApp.Models.Registry;
using Xunit;

namespace ProtoVerseApp.Tests
{
    public class MultiSlotResolverTests
    {
        private const int SlotCount = 3;

        private static RawSlotDetection Empty(int slot) => new(slot, null, null);
        private static RawSlotDetection Detected(int slot, ushort id, string revision = "A") => new(slot, id, revision);

        private static MultiSlotResolver ResolverWithSpans(params (ushort Id, int Span)[] modules)
        {
            var entries = modules
                .Select(m => new ProtoModRegistryEntry(
                    Id: m.Id, Revision: "A", Name: $"Module 0x{m.Id:X4}", CircuitCode: $"T{m.Id:X2}",
                    ManualReference: null, ControlScheme: "SomeViewModel", SlotSpan: m.Span))
                .ToList();
            return new MultiSlotResolver(new RevisionAwareModuleCatalog(entries), SlotCount);
        }

        [Fact]
        public void AllEmptySlots_ProducesNoOccupanciesOrConflicts()
        {
            var resolver = ResolverWithSpans();
            var result = resolver.Resolve(new[] { Empty(0), Empty(1), Empty(2) });

            Assert.Empty(result.Occupancies);
            Assert.Empty(result.Conflicts);
        }

        [Fact]
        public void SingleSlotModule_OccupiesOnlyItsOwnSlot()
        {
            var resolver = ResolverWithSpans((0x0001, 1));
            var result = resolver.Resolve(new[] { Detected(0, 0x0001), Empty(1), Empty(2) });

            var occupancy = Assert.Single(result.Occupancies);
            Assert.Equal(0, occupancy.AnchorSlot);
            Assert.Equal(new[] { 0 }, occupancy.OccupiedSlots);
            Assert.Empty(result.Conflicts);
        }

        [Fact]
        public void TwoSlotModule_AnchoredAtFirstSlot_OccupiesBothSlots()
        {
            var resolver = ResolverWithSpans((0x0002, 2));
            var result = resolver.Resolve(new[] { Detected(0, 0x0002), Empty(1), Empty(2) });

            var occupancy = Assert.Single(result.Occupancies);
            Assert.Equal(0, occupancy.AnchorSlot);
            Assert.Equal(new[] { 0, 1 }, occupancy.OccupiedSlots);
            Assert.Empty(result.Conflicts);
        }

        [Fact]
        public void TwoSlotModule_AnchoredAtMiddleSlot_OccupiesLastTwoSlots()
        {
            var resolver = ResolverWithSpans((0x0002, 2));
            var result = resolver.Resolve(new[] { Empty(0), Detected(1, 0x0002), Empty(2) });

            var occupancy = Assert.Single(result.Occupancies);
            Assert.Equal(1, occupancy.AnchorSlot);
            Assert.Equal(new[] { 1, 2 }, occupancy.OccupiedSlots);
            Assert.Empty(result.Conflicts);
        }

        [Fact]
        public void TwoSlotModule_AnchoredAtLastSlot_DoesNotFit_ProducesConflict()
        {
            // The user's explicit rule for this evaluation: a 2-slot module can only
            // ever identify from slot 1 or slot 2 (0-based 0 or 1) on a 3-slot
            // ProtoCore - never the last slot, since there is no slot above it.
            var resolver = ResolverWithSpans((0x0002, 2));
            var result = resolver.Resolve(new[] { Empty(0), Empty(1), Detected(2, 0x0002) });

            var conflict = Assert.Single(result.Conflicts);
            Assert.Equal(2, conflict.SlotIndex);

            // Still recorded as a (degraded, single-slot) occupancy - it did genuinely
            // identify itself from a real slot - rather than silently dropped.
            var occupancy = Assert.Single(result.Occupancies);
            Assert.Equal(new[] { 2 }, occupancy.OccupiedSlots);
        }

        [Fact]
        public void ThreeSlotModule_MustAnchorAtSlotZero_ToFit()
        {
            var resolver = ResolverWithSpans((0x0003, 3));
            var result = resolver.Resolve(new[] { Detected(0, 0x0003), Empty(1), Empty(2) });

            var occupancy = Assert.Single(result.Occupancies);
            Assert.Equal(new[] { 0, 1, 2 }, occupancy.OccupiedSlots);
            Assert.Empty(result.Conflicts);
        }

        [Fact]
        public void ThreeSlotModule_AnchoredAnywhereElse_DoesNotFit()
        {
            var resolver = ResolverWithSpans((0x0003, 3));
            var result = resolver.Resolve(new[] { Empty(0), Detected(1, 0x0003), Empty(2) });

            Assert.Single(result.Conflicts);
        }

        [Fact]
        public void SecondarySlot_ReportingItsOwnIdentification_IsAConflict()
        {
            // Covers both "hardware fault / partial seating" and "two independent
            // single-slot modules collide with a registered multi-slot module" from
            // the original task - both look identical from the handshake alone.
            var resolver = ResolverWithSpans((0x0002, 2), (0x0009, 1));
            var result = resolver.Resolve(new[] { Detected(0, 0x0002), Detected(1, 0x0009), Empty(2) });

            var conflict = Assert.Single(result.Conflicts);
            Assert.Equal(1, conflict.SlotIndex);

            // The anchor's occupancy is still reported (best-effort) alongside the
            // conflict, so a caller isn't left with nothing to show for slot 0.
            var occupancy = Assert.Single(result.Occupancies);
            Assert.Equal(0, occupancy.AnchorSlot);
        }

        [Fact]
        public void UnrecognizedId_IsTreatedAsSingleSlot_NeverGuessesASpan()
        {
            // No registry entry at all for this id - the resolver has no span data to
            // infer from, so it must not assume a multi-slot footprint just because a
            // neighboring slot happens to be empty.
            var resolver = ResolverWithSpans(); // empty registry
            var result = resolver.Resolve(new[] { Detected(0, 0xABCD), Empty(1), Empty(2) });

            var occupancy = Assert.Single(result.Occupancies);
            Assert.Equal(new[] { 0 }, occupancy.OccupiedSlots);
            Assert.Empty(result.Conflicts);
        }

        [Fact]
        public void TwoIndependentSingleSlotModules_ResolveWithoutInterference()
        {
            var resolver = ResolverWithSpans((0x0001, 1), (0x0003, 1));
            var result = resolver.Resolve(new[] { Detected(0, 0x0001), Empty(1), Detected(2, 0x0003) });

            Assert.Equal(2, result.Occupancies.Count);
            Assert.Empty(result.Conflicts);
        }
    }
}
