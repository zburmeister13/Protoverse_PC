using System;
using System.IO;
using System.Linq;
using ProtoVerseApp.Models.Manual;
using ProtoVerseApp.Services;
using ProtoVerseApp.ViewModels;
using Xunit;

namespace ProtoVerseApp.Tests
{
    /// <summary>
    /// Covers the two features derived from manual content: the cheat sheet and the
    /// answers export.
    ///
    /// The export is worth a real test rather than a click-through because its whole
    /// value is that it reflects what the learner actually entered. A PDF that renders
    /// beautifully and quietly drops an answer is worse than no export - it would be
    /// handed in as evidence of work that then isn't in it.
    /// </summary>
    public class ManualExportTests
    {
        private static ManualViewModel BlinkyManualVm() => new(BlinkyManual.Build());

        // ------------------------------------------------------------ cheat sheet

        [Fact]
        public void CheatSheet_CollectsTechNotesAndKeyTakeaways()
        {
            var sheet = CheatSheet.Build(BlinkyManual.Build());

            Assert.False(sheet.IsEmpty);
            Assert.NotEmpty(sheet.Notes);
            Assert.NotEmpty(sheet.Takeaways);
            Assert.Equal("F01", sheet.ModuleCode);

            // F01's tech notes include the 13 mA arithmetic - the single most
            // recall-worthy thing on that board.
            Assert.Contains(sheet.Notes, n => n.Text.Contains("13 mA"));
        }

        [Fact]
        public void CheatSheet_ExcludesAppendicesAndNonTechNoteCallouts()
        {
            var document = BlinkyManual.Build();
            var sheet = CheatSheet.Build(document);

            // Facilitator notes are for whoever runs the lab, not for recall.
            var appendixTitles = document.Sections.Where(s => s.IsAppendix).Select(s => s.Title).ToList();
            Assert.NotEmpty(appendixTitles);
            foreach (var title in appendixTitles)
                Assert.DoesNotContain(sheet.Takeaways, t => t.SectionTitle == title);

            // Observe prompts are questions, not facts to memorize.
            var observeTexts = document.Sections
                .SelectMany(s => s.Blocks)
                .OfType<CalloutBlock>()
                .Where(c => c.Kind == CalloutKind.Observe)
                .Select(c => c.Text)
                .ToList();
            foreach (var text in observeTexts)
                Assert.DoesNotContain(sheet.Notes, n => n.Text == text);
        }

        [Fact]
        public void CheatSheet_IsEmptyRatherThanThrowing_ForAManualWithNothingToSummarize()
        {
            var bare = new ManualDocument(
                "X00",
                new ManualHeader("Test Series", "X00", "Bare", "Nothing here", null, null, null),
                new[] { new ManualSection("s", "1. Only prose", new ManualBlock[] { new ParagraphBlock("text") }) },
                "test");

            var sheet = CheatSheet.Build(bare);

            Assert.True(sheet.IsEmpty);
            Assert.Empty(sheet.Notes);
            Assert.Empty(sheet.Takeaways);
        }

        // ---------------------------------------------------------- answers export

        [Fact]
        public void Export_ProducesARealPdf_ContainingTheLearnersAnswers()
        {
            var manual = BlinkyManualVm();

            // Answer the first multiple-choice question correctly, and leave the rest
            // alone so the "not answered" path is exercised in the same document.
            var choices = manual.Sections
                .SelectMany(s => s.Blocks)
                .OfType<MultipleChoiceViewModel>()
                .FirstOrDefault();
            Assert.NotNull(choices);

            var question = choices!.Questions[0];
            var correct = question.Options.First(o => o.IsCorrect);
            correct.SelectCommand.Execute(null);

            Assert.True(question.IsAnswered);
            Assert.True(question.AnsweredCorrectly);

            var output = Path.Combine(Path.GetTempPath(), $"protoverse_test_{Guid.NewGuid():N}.pdf");
            try
            {
                var result = ManualPdfExporter.Export(manual, "Test Learner", output);

                // Edge is present on this machine and on any Windows box; if it ever
                // isn't, the exporter is required to say so rather than fail silently,
                // and that message is worth surfacing in the failure.
                Assert.True(result.Success, result.Message);
                Assert.True(File.Exists(output), "Export reported success but wrote no file.");

                var bytes = File.ReadAllBytes(output);
                Assert.True(bytes.Length > 1000, $"PDF is suspiciously small ({bytes.Length} bytes).");
                Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            }
            finally
            {
                try { File.Delete(output); } catch { /* best effort */ }
            }
        }

        [Fact]
        public void Export_ReportsFailureRatherThanThrowing_WhenTheOutputPathIsUnwritable()
        {
            var manual = BlinkyManualVm();

            // A directory that cannot exist - the exporter must return a message, not
            // let an IOException reach the app's global crash handler.
            var output = Path.Combine("Z:", "no-such-drive", $"{Guid.NewGuid():N}.pdf");

            var result = ManualPdfExporter.Export(manual, "Test Learner", output);

            Assert.False(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.Message));
        }
    }
}
