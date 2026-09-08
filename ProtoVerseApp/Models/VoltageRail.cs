using System.Collections.Generic;
using System.Linq;

namespace ProtoVerseApp.Models
{
    /// <summary>
    /// The six ProtoCore supply rails that can be switched on and off, as ids on the
    /// wire. Frozen, shared vocabulary with firmware exactly like
    /// <see cref="ProtoModId"/> - never renumbered, only appended to.
    ///
    /// The order is firmware's `power_expander.h` enum order, independently confirmed
    /// against the schematic: these map 1:1 onto the PCF8574A expander's output pins
    /// (U5.4/5/6/7/9/10 = P0-P5) in exactly this sequence.
    /// </summary>
    public enum RailId : byte
    {
        Ldo1V0 = 0,
        Ldo1V2 = 1,
        Buck1V4 = 2,
        Buck1V8 = 3,
        Buck3V1 = 4,
        BuckVar = 5
    }

    /// <summary>
    /// One switchable rail's identity and its place in the supply tree.
    /// </summary>
    /// <param name="NominalVoltage">Display string, or null where this app genuinely
    /// doesn't know. Null for the variable buck: its output is trimmed by feedback
    /// resistors on the board rather than being one of the documented nominal
    /// voltages, and nobody has told us what it's set to. Per this project's
    /// no-fabrication rule that stays blank rather than being computed from a
    /// datasheet figure taken on trust - a wrong number on a power rail is worse
    /// than no number.</param>
    /// <param name="Parent">The switchable rail this one is powered from, or null if
    /// it hangs off a rail that can't be switched (4V5, which is always on). Drives
    /// the dependency explanations in the UI.</param>
    public record VoltageRail(
        RailId Id,
        string Name,
        string? NominalVoltage,
        RailId? Parent,
        string SuppliedBy);

    /// <summary>
    /// The rail catalog and the wire vocabulary for rail control.
    ///
    /// THE SUPPLY TREE BELOW IS FROM A NETLIST TRACE, not from reading the schematic
    /// sheet. That distinction cost real effort and is worth preserving: every
    /// regulator input on this board runs through a 2-pin link, so the rendered sheet
    /// does not show what actually feeds what. Exporting a kicadxml netlist and
    /// walking the node lists is what established that the 1V0/1V2 LDOs hang off
    /// 1V8_BUCK rather than off 3V1_BUCK - which turned out to be a real bug in
    /// firmware's own interlock, since fixed.
    ///
    /// COMMANDED, NEVER MEASURED. On this hardware revision the PGOOD nets are not
    /// routed to the MCU (user, 2026-09-07: "It's simply the name of a net on the
    /// schematic. There is no closed loop"). Firmware therefore reports the bit it
    /// last wrote, not whether the rail actually came up, and there is no way to tell
    /// the difference in software. Any UI built on this must say so - the Electronic
    /// Load panel's "commanded, not measured" wording is the precedent.
    /// </summary>
    public static class VoltageRailCatalog
    {
        /// <summary>payload[0] of a Command addressed to <see cref="ProtoModId.Core"/>.
        /// Layout: [CmdSetRail, rail id, 0|1].</summary>
        public const byte CmdSetRail = 0x01;

        /// <summary>payload[0] of a Command addressed to <see cref="ProtoModId.Core"/>.
        /// No further payload; the reply is the same snapshot every rail command
        /// returns.</summary>
        public const byte CmdGetRails = 0x02;

        /// <summary>Interlock refusal - e.g. switching off a rail something else is
        /// still running from. New code agreed with firmware for rail control; the
        /// 0x01-0x05 codes are the pre-existing set.</summary>
        public const byte ErrDependency = 0x06;

        /// <summary>Returned when a Command is addressed to a ProtoModId firmware
        /// can't resolve. Until firmware ships a Core dispatch path, that's what
        /// every rail command gets back - which is how this app tells "your firmware
        /// is too old for rail control" apart from a dead link.</summary>
        public const byte ErrNotPresent = 0x01;

        public static readonly IReadOnlyList<VoltageRail> Rails = new[]
        {
            new VoltageRail(RailId.Ldo1V0, "1.0 V LDO", "1.0 V", RailId.Buck1V8, "1.8 V buck, via U6 (AP7330)"),
            new VoltageRail(RailId.Ldo1V2, "1.2 V LDO", "1.2 V", RailId.Buck1V8, "1.8 V buck, via U8 (AP7330)"),
            new VoltageRail(RailId.Buck1V4, "1.4 V buck", "1.4 V", null, "4.5 V rail, via U1 (ADP2120)"),
            new VoltageRail(RailId.Buck1V8, "1.8 V buck", "1.8 V", RailId.Buck3V1, "3.1 V buck, via U3 (ADP2120)"),
            new VoltageRail(RailId.Buck3V1, "3.1 V buck", "3.1 V", null, "4.5 V rail, via U4 (ADP2120)"),
            // Deliberately no nominal voltage - see the VoltageRail doc comment.
            new VoltageRail(RailId.BuckVar, "Variable buck", null, null, "4.5 V rail, via U9 (ADP2120)")
        };

        public static VoltageRail? Find(RailId id) => Rails.FirstOrDefault(r => r.Id == id);

        /// <summary>Every rail that must be on before <paramref name="id"/> can be,
        /// nearest parent first. Firmware enables these automatically on the way up;
        /// the app uses the list only to say what a click is about to switch on.</summary>
        public static IReadOnlyList<VoltageRail> AncestorsOf(RailId id)
        {
            var chain = new List<VoltageRail>();
            var current = Find(id)?.Parent;
            while (current != null)
            {
                var rail = Find(current.Value);
                if (rail == null) break;
                chain.Add(rail);
                current = rail.Parent;
            }
            return chain;
        }

        /// <summary>Rails powered directly from <paramref name="id"/>. Firmware
        /// refuses to switch a rail off while any of these is still on.</summary>
        public static IReadOnlyList<VoltageRail> ChildrenOf(RailId id) =>
            Rails.Where(r => r.Parent == id).ToList();
    }
}
