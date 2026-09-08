using System.Collections.Generic;
using ProtoVerseApp.Models.Registry;
using Xunit;

namespace ProtoVerseApp.Tests
{
    public class RevisionAwareCatalogTests
    {
        private static readonly ProtoModRegistryEntry RevA = new(
            Id: 1, Revision: "A", Name: "Widget", CircuitCode: "W01",
            ManualReference: "WidgetManual", ControlScheme: "WidgetViewModel", SlotSpan: 1);

        [Fact]
        public void KnownIdAndRevision_ResolvesFound()
        {
            var catalog = new RevisionAwareModuleCatalog(new[] { RevA });
            var result = catalog.Resolve(1, "A");

            Assert.Equal(RegistryLookupKind.Found, result.Kind);
            Assert.Equal("WidgetViewModel", result.ResolvedControlScheme);
            Assert.Equal("WidgetManual", result.ResolvedManualReference);
        }

        [Fact]
        public void KnownId_UnrecognizedRevision_DoesNotFallBackToAnyRevision()
        {
            // This is the "revision defaults to different module" hard constraint in
            // code: Rev B must never resolve using Rev A's data just because Rev A
            // happens to be the only thing registered for this id.
            var catalog = new RevisionAwareModuleCatalog(new[] { RevA });
            var result = catalog.Resolve(1, "B");

            Assert.Equal(RegistryLookupKind.UnrecognizedRevision, result.Kind);
            Assert.Null(result.ResolvedControlScheme);
        }

        [Fact]
        public void CompletelyUnknownId_IsDistinctFromUnrecognizedRevision()
        {
            var catalog = new RevisionAwareModuleCatalog(new[] { RevA });
            var result = catalog.Resolve(0x9999, "A");

            Assert.Equal(RegistryLookupKind.UnknownId, result.Kind);
        }

        [Fact]
        public void CompatibilityOverride_ResolvesToTargetRevisionsControlScheme()
        {
            var revB = new ProtoModRegistryEntry(
                Id: 1, Revision: "B", Name: "Widget Rev B", CircuitCode: "W01",
                ManualReference: "WidgetManualB", ControlScheme: "ignored-should-not-be-used",
                SlotSpan: 1, CompatibleWithRevision: "A");
            var catalog = new RevisionAwareModuleCatalog(new[] { RevA, revB });

            var result = catalog.Resolve(1, "B");

            Assert.Equal(RegistryLookupKind.Found, result.Kind);
            Assert.Equal("WidgetViewModel", result.ResolvedControlScheme); // borrowed from Rev A
            Assert.Equal("WidgetManualB", result.ResolvedManualReference); // Rev B keeps its own manual
        }

        [Fact]
        public void CompatibilityOverride_NeverInferredFromRevisionOrdering()
        {
            // Two revisions, neither declaring an override - "B" must not silently
            // behave like "A" just because a naive scheme might treat B as "newer than
            // and therefore compatible with" A.
            var revBNoOverride = new ProtoModRegistryEntry(
                Id: 1, Revision: "B", Name: "Widget Rev B", CircuitCode: "W01",
                ManualReference: null, ControlScheme: "WidgetBViewModel", SlotSpan: 1);
            var catalog = new RevisionAwareModuleCatalog(new[] { RevA, revBNoOverride });

            var result = catalog.Resolve(1, "B");

            Assert.Equal(RegistryLookupKind.Found, result.Kind);
            Assert.Equal("WidgetBViewModel", result.ResolvedControlScheme); // its own, not A's
        }

        [Fact]
        public void CompatibilityChain_ResolvesTransitively()
        {
            var revB = new ProtoModRegistryEntry(
                Id: 1, Revision: "B", Name: "Widget Rev B", CircuitCode: "W01",
                ManualReference: null, ControlScheme: "ignored", SlotSpan: 1, CompatibleWithRevision: "A");
            var revC = new ProtoModRegistryEntry(
                Id: 1, Revision: "C", Name: "Widget Rev C", CircuitCode: "W01",
                ManualReference: null, ControlScheme: "ignored", SlotSpan: 1, CompatibleWithRevision: "B");
            var catalog = new RevisionAwareModuleCatalog(new[] { RevA, revB, revC });

            var result = catalog.Resolve(1, "C");

            Assert.Equal("WidgetViewModel", result.ResolvedControlScheme); // Rev A's, via B
        }

        [Fact]
        public void CompatibilityCycle_DoesNotHang()
        {
            // Validator should reject this in practice (self/dangling reference
            // checks), but the catalog itself must not infinite-loop if it's ever
            // handed unvalidated data - defense in depth.
            var revA = new ProtoModRegistryEntry(
                Id: 1, Revision: "A", Name: "Widget", CircuitCode: "W01",
                ManualReference: null, ControlScheme: "A-scheme", SlotSpan: 1, CompatibleWithRevision: "B");
            var revB = new ProtoModRegistryEntry(
                Id: 1, Revision: "B", Name: "Widget", CircuitCode: "W01",
                ManualReference: null, ControlScheme: "B-scheme", SlotSpan: 1, CompatibleWithRevision: "A");
            var catalog = new RevisionAwareModuleCatalog(new[] { revA, revB });

            var result = catalog.Resolve(1, "A");

            Assert.Equal(RegistryLookupKind.Found, result.Kind);
            Assert.NotNull(result.ResolvedControlScheme); // must terminate with *something*, not hang
        }
    }
}
