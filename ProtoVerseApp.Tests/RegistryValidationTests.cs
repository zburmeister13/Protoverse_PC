using System.IO;
using System.Runtime.CompilerServices;
using ProtoVerseApp.Models.Registry;
using Xunit;

namespace ProtoVerseApp.Tests
{
    /// <summary>
    /// This is the "CI check" the evaluation task asked for: a real registry file
    /// with any validation error fails this test, and therefore fails a build. Uses
    /// <see cref="CallerFilePathAttribute"/> to find the real
    /// ProtoModRegistry.json next to the source tree rather than depending on a
    /// build output layout, which would make this test fragile to exactly the kind
    /// of build/copy-step changes this repo's own CLAUDE.md already warns are easy
    /// to get subtly wrong (see the "CopyToOutputDirectory can silently ship a stale
    /// asset" gotcha).
    /// </summary>
    public class RegistryValidationTests
    {
        private const int SlotCount = 3; // matches MainViewModel.SlotCount

        private static string RealRegistryPath([CallerFilePath] string here = "") =>
            Path.Combine(Path.GetDirectoryName(here)!, "..", "ProtoVerseApp", "Models", "Registry", "ProtoModRegistry.json");

        [Fact]
        public void RealRegistryFileExists()
        {
            Assert.True(File.Exists(RealRegistryPath()),
                $"Expected to find the real registry at {RealRegistryPath()}");
        }

        [Fact]
        public void RealRegistryHasNoValidationErrors()
        {
            var document = ProtoModRegistryLoader.LoadFromFile(RealRegistryPath());
            var errors = ProtoModRegistryValidator.Validate(document.Entries, SlotCount);

            Assert.True(errors.Count == 0,
                "Real registry failed validation:\n" + string.Join("\n", errors));
        }

        [Fact]
        public void RealRegistryHasAtLeastTheFourKnownEntries()
        {
            var document = ProtoModRegistryLoader.LoadFromFile(RealRegistryPath());
            Assert.True(document.Entries.Count >= 4);
        }
    }
}
