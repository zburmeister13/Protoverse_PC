using System.Collections.Generic;

namespace ProtoVerseApp.Models.Registry
{
    /// <summary>Raw, per-slot identification exactly as ProtoCore would report it
    /// today (a bare (Id, Revision), or nothing) - before any multi-slot inference is
    /// applied. <see cref="SlotIndex"/> is 0-based.</summary>
    public record RawSlotDetection(int SlotIndex, ushort? Id, string? Revision)
    {
        public bool IsEmpty => Id is null;
    }

    /// <summary>One physically-resolved ProtoMod, after multi-slot inference: which
    /// slot it identified from (<see cref="AnchorSlot"/>, always the lowest-numbered
    /// occupied one - see <see cref="MultiSlotResolver"/>'s doc comment for why), and
    /// every physical slot it actually occupies.</summary>
    public record ResolvedModuleOccupancy(int AnchorSlot, IReadOnlyList<int> OccupiedSlots, ushort Id, string Revision);

    /// <summary>A slot layout problem the resolver could detect but not silently paper
    /// over - see the two <see cref="MultiSlotResolver.Resolve"/> cases that produce
    /// these.</summary>
    public record SlotConflict(int SlotIndex, string Reason);

    public record MultiSlotResolution(
        IReadOnlyList<ResolvedModuleOccupancy> Occupancies,
        IReadOnlyList<SlotConflict> Conflicts);

    /// <summary>
    /// Infers multi-slot ProtoMod occupancy from independent per-slot identification,
    /// per the user's explicit hardware rule for this evaluation (2026-09-07): a
    /// multi-slot ProtoMod identifies itself from its lowest-numbered occupied slot
    /// (the "communication slot" - a single physical interface, keyed by hardware
    /// orientation so there is no ambiguity about which end is slot 1), and every
    /// additional slot it occupies is the next slot(s) upward
    /// (<c>commSlot + 1</c>, <c>+ 2</c>, ...) - never downward, never non-contiguous.
    /// This resolves the generic "which slot reports identification" question the
    /// original evaluation task posed as open: it isn't inferred per module or left to
    /// physical keying variation, it is always "the lowest slot, extending up."
    ///
    /// A useful consequence of that fixed rule: the original task listed three
    /// separate multi-slot edge cases to handle. With "anchor is always the lowest
    /// slot" settled, two of them collapse into one detectable condition here - a
    /// secondary slot unexpectedly reporting its own identification looks identical
    /// to the wire whether that identification came from hardware fault, partial
    /// seating, or two genuinely independent single-slot modules occupying slots the
    /// registry says belong to one multi-slot module. All three are surfaced as the
    /// same <see cref="SlotConflict"/>, because the resolver has no way to
    /// distinguish their causes from the handshake data alone - see EVALUATION.md for
    /// why this is a real limitation, not a gap in this implementation.
    /// </summary>
    public class MultiSlotResolver
    {
        private readonly RevisionAwareModuleCatalog _catalog;
        private readonly int _slotCount;

        public MultiSlotResolver(RevisionAwareModuleCatalog catalog, int slotCount)
        {
            _catalog = catalog;
            _slotCount = slotCount;
        }

        public MultiSlotResolution Resolve(IReadOnlyList<RawSlotDetection> detections)
        {
            var claimedBy = new int?[_slotCount];
            var occupancies = new List<ResolvedModuleOccupancy>();
            var conflicts = new List<SlotConflict>();

            for (int slot = 0; slot < _slotCount; slot++)
            {
                if (claimedBy[slot] != null)
                    continue; // already folded into an earlier anchor's span

                var detection = detections[slot];
                if (detection.IsEmpty)
                    continue;

                // An id this registry has never heard of at all - not even at another
                // revision - has no span data to infer from. Treated as span 1 rather
                // than guessed at: inferring a multi-slot footprint for a module this
                // registry cannot otherwise identify would be exactly the kind of
                // unearned assumption the "revision defaults to different module" rule
                // exists to prevent one level up.
                var lookup = _catalog.Resolve(detection.Id!.Value, detection.Revision!);
                int span = lookup.Kind == RegistryLookupKind.Found ? lookup.Entry!.SlotSpan : 1;

                int lastSlotNeeded = slot + span - 1;
                if (lastSlotNeeded > _slotCount - 1)
                {
                    // Declared span doesn't fit at the position it identified from -
                    // e.g. a 2-slot module anchored at the last physical slot. Recorded
                    // as its own single-slot occupancy (best-effort: it did identify
                    // itself, from a real slot) rather than dropped, but the conflict is
                    // what a caller should act on - this module cannot work correctly as
                    // seated.
                    conflicts.Add(new SlotConflict(slot,
                        $"Declared slot span {span} does not fit anchored at slot {slot + 1} " +
                        $"on a {_slotCount}-slot ProtoCore."));
                    claimedBy[slot] = occupancies.Count;
                    occupancies.Add(new ResolvedModuleOccupancy(slot, new[] { slot }, detection.Id.Value, detection.Revision!));
                    continue;
                }

                var occupiedSlots = new List<int>(span);
                for (int s = slot; s <= lastSlotNeeded; s++)
                    occupiedSlots.Add(s);

                // A secondary slot should be silent - it's physically part of the same
                // board as the anchor, wired to the same identification interface. Any
                // identification data of its own (whether from a hardware fault, a
                // partially-seated board, or two real independent boards colliding with
                // the registry's multi-slot claim - see the class doc comment) is a
                // conflict the resolver surfaces rather than silently overwrites.
                for (int i = 1; i < occupiedSlots.Count; i++)
                {
                    int secondarySlot = occupiedSlots[i];
                    if (!detections[secondarySlot].IsEmpty)
                    {
                        conflicts.Add(new SlotConflict(secondarySlot,
                            $"Secondary slot of module anchored at slot {slot + 1} unexpectedly reports its " +
                            $"own identification (id 0x{detections[secondarySlot].Id:X4}) instead of staying silent."));
                    }
                }

                foreach (var s in occupiedSlots)
                    claimedBy[s] = occupancies.Count;
                occupancies.Add(new ResolvedModuleOccupancy(slot, occupiedSlots, detection.Id.Value, detection.Revision!));
            }

            return new MultiSlotResolution(occupancies, conflicts);
        }
    }
}
