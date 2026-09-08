using System.Collections.Generic;
using System.Linq;
using ProtoVerseApp.Models.Registry;

namespace ProtoVerseApp.Models
{
    /// <summary>One ProtoMod type's expected EEPROM identity fields. Every ProtoMod's
    /// onboard EEPROM (an AT24C02, read by ProtoCore over I2C) stores an 11-byte
    /// record: offset 0-2 circuit code (ASCII, e.g. "F01"), 3-4 PCB rev (ASCII 2
    /// chars), 5-6 PCBA rev (ASCII 2 chars), 7-8 WW/YY date, 9-10 two misc bytes -
    /// this app never reads that EEPROM itself (only ProtoCore's I2C bus can), so
    /// only the identity fields worth showing a person are mirrored here.</summary>
    public record ProtoModBoardIdentity(ProtoModId Id, string CircuitCode, string PcbRev, string PcbaRev);

    /// <summary>
    /// Reference/documentation view of each ProtoMod type's expected EEPROM
    /// identity - purely for a person (the Help tab) to independently sanity-check
    /// the ProtoModId&lt;-&gt;physical-board mapping; this app never talks to a
    /// ProtoMod's EEPROM directly, so nothing here is sent over the wire or used to
    /// parse anything.
    ///
    /// AS OF 2026-09-07, THIS IS A COMPUTED VIEW, NOT A SEPARATE CATALOG. The
    /// circuit code / PCB rev / PCBA rev per ID used to be hand-maintained here
    /// independently of <see cref="ViewModels.ModuleCatalog"/> and
    /// <see cref="Manual.ManualLibrary"/>'s own per-ID dictionaries - three sources
    /// of truth for "what is this ProtoMod" that could (and once did: the
    /// AccelTemp/F02 circuit-code mixup, see CLAUDE.md) silently disagree.
    /// <c>Entries</c> now reads straight from
    /// <see cref="ProtoModRegistryService"/>, which those other two also read from,
    /// so there is exactly one place any of this data is authored:
    /// <c>Models/Registry/ProtoModRegistry.json</c>.
    /// </summary>
    public static class ProtoModBoardCatalog
    {
        public static IReadOnlyList<ProtoModBoardIdentity> Entries =>
            ProtoModRegistryService.Entries
                .Where(e => e.Revision == ProtoModRegistryService.DefaultRevision)
                .Select(e => new ProtoModBoardIdentity(e.ProtoModId, e.CircuitCode, e.PcbRev ?? "?", e.PcbaRev ?? "?"))
                .ToList();
    }
}
