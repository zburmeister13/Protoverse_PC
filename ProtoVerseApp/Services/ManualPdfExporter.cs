using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ProtoVerseApp.ViewModels;

namespace ProtoVerseApp.Services
{
    /// <summary>
    /// Exports a learner's manual answers as a PDF - the artifact a student hands in to
    /// show they completed the lab.
    ///
    /// WHY HTML THEN EDGE, rather than a PDF library. This project has no PDF
    /// dependency and adding one for a single feature is a poor trade; meanwhile
    /// Chromium's print-to-PDF is already a proven tool here (it is what renders the
    /// schematic assets and what verified the README's diagrams). So the export builds
    /// an HTML document and asks Edge to print it headlessly. The intermediate HTML is
    /// deliberately kept when the export fails, so a failure can be inspected rather
    /// than guessed at.
    ///
    /// WHAT IT CONTAINS, and why the correct answers are in it: this is a completion
    /// record, not an exam paper. The multiple-choice questions in the app already
    /// reveal their answer the moment they are answered, so printing the correct answer
    /// alongside the learner's leaks nothing that the learner has not already seen -
    /// and it makes the sheet markable at a glance, which is the point.
    ///
    /// It reflects exactly what is in the app right now. Nothing is inferred, and an
    /// unanswered question is reported as unanswered rather than blank, so a marker can
    /// tell "skipped" from "the export lost it".
    /// </summary>
    public static class ManualPdfExporter
    {
        public record ExportResult(bool Success, string Message, string? OutputPath);

        public static ExportResult Export(ManualViewModel manual, string? learnerName, string outputPath)
        {
            string html = BuildHtml(manual, learnerName);

            string htmlPath = Path.Combine(
                Path.GetTempPath(),
                $"protoverse_{manual.Document.ModuleCode}_{Guid.NewGuid():N}.html");

            try
            {
                File.WriteAllText(htmlPath, html, Encoding.UTF8);

                var edge = FindEdge();
                if (edge == null)
                {
                    return new ExportResult(false,
                        "Couldn't find Microsoft Edge, which this export uses to produce the PDF. " +
                        $"The formatted answers were saved as HTML instead:\n{htmlPath}", htmlPath);
                }

                var psi = new ProcessStartInfo(edge)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("--headless=new");
                psi.ArgumentList.Add("--disable-gpu");
                psi.ArgumentList.Add("--no-first-run");
                psi.ArgumentList.Add("--no-pdf-header-footer");
                psi.ArgumentList.Add($"--print-to-pdf={outputPath}");
                psi.ArgumentList.Add(new Uri(htmlPath).AbsoluteUri);

                using var process = Process.Start(psi);
                if (process == null)
                    return new ExportResult(false, "Couldn't start Microsoft Edge to render the PDF.", htmlPath);

                // Bounded: a headless render that hasn't finished in 30s isn't going to.
                if (!process.WaitForExit(30_000))
                {
                    try { process.Kill(true); } catch { /* already gone */ }
                    return new ExportResult(false, "Rendering the PDF timed out.", htmlPath);
                }

                if (!File.Exists(outputPath))
                {
                    return new ExportResult(false,
                        $"Edge exited without writing the PDF. The formatted answers are saved as HTML:\n{htmlPath}",
                        htmlPath);
                }

                // Only delete the intermediate on success - on failure it's the evidence.
                try { File.Delete(htmlPath); } catch { /* harmless */ }

                return new ExportResult(true, $"Saved to {outputPath}", outputPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                // Expected failure modes - a locked output file (the PDF already open in
                // a viewer is the common one), a read-only folder, Edge missing. Not
                // bugs; report them rather than letting them reach the crash handler.
                return new ExportResult(false, $"Couldn't write the PDF: {ex.Message}", null);
            }
        }

        private static string? FindEdge()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft", "Edge", "Application", "msedge.exe")
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        private static string BuildHtml(ManualViewModel manual, string? learnerName)
        {
            var doc = manual.Document;
            var sb = new StringBuilder();

            sb.Append("""
                <!doctype html><html><head><meta charset="utf-8"><style>
                @page { size: A4; margin: 16mm; }
                body { font-family: "Segoe UI", Arial, sans-serif; font-size: 10.5pt; color: #111; line-height: 1.45; }
                h1 { font-size: 17pt; margin: 0 0 2px 0; }
                h2 { font-size: 12pt; margin: 22px 0 8px 0; border-bottom: 1px solid #bbb; padding-bottom: 3px; }
                .sub { color: #555; margin: 0 0 14px 0; }
                .meta { border: 1px solid #ccc; padding: 8px 11px; margin: 0 0 18px 0; font-size: 10pt; }
                .meta span { display: inline-block; margin-right: 26px; }
                .q { margin: 0 0 16px 0; page-break-inside: avoid; }
                .qtext { font-weight: 600; margin-bottom: 5px; }
                .ans { border-left: 3px solid #2f8f7d; padding: 6px 10px; background: #f4f9f8; white-space: pre-wrap; }
                .none { border-left-color: #b06a00; background: #fdf6ec; color: #7a4a00; font-style: italic; }
                .correct { margin-top: 5px; padding: 6px 10px; background: #f2f2f2; font-size: 9.8pt; }
                .mark { font-weight: 700; }
                .ok { color: #1c7c4a; }
                .bad { color: #b32020; }
                table { border-collapse: collapse; width: 100%; margin-top: 6px; font-size: 9.8pt; }
                th, td { border: 1px solid #bbb; padding: 5px 7px; text-align: left; vertical-align: top; }
                th { background: #f2f2f2; }
                .foot { margin-top: 26px; padding-top: 8px; border-top: 1px solid #ccc; color: #666; font-size: 8.5pt; }
                </style></head><body>
                """);

            sb.Append($"<h1>{Esc(doc.Header.Name)} ({Esc(doc.ModuleCode)})</h1>");
            sb.Append($"<p class=\"sub\">{Esc(doc.Header.Series)} &middot; lab completion record</p>");

            sb.Append("<div class=\"meta\">");
            sb.Append($"<span><b>Name:</b> {Esc(string.IsNullOrWhiteSpace(learnerName) ? "(not signed in)" : learnerName!)}</span>");
            sb.Append($"<span><b>Exported:</b> {DateTime.Now:yyyy-MM-dd HH:mm}</span>");
            if (doc.Header.Difficulty != null) sb.Append($"<span><b>Difficulty:</b> {Esc(doc.Header.Difficulty)}</span>");
            if (doc.Header.Time != null) sb.Append($"<span><b>Time:</b> {Esc(doc.Header.Time)}</span>");
            sb.Append("</div>");

            int answered = 0, total = 0, correct = 0;
            var body = new StringBuilder();

            foreach (var section in manual.Sections)
            {
                var pieces = new StringBuilder();

                foreach (var block in section.Blocks)
                {
                    switch (block)
                    {
                        case QuestionListViewModel questions:
                            foreach (var q in questions.Questions)
                            {
                                total++;
                                bool has = !string.IsNullOrWhiteSpace(q.Answer);
                                if (has) answered++;
                                pieces.Append($"<div class=\"q\"><div class=\"qtext\">{Esc(q.Text)}</div>");
                                pieces.Append(has
                                    ? $"<div class=\"ans\">{Esc(q.Answer)}</div>"
                                    : "<div class=\"ans none\">Not answered</div>");
                                pieces.Append("</div>");
                            }
                            break;

                        case MultipleChoiceViewModel choices:
                            foreach (var q in choices.Questions)
                            {
                                total++;
                                if (q.IsAnswered) answered++;
                                if (q.IsAnswered && q.AnsweredCorrectly) correct++;

                                // Which option the learner picked: the one marked Wrong
                                // if they missed it, otherwise the revealed Right one.
                                var picked = q.Options.FirstOrDefault(o => o.State == ChoiceState.Wrong)
                                             ?? (q.AnsweredCorrectly
                                                 ? q.Options.FirstOrDefault(o => o.State == ChoiceState.Right)
                                                 : null);
                                var right = q.Options.FirstOrDefault(o => o.IsCorrect);

                                pieces.Append($"<div class=\"q\"><div class=\"qtext\">{Esc(q.Text)}</div>");
                                if (!q.IsAnswered)
                                {
                                    pieces.Append("<div class=\"ans none\">Not answered</div>");
                                }
                                else
                                {
                                    string mark = q.AnsweredCorrectly
                                        ? "<span class=\"mark ok\">Correct</span>"
                                        : "<span class=\"mark bad\">Not quite</span>";
                                    pieces.Append($"<div class=\"ans\">{mark} &mdash; answered " +
                                                  $"<b>{Esc(picked?.Letter ?? "?")}</b>. {Esc(picked?.Text ?? "")}</div>");
                                }
                                pieces.Append($"<div class=\"correct\"><b>Correct answer: {Esc(right?.Letter ?? "?")}</b>. " +
                                              $"{Esc(right?.Text ?? "")}<br/>{Esc(q.Explanation)}</div>");
                                pieces.Append("</div>");
                            }
                            break;

                        case ValueTableViewModel table:
                            pieces.Append("<div class=\"q\">");
                            if (!string.IsNullOrWhiteSpace(table.Heading))
                                pieces.Append($"<div class=\"qtext\">{Esc(table.Heading!)}</div>");
                            pieces.Append("<table><tr>");
                            foreach (var col in table.Columns)
                                pieces.Append($"<th>{Esc(col)}</th>");
                            pieces.Append("</tr>");
                            foreach (var row in table.Rows)
                            {
                                pieces.Append("<tr>");
                                foreach (var cell in row.FixedCells)
                                    pieces.Append($"<td><b>{Esc(cell)}</b></td>");
                                foreach (var cell in row.EditableCells)
                                {
                                    total++;
                                    bool has = !string.IsNullOrWhiteSpace(cell.Value);
                                    if (has) answered++;
                                    pieces.Append(has
                                        ? $"<td>{Esc(cell.Value)}</td>"
                                        : "<td style=\"color:#b06a00;font-style:italic\">&mdash;</td>");
                                }
                                pieces.Append("</tr>");
                            }
                            pieces.Append("</table></div>");
                            break;
                    }
                }

                if (pieces.Length > 0)
                {
                    body.Append($"<h2>{Esc(section.Title)}</h2>");
                    body.Append(pieces);
                }
            }

            if (body.Length == 0)
            {
                sb.Append("<p><i>This manual has no questions or fillable tables to record.</i></p>");
            }
            else
            {
                sb.Append("<div class=\"meta\">");
                sb.Append($"<span><b>Answered:</b> {answered} of {total}</span>");
                sb.Append($"<span><b>Multiple choice correct:</b> {correct}</span>");
                sb.Append("</div>");
                sb.Append(body);
            }

            sb.Append("<div class=\"foot\">Generated by the ProtoVerse app from the learner's in-app answers. " +
                      "Correct answers are shown because the app reveals them on answering, so nothing here is hidden from the learner.</div>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static string Esc(string s) => WebUtility.HtmlEncode(s);
    }
}
