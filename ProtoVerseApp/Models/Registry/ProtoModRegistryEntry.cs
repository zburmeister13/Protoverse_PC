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
    /// though every board shipped today happens to be a single revision. This is
    /// the app's logical identity axis - a single string an entry is keyed on -
    /// which is deliberately a simpler model than the EEPROM's own separate PCB/PCBA
    /// revision sub-fields; see <paramref name="PcbRev"/>/<paramref name="PcbaRev"/>.</param>
    /// <param name="Name">Display name shown in the UI (Library, slot navigator).</param>
    /// <param name="CircuitCode">Circuit code as printed on the board / burned into
    /// its EEPROM, e.g. "F01" - was previously duplicated (and once drifted out of
    /// sync - see the AccelTemp/F02 mixup in CLAUDE.md) between
    /// <see cref="ProtoModBoardCatalog"/> and other per-module lookups. This
    /// registry is now the one place it's authored.</param>
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
    /// <param name="PcbRev">Raw EEPROM PCB revision sub-field (e.g. "R1") - purely
    /// informational today (nothing in the UI reads it, same as before this
    /// registry existed), carried forward from <see cref="ProtoModBoardCatalog"/>
    /// rather than dropped, since it is real hardware data. Not the same axis as
    /// <paramref name="Revision"/> above and never assumed to move in lockstep with
    /// it.</param>
    /// <param name="PcbaRev">Raw EEPROM PCBA revision sub-field - same notes as
    /// <paramref name="PcbRev"/>.</param>
    public record ProtoModRegistryEntry(
        [property: JsonPropertyName("id")] ushort Id,
        [property: JsonPropertyName("revision")] string Revision,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("circuitCode")] string CircuitCode,
        [property: JsonPropertyName("manualReference")] string? ManualReference,
        [property: JsonPropertyName("controlScheme")] string ControlScheme,
        [property: JsonPropertyName("slotSpan")] int SlotSpan,
        [property: JsonPropertyName("compatibleWithRevision")] string? CompatibleWithRevision = null,
        [property: JsonPropertyName("pcbRev")] string? PcbRev = null,
        [property: JsonPropertyName("pcbaRev")] string? PcbaRev = null)
    {
        [JsonIgnore]
        public ProtoModId ProtoModId => (ProtoModId)Id;
    }
}
