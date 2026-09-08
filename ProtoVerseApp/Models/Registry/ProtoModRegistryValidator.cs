using System.Collections.Generic;
using System.Linq;

namespace ProtoVerseApp.Models.Registry
{
    /// <summary>
    /// Static analysis over a registry's entries - no I/O, no ProtoCore, so it runs
    /// identically whether it's checking the real 4-entry registry, a hand-written
    /// bad-data test fixture, or a 1000-entry synthetic stress set. Intended as both
    /// a unit-test assertion (see ProtoVerseApp.Tests) and, per the evaluation task,
    /// a CI check: a registry with any of these problems should fail a build before
    /// it ever reaches ModuleCatalog.
    ///
    /// Deliberately conservative: every check here is something that would silently
    /// produce wrong behavior later (the wrong panel for a board, a module that can
    /// never load, a physically-impossible slot layout) rather than a style
    /// preference - a registry that never triggers any of these has no more findings
    /// to fix, not "no findings this pass caught."
    /// </summary>
    public static class ProtoModRegistryValidator
    {
        public static IReadOnlyList<string> Validate(IReadOnlyList<ProtoModRegistryEntry> entries, int slotCount)
        {
            var errors = new List<string>();

            // Duplicate (Id, Revision) pairs: the registry's whole reason to exist is
            // being the one place that maps an identity to one unambiguous entry - two
            // entries claiming the same identity means TryCreate's answer depends on
            // dictionary insertion order, which is exactly the kind of bug that works
            // in testing and breaks in the field.
            var duplicates = entries
                .GroupBy(e => (e.Id, e.Revision))
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var (id, revision) in duplicates)
                errors.Add($"Duplicate entry for id 0x{id:X4} revision \"{revision}\".");

            foreach (var entry in entries)
            {
                var label = $"id 0x{entry.Id:X4} revision \"{entry.Revision}\"";

                // An id with no revision can't be looked up by (Id, Revision) at all -
                // it would need a separate "revision-less" code path, which is exactly
                // the ambiguity this registry exists to remove. Every board reports
                // *some* revision string, even if every board today reports the same
                // one ("A") - see the hard constraint that revision defaults to
                // "different module," which only makes sense if revision is never
                // absent.
                if (string.IsNullOrWhiteSpace(entry.Revision))
                    errors.Add($"Entry for id 0x{entry.Id:X4} has no revision.");

                if (string.IsNullOrWhiteSpace(entry.Name))
                    errors.Add($"Entry for {label} has no name.");

                // The circuit code is the one identity fact a person can independently
                // verify against a physical board (it's printed on the silkscreen) -
                // this is the field that drifted out of sync between catalogs before
                // this registry consolidated them (the AccelTemp/F02 mixup - see
                // CLAUDE.md), so an entry claiming to be real hardware without one is
                // rejected outright rather than left to drift again.
                if (string.IsNullOrWhiteSpace(entry.CircuitCode))
                    errors.Add($"Entry for {label} has no circuit code.");

                if (string.IsNullOrWhiteSpace(entry.ControlScheme))
                    errors.Add($"Entry for {label} has no control scheme.");

                // Slot span must be a physically real footprint on the ProtoCore this
                // registry targets - 1 for the ordinary case, up to slotCount for a
                // module spanning every slot. Neither 0 nor negative nor "more slots
                // than ProtoCore has" can ever be placed.
                if (entry.SlotSpan < 1 || entry.SlotSpan > slotCount)
                    errors.Add($"Entry for {label} has slot span {entry.SlotSpan}, " +
                               $"which does not fit a {slotCount}-slot ProtoCore.");

                // A compatibility override must point at a real, different revision of
                // the *same* id - never at itself (a no-op that reads like a mistake),
                // never at a different id (compatibility is not modeled across
                // different ProtoMod types), and never at a revision this registry
                // doesn't actually contain (an override the registry can't resolve is
                // worse than no override, since it looks like the module has a control
                // scheme when TryCreate would actually fail).
                if (entry.CompatibleWithRevision != null)
                {
                    if (entry.CompatibleWithRevision == entry.Revision)
                    {
                        errors.Add($"Entry for {label} declares compatibility with its own revision.");
                    }
                    else if (!entries.Any(e => e.Id == entry.Id && e.Revision == entry.CompatibleWithRevision))
                    {
                        errors.Add($"Entry for {label} declares compatibility with revision " +
                                   $"\"{entry.CompatibleWithRevision}\", which has no entry for id 0x{entry.Id:X4}.");
                    }
                }
            }

            return errors;
        }
    }
}
