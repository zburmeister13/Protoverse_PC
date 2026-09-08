using System.Linq;
using ProtoVerseApp.Models;
using Xunit;

namespace ProtoVerseApp.Tests
{
    /// <summary>
    /// Guardrails for rail control's two shared contracts.
    ///
    /// The first is the supply tree. It was established by exporting a KiCad netlist
    /// and walking the node lists, and getting it wrong is not a cosmetic problem:
    /// firmware enforces interlocks derived from the same tree, and the app uses it to
    /// tell the user what a click is about to switch on. A wrong parent here produces
    /// a confidently-worded lie about the hardware. It also already caught a real bug
    /// once - firmware's original interlock had the LDOs hanging off 3V1 rather than
    /// 1V8 - so it's worth pinning rather than trusting to comments.
    ///
    /// The second is the wire vocabulary: rail ids and the sub-command/error bytes are
    /// shared with firmware and must never drift.
    /// </summary>
    public class VoltageRailTests
    {
        [Fact]
        public void RailIds_MatchThePcf8574aExpanderPinOrder()
        {
            // U5.4/5/6/7/9/10 = P0-P5, confirmed against the schematic and against
            // firmware's power_expander.h enum. Frozen vocabulary: append only.
            Assert.Equal(0, (byte)RailId.Ldo1V0);
            Assert.Equal(1, (byte)RailId.Ldo1V2);
            Assert.Equal(2, (byte)RailId.Buck1V4);
            Assert.Equal(3, (byte)RailId.Buck1V8);
            Assert.Equal(4, (byte)RailId.Buck3V1);
            Assert.Equal(5, (byte)RailId.BuckVar);
        }

        [Fact]
        public void WireVocabulary_MatchesTheAgreedContract()
        {
            Assert.Equal(0x01, VoltageRailCatalog.CmdSetRail);
            Assert.Equal(0x02, VoltageRailCatalog.CmdGetRails);
            Assert.Equal(0x06, VoltageRailCatalog.ErrDependency);
            Assert.Equal(0x01, VoltageRailCatalog.ErrNotPresent);
        }

        [Fact]
        public void EveryRailIdHasExactlyOneCatalogEntry()
        {
            foreach (RailId id in System.Enum.GetValues<RailId>())
                Assert.True(VoltageRailCatalog.Find(id) != null, $"No catalog entry for {id}.");

            Assert.Equal(
                VoltageRailCatalog.Rails.Count,
                VoltageRailCatalog.Rails.Select(r => r.Id).Distinct().Count());
        }

        [Fact]
        public void SupplyTree_MatchesTheNetlistTrace()
        {
            // 1V8_BUCK -> U6 -> 1V0_LDO, and 1V8_BUCK -> U8 -> 1V2_LDO. This is the
            // pair that mattered: firmware originally had both hanging off 3V1.
            Assert.Equal(RailId.Buck1V8, VoltageRailCatalog.Find(RailId.Ldo1V0)!.Parent);
            Assert.Equal(RailId.Buck1V8, VoltageRailCatalog.Find(RailId.Ldo1V2)!.Parent);

            // 3V1_BUCK -> U3 -> 1V8_BUCK
            Assert.Equal(RailId.Buck3V1, VoltageRailCatalog.Find(RailId.Buck1V8)!.Parent);

            // These three hang off 4V5, which is not switchable - so no parent among
            // the rails this app can control.
            Assert.Null(VoltageRailCatalog.Find(RailId.Buck1V4)!.Parent);
            Assert.Null(VoltageRailCatalog.Find(RailId.Buck3V1)!.Parent);
            Assert.Null(VoltageRailCatalog.Find(RailId.BuckVar)!.Parent);
        }

        [Fact]
        public void EnablingAnLdo_RequiresBoth1V8And3V1()
        {
            var ancestors = VoltageRailCatalog.AncestorsOf(RailId.Ldo1V0)
                .Select(r => r.Id)
                .ToList();

            // Nearest parent first, then up the chain - the order the UI reads them out.
            Assert.Equal(new[] { RailId.Buck1V8, RailId.Buck3V1 }, ancestors);
        }

        [Fact]
        public void Disabling3V1_IsBlockedBy1V8Only_NotDirectlyByTheLdos()
        {
            // 3V1's only direct child is 1V8. The LDOs block 1V8, which in turn blocks
            // 3V1 - the two-level chain, not a flat "3V1 owns everything" mask.
            var children = VoltageRailCatalog.ChildrenOf(RailId.Buck3V1).Select(r => r.Id).ToList();
            Assert.Equal(new[] { RailId.Buck1V8 }, children);

            var ldoParents = VoltageRailCatalog.ChildrenOf(RailId.Buck1V8).Select(r => r.Id).ToList();
            Assert.Contains(RailId.Ldo1V0, ldoParents);
            Assert.Contains(RailId.Ldo1V2, ldoParents);
        }

        [Fact]
        public void NoRailAdvertisesAVoltageItCannotKnow()
        {
            // The variable buck's output is trimmed by feedback resistors on the board
            // and nobody has told this app what it's set to. Per the project's
            // no-fabrication rule it stays null rather than being guessed - a wrong
            // number on a power rail is worse than no number.
            Assert.Null(VoltageRailCatalog.Find(RailId.BuckVar)!.NominalVoltage);

            foreach (var rail in VoltageRailCatalog.Rails.Where(r => r.Id != RailId.BuckVar))
                Assert.False(string.IsNullOrWhiteSpace(rail.NominalVoltage), $"{rail.Id} has no voltage label.");
        }

        [Theory]
        [InlineData(RailId.Ldo1V0, true, "SetRail: 1.0 V LDO on")]
        [InlineData(RailId.Buck3V1, false, "SetRail: 3.1 V buck off")]
        public void TrafficLog_DecodesSetRail(RailId id, bool on, string expected)
        {
            var frame = new ProtocolFrame(ProtoModId.Core, MsgType.Command,
                new[] { VoltageRailCatalog.CmdSetRail, (byte)id, (byte)(on ? 1 : 0) });

            Assert.Equal(expected, FrameInterpreter.Describe(frame));
        }

        [Fact]
        public void TrafficLog_DecodesTheRailSnapshotByName()
        {
            // [rail_count, one byte per rail] - 1V0, 1V8 and 3V1 on, as a single click
            // on the 1.0 V rail actually produces once firmware cascades.
            var payload = new byte[] { 6, 1, 0, 0, 1, 1, 0 };
            var frame = new ProtocolFrame(ProtoModId.Core, MsgType.Response, payload);

            var described = FrameInterpreter.Describe(frame);

            Assert.Contains("1.0 V LDO=on", described);
            Assert.Contains("1.2 V LDO=off", described);
            Assert.Contains("1.8 V buck=on", described);
            Assert.Contains("3.1 V buck=on", described);
            Assert.Contains("Variable buck=off", described);
        }

        [Fact]
        public void TrafficLog_NamesTheDependencyError()
        {
            var frame = new ProtocolFrame(ProtoModId.Core, MsgType.Error,
                new[] { VoltageRailCatalog.ErrDependency });

            Assert.Contains("PROTOCOL_ERR_DEPENDENCY", FrameInterpreter.Describe(frame));
        }

        [Fact]
        public void TrafficLog_DoesNotMisreadATruncatedSnapshot()
        {
            // Claims 6 rails, carries 2. Must fall back rather than read past the end -
            // the frame reader guarantees a valid checksum, not a sane payload.
            var frame = new ProtocolFrame(ProtoModId.Core, MsgType.Response, new byte[] { 6, 1 });

            var described = FrameInterpreter.Describe(frame);

            Assert.DoesNotContain("1.0 V LDO=on", described);
        }
    }
}
