using System.Text.Json.Serialization;

namespace ProtoVerseApp.Models.Registry
{
    /// <summary>
    /// One (ProtoModId, Revision) identity in the scale-framework registry - the
    /// single source of truth this evaluation proposes for "what is this board, and
    /// how does the app control it," as distinct from <see cref="ProtoModId"/> (the
    /// wire-level type tag) and <see cref="ProtoModBoardCatalog"/> (today's
    /// ID-only, revision-blind EEPROM mirror).
    ///
    /// EVALUATION SCOPE (see EVALUATION.md at repo root on this branch): this type
    /// models identity only - name, manual, control scheme, physical footprint. It
    /// deliberately does not model capability (what commands a board accepts, what
    /// its payloads mean) - that direction is explicitly out of scope per the task
    /// that produced this branch ("do not implement self-describing capability
    /// manifests").
    /// </summary>
    /// <param name="Id">The wire-level type tag - matches <see cref="ProtoModId"/>
    /// firmware also reports. Not unique by itself in this registry; see
    /// <paramref name="Revision"/>.</param>
    /// <param name="Revision">Hardware revision, e.g. "A", "B". Opaque and
    /// unordered on purpose - see <see cref="CompatibleWithRevision"/>. Required:
    /// an entry with no revision is a validation error (a registry that can't say
    /// which hardware revision it describes can't safely control anything), even
    /// though every board shipped today happens to be a single revision.</param>
    /// <param name="Name">Display name shown in the UI (Library, slot navigator).</param>
    /// <param name="ManualReference">Path/id of this revision's manual content, or
    /// null if none exists yet. A different revision can reference a different
    /// manual even when <see cref="CompatibleWithRevision"/> shares its control
    /// scheme - the physical board changed, so the assembly/observe steps may no
    /// longer match, even if the command set is identical.</param>
    /// <param name="ControlScheme">Identifies which panel/ViewModel this revision
    /// uses - a string key into whatever registers panel factories (see
    /// <see cref="RevisionAwareModuleCatalog"/>), not a manual reference. Ignored
    /// when <see cref="CompatibleWithRevision"/> is set; present for both cases so
    /// the field always documents "if this entry did own its control scheme, that
    /// would be its key," which matters for a human auditing the registry.</param>
    /// <param name="SlotSpan">How many adjacent physical slots this ProtoMod
    /// occupies: 1, 2, or 3 on today's 3-slot ProtoCore. See
    /// <see cref="MultiSlotResolver"/> for how a span &gt; 1 is resolved from a
    /// single identifying slot.</param>
    /// <param name="CompatibleWithRevision">Explicit, human-authored override: when
    /// set, this revision uses the named earlier revision's
    /// <see cref="ControlScheme"/> instead of its own. Null means "no override -
    /// this revision is its own, separate identity," which is the default and the
    /// only safe assumption absent a person's explicit say-so (see the "revision
    /// defaults to different module" hard constraint in EVALUATION.md). Must name a
    /// revision of the *same* <see cref="Id"/> - compatibility across different
    /// ProtoModId values is not a thing this registry models.</param>
    public record ProtoModRegistryEntry(
        [property: JsonPropertyName("id")] ushort Id,
        [property: JsonPropertyName("revision")] string Revision,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("manualReference")] string? ManualReference,
        [property: JsonPropertyName("controlScheme")] string ControlScheme,
        [property: JsonPropertyName("slotSpan")] int SlotSpan,
        [property: JsonPropertyName("compatibleWithRevision")] string? CompatibleWithRevision = null)
    {
        [JsonIgnore]
        public ProtoModId ProtoModId => (ProtoModId)Id;
    }
}
