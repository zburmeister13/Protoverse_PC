using System.Collections.Generic;
using System.Linq;

namespace ProtoVerseApp.Models.Registry
{
    public enum RegistryLookupKind
    {
        /// <summary>This exact (Id, Revision) is in the registry - resolved, ready to
        /// use. <see cref="RegistryLookupResult.ResolvedControlScheme"/> and
        /// <see cref="RegistryLookupResult.ResolvedManualReference"/> already account
        /// for a <see cref="ProtoModRegistryEntry.CompatibleWithRevision"/> override,
        /// if one applies.</summary>
        Found,

        /// <summary>The id has at least one registry entry, but not this revision.
        /// Firmware/EEPROM is reporting a real, known ProtoMod type running hardware
        /// this registry has never been told about - the "revision defaults to
        /// different module" constraint means this is exactly as unsupported as an
        /// id this registry has never heard of at all, not a smaller problem.</summary>
        UnrecognizedRevision,

        /// <summary>No registry entry exists for this id at any revision.</summary>
        UnknownId
    }

    /// <summary>Outcome of a lookup. <see cref="ResolvedControlScheme"/> and
    /// <see cref="ResolvedManualReference"/> are only meaningful when
    /// <see cref="Kind"/> is <see cref="RegistryLookupKind.Found"/>.</summary>
    public record RegistryLookupResult(
        RegistryLookupKind Kind,
        ProtoModRegistryEntry? Entry,
        string? ResolvedControlScheme,
        string? ResolvedManualReference);

    /// <summary>
    /// Revision-aware replacement for keying module lookups on <see cref="ProtoModId"/>
    /// alone - the core ask of this evaluation. Where
    /// <see cref="ViewModels.ModuleCatalog"/> answers "what panel does this ID get,"
    /// this answers "what panel does this (ID, Revision) get, and is that answer
    /// borrowed from a different revision by explicit human override."
    ///
    /// Takes already-validated entries (see <see cref="ProtoModRegistryValidator"/>) -
    /// this class assumes no duplicate (Id, Revision) pairs and no dangling
    /// <see cref="ProtoModRegistryEntry.CompatibleWithRevision"/> references, which is
    /// exactly what validation guarantees, so it never has to re-check those itself.
    /// </summary>
    public class RevisionAwareModuleCatalog
    {
        private readonly Dictionary<(ushort Id, string Revision), ProtoModRegistryEntry> _byIdentity;
        private readonly HashSet<ushort> _knownIds;

        public RevisionAwareModuleCatalog(IReadOnlyList<ProtoModRegistryEntry> entries)
        {
            _byIdentity = entries.ToDictionary(e => (e.Id, e.Revision));
            _knownIds = entries.Select(e => e.Id).ToHashSet();
        }

        public RegistryLookupResult Resolve(ushort id, string revision)
        {
            if (!_byIdentity.TryGetValue((id, revision), out var entry))
            {
                return new RegistryLookupResult(
                    _knownIds.Contains(id) ? RegistryLookupKind.UnrecognizedRevision : RegistryLookupKind.UnknownId,
                    null, null, null);
            }

            return new RegistryLookupResult(RegistryLookupKind.Found, entry, ResolveControlScheme(entry), entry.ManualReference);
        }

        /// <summary>Follows <see cref="ProtoModRegistryEntry.CompatibleWithRevision"/>
        /// to find whichever entry actually owns the control scheme, without ever
        /// assuming an unbroken chain - <see cref="ProtoModRegistryValidator"/>
        /// guarantees each single hop resolves to a real entry, but guards against a
        /// cycle here anyway rather than trusting that guarantee all the way down a
        /// chain a future registry might grow.
        ///
        /// Deliberately resolves *only* the control scheme, not the manual reference:
        /// an override declares "this revision's controls behave like that one's," not
        /// "this revision's physical assembly steps are identical" - see the doc
        /// comment on <see cref="ProtoModRegistryEntry.ManualReference"/>. Each entry
        /// keeps its own manual regardless of any override.</summary>
        private string ResolveControlScheme(ProtoModRegistryEntry entry)
        {
            var current = entry;
            var visited = new HashSet<string> { current.Revision };

            while (current.CompatibleWithRevision != null)
            {
                if (!visited.Add(current.CompatibleWithRevision))
                    break; // cycle - stop and use whatever this entry currently has, rather than loop forever

                if (!_byIdentity.TryGetValue((current.Id, current.CompatibleWithRevision), out var target))
                    break; // dangling override - validator should have caught this; degrade rather than throw

                current = target;
            }

            return current.ControlScheme;
        }
    }
}
