using System.Collections.Generic;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// Content that is identical across every ProtoMod manual and should never be
    /// hand-authored per module. The template calls the assembly steps out as reusable
    /// boilerplate, and every filled manual in `PROTOVERSE/Manuals/` does in fact
    /// repeat them near-verbatim with only the module's name and code swapped.
    ///
    /// Keeping them here means a change to the setup procedure (say, ProtoCore gaining
    /// a second USB connector) is one edit rather than one per manual - which is a
    /// large part of why the data-driven approach is worth it at all. See
    /// EVALUATION.md.
    /// </summary>
    public static class ManualBoilerplate
    {
        /// <summary>The standard four assembly steps, wording taken from the filled
        /// manuals (Blinky F01 Gen2, Simple LED F02, Sensors I E03, DDS A01 all carry
        /// the same four with only the module name differing).</summary>
        public static StepsBlock AssemblySteps(string moduleName, string code) => new(
            new[]
            {
                new ManualStep($"Insert the {moduleName} ({code}) ProtoMod into any slot on your ProtoCore."),
                new ManualStep("Power the ProtoCore with USB-B."),
                new ManualStep("Open the ProtoVerse app (or use a serial monitor)."),
                new ManualStep($"Click Identify slots and confirm “{code}” appears in the correct slot.",
                    Observe: "The app now auto-identifies on connect, so the slot should already be populated before you click anything."),
            },
            Numbered: true,
            Heading: "Assembly steps");

        /// <summary>The Creative Challenge's closing reassurance. The template
        /// prescribes this verbatim in spirit for every module: success is a result the
        /// learner can explain, not one that matches a hidden target.</summary>
        public static CalloutBlock NoSingleCorrectAnswer => new(
            CalloutKind.Reassurance,
            "There's no single correct answer here. Success is a result you can explain, not one that matches a hidden target.",
            "No single correct answer");

        /// <summary>Marks content the source material doesn't contain yet. Used
        /// instead of writing plausible-sounding filler - this project's standing rule
        /// is that module content is quoted from something real or visibly absent, and
        /// a UI spike is not a reason to start inventing electronics teaching
        /// material.</summary>
        public static CalloutBlock Missing(string whatWouldGoHere) => new(
            CalloutKind.Placeholder,
            whatWouldGoHere,
            "Not written yet");

        /// <summary>Standard "You'll need" list. Only the board name varies.</summary>
        public static ChecklistBlock YoullNeed(string moduleName, string code) => new(
            new[]
            {
                $"{moduleName} ({code}) ProtoMod board",
                "ProtoCore board",
                "USB cable + computer",
                "Schematic reference (see Appendix C)",
            },
            Heading: "You'll need");
    }
}
