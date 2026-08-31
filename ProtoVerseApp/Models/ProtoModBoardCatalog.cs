using System.Collections.Generic;

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
    /// Mirrors firmware's `Core/Src/protomod_catalog.c` (separate `Protocore`
    /// codebase) - must match it exactly, update both sides together, same spirit as
    /// `ProtoModId` mirroring `protocol.h`'s enum. Purely reference/documentation on
    /// this side: the PC app never talks to a ProtoMod's EEPROM directly, so this
    /// data isn't sent over the wire or used to parse anything here - it exists so a
    /// person looking at this app (the Help tab) can see the circuit code a
    /// recognized ProtoMod type is expected to report, which is the only way to
    /// independently sanity-check the ProtoModId<->physical-board mapping.
    ///
    /// RESOLVED (doc-confirmed) 2026-08-30, hardware-read still pending: an earlier
    /// version of this catalog listed AccelTemp="F02", which the user's real-hardware
    /// session directly contradicted ("board two is not IMU and Temp"). Settled
    /// against the project's own module manuals
    /// (`Documents/.../PROTOVERSE/Manuals/`), not another guess: `E03_Sensors1.docx`
    /// - "Module Name: Sensors 1 (E03) ... This ProtoMod introduces two types of
    /// sensors: STM LIS3DH accelerometer ... Analog Devices TMP36 temperature
    /// sensor" - is AccelTemp, precisely. `F02_Simple_LED.docx` - "Simple LED (F02)
    /// ... two LED paths (one red, one green) plus two switches per path" - is a
    /// static resistor/voltage demo board with zero sensors, not AccelTemp. Both
    /// quotes independently verified against the actual .docx files by this app's
    /// own session, not just trusted from the firmware session's relay. AccelTemp's
    /// circuit code is now "E03". Caveat, same honesty standard as everywhere else in
    /// this project: this is documentation-confirmed, not yet hardware-confirmed - no
    /// ProtoCore has done a live EEPROM read of a physical Sensors-1 board to verify
    /// "E03" is really what's burned into it. `identify_slots()`/
    /// `BoardID_ReadParsed()` are both non-destructive and already implemented
    /// firmware-side, so that read is trivial once real Sensors-1 hardware is on the
    /// bench, and would be the final word if this is ever in doubt again.
    ///
    /// FULLY CLOSED 2026-08-30 with real hardware evidence (a raw-serial capture, not
    /// documentation this time): the physical board that started the AccelTemp/"F02"
    /// confusion above was never an AccelTemp unit at all - it's a real board with a
    /// valid EEPROM that simply wasn't in firmware's catalog yet, so it was reporting
    /// as <see cref="ProtoModId.None"/> (indistinguishable from an empty slot).
    /// Firmware added <see cref="ProtoModId.Unknown"/> (0xFFE0) for exactly this
    /// "present but unrecognized" case, plus the actual missing catalog entry:
    /// <see cref="ProtoModId.BasicLed"/> (0x0004, circuit code "F02") - which, fittingly,
    /// is exactly the Simple LED board the manuals said "F02" was, confirming that
    /// reading was right all along; it just needed its own catalog entry rather than
    /// being confused with AccelTemp's.
    /// </summary>
    public static class ProtoModBoardCatalog
    {
        public static readonly IReadOnlyList<ProtoModBoardIdentity> Entries = new[]
        {
            new ProtoModBoardIdentity(ProtoModId.BlinkyLed, "F01", "R1", "R1"),
            new ProtoModBoardIdentity(ProtoModId.AccelTemp, "E03", "R1", "R1"),
            new ProtoModBoardIdentity(ProtoModId.ElectronicLoad, "E05", "R1", "R1"),
            new ProtoModBoardIdentity(ProtoModId.BasicLed, "F02", "R1", "R1"),
        };
    }
}
