using System.Collections.Generic;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// The Electronic Load (E05) manual, as in-app content.
    ///
    /// NO WORD MANUAL EXISTS FOR THIS BOARD. Unlike F01/F02/E03/A01, there is nothing
    /// in `PROTOVERSE/Manuals/` for E05 - so this is assembled from the only writing
    /// that does exist about it: this repo's own CLAUDE.md (the "Electronic Load's wire
    /// format is settled" section), CHANGELOG entries 36-42, and
    /// `ViewModels/ElectronicLoadViewModel.cs`. That material is unusually technical
    /// for a learner manual, but it is real and hardware-confirmed, which matters more
    /// here than tone.
    ///
    /// Sections with no source material at all - real-world applications, the creative
    /// challenge's starting ideas, reflection questions, the answer key, facilitator
    /// notes - are explicit <see cref="ManualBoilerplate.Missing"/> placeholders rather
    /// than invented prose. That is a deliberate constraint, not laziness: the project
    /// rule is that module content is quoted from something real or visibly absent, and
    /// a UI spike is not a licence to start writing electronics curriculum. The upside
    /// for the evaluation is that it demonstrates exactly how a half-written manual
    /// looks in the UI, which is the state most manuals will actually be in during
    /// rollout.
    ///
    /// The value table below is the reason this module was chosen for the spike: E05 is
    /// the one board where the learner has a dial to turn and a number to read back, so
    /// it genuinely exercises the fillable-table pattern the evaluation is meant to
    /// assess. (Blinky F01, which the original spec named, has no adjustable values and
    /// no such table anywhere in its manual.)
    /// </summary>
    public static class ElectronicLoadManual
    {
        public static ManualDocument Build() => new(
            ModuleCode: "E05",
            Header: new ManualHeader(
                Series: "Explorers Series",
                Code: "E05",
                Name: "Electronic Load",
                Tagline: "Command a current, watch a real circuit obey - and find out what the board can't tell you.",
                // Only F01's Gen2 manual states difficulty/time anywhere in this
                // project; nothing states them for E05, so they stay null and the
                // header renders them as unset rather than guessing.
                Difficulty: null,
                Time: null,
                Prerequisites: null),
            SourceNote: "Assembled from this repo's own documentation (CLAUDE.md, CHANGELOG 36-42, ElectronicLoadViewModel.cs) - no Word manual exists for E05. Sections without source material are marked as not written yet.",
            Sections: new[]
            {
                // ---------------------------------------------------------- 1
                new ManualSection("overview", "Overview", new ManualBlock[]
                {
                    new ParagraphBlock("Module name: Electronic Load (E05)"),
                    new SubheadingBlock("Core concept"),
                    new ParagraphBlock(
                        "A programmable current sink: ProtoCore commands a current between 0 and 300 mA and the board draws it, so you can load a supply or source and watch what happens."),
                    new BulletsBlock(new[]
                    {
                        "Commanded current is set in milliamps, from software, over the ProtoVerse wire protocol.",
                        "A bit-banged PWM signal drives an op-amp that forces that current through a 10 Ω sense resistor.",
                        "The board is open-loop on this revision - there is no ADC feedback path anywhere on it.",
                        "Because of that, it reports back the current you asked for and the PWM duty it is driving, never a measurement.",
                    }),
                    new ParagraphBlock(
                        "That last point is the interesting one, and it is what this module is really about. Most instruments tell you what they measured. This one tells you what it was told to do, and leaves verifying it to you."),
                }),

                // ---------------------------------------------------------- 2
                new ManualSection("theory", "Background & theory", new ManualBlock[]
                {
                    new SubheadingBlock("Turning a duty cycle into a current"),
                    new ParagraphBlock(
                        "ProtoCore has no analog output, so it makes one. A PWM signal switches between 0 V and 3.3 V, and a low-pass filter averages that square wave into a steady voltage proportional to how much of each cycle is spent high - the duty cycle. Feed that averaged voltage into an op-amp configured to hold it across a sense resistor, and the current through the resistor follows Ohm's law."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "I·R = V, and V/VDD = duty. With R = 10 Ω and VDD = 3.3 V nominal, 100 mA needs 1.0 V across the sense resistor, which is a duty cycle of about 30%.",
                        "Tech note - the calibration"),
                    new SubheadingBlock("Why the duty cycle isn't quite the formula"),
                    new ParagraphBlock(
                        "The clean formula above assumes VDD is exactly 3.3 V and the op-amp is ideal. Neither is true on a real bench. Firmware therefore applies a measured linear correction rather than the textbook ratio, which is why the duty you see reported runs slightly above what the formula predicts - the correction has to overshoot to compensate for a supply rail sitting below its nominal value."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "Firmware's calibration constants are CAL_SLOPE_MV_PER_MA = 9.3828 and CAL_OFFSET_MV = 32.906. At the 300 mA maximum the duty caps at 95%, not 100% - expected, not a fault.",
                        "Tech note - real constants"),
                    new SubheadingBlock("Open loop, and what that costs"),
                    new ParagraphBlock(
                        "A closed-loop load measures its own current and corrects itself. This board cannot: there is no sense amplifier and no ADC input on the current path. It sets a duty cycle and trusts the analog stage. If the supply sags, if the resistor drifts with temperature, if the op-amp offsets - the board neither knows nor reports it."),
                    new BulletsBlock(new[]
                    {
                        "The current is set, not measured - the number the app shows is the commanded value echoed back.",
                        "The duty percent it reports is real, in the sense that it is genuinely what firmware is driving.",
                        "Verifying the actual current requires an external instrument. That is the exercise.",
                    }, Heading: "Key takeaways"),
                }),

                // ---------------------------------------------------------- 3
                new ManualSection("applications", "Real-world applications", new ManualBlock[]
                {
                    ManualBoilerplate.Missing(
                        "2-4 short paragraphs on where programmable loads show up in practice - battery capacity testing, power-supply characterisation, and so on - ending on a transition into Setup & Testing. Nothing on this has been written for E05 in any document in this project, so nothing is quoted here."),
                }),

                // ---------------------------------------------------------- 4
                new ManualSection("setup", "Setup & testing", new ManualBlock[]
                {
                    ManualBoilerplate.YoullNeed("Electronic Load", "E05"),
                    new CalloutBlock(CalloutKind.TechNote,
                        "A multimeter in series with the load, or a bench supply with a current readout. This module is largely pointless without one - the board cannot tell you what it is actually drawing.",
                        "You'll also need, for this module specifically"),
                    ManualBoilerplate.AssemblySteps("Electronic Load", "E05"),
                    new FigureBlock("ProtoCore with the Electronic Load (E05) ProtoMod installed, multimeter in series with the load path"),
                    new StepsBlock(new[]
                    {
                        new ManualStep(
                            "Set the commanded current to 10 mA and click Apply. Watch the panel above this manual, not just the number you typed.",
                            Observe: "Two values come back: the commanded current, echoed, and the duty percent. Does the duty match the roughly 3% the formula predicts, or is it a little higher?"),
                        new ManualStep(
                            "Step the commanded current up: 50 mA, 100 mA, 150 mA. Record the duty reported at each."),
                        new ManualStep(
                            "Work out the duty the formula predicts for each of those currents before you look at what the board reported.",
                            Observe: "The gap between predicted and reported is the calibration correction doing its job. Is the gap constant, or does it grow with current?"),
                        new ManualStep(
                            "Command 300 mA, then try commanding 400 mA.",
                            Observe: "300 mA is the maximum. What happens to the frame at 400 mA? Open the Traffic Log at the bottom of the window - the board rejects it with an explicit error rather than clamping silently."),
                        new ManualStep(
                            "If you have a meter in the load path, measure the actual current at two or three settings and compare it to what you commanded."),
                    }, Numbered: true, Heading: "Now try this"),
                }),

                // ---------------------------------------------------------- 5
                new ManualSection("observations", "Observations & results", new ManualBlock[]
                {
                    new BulletsBlock(new[]
                    {
                        "The commanded current always echoes back exactly as sent - it is your own number returning, not a reading.",
                        "The reported duty rises by roughly 1% for every 3 mA commanded.",
                        "At 300 mA the duty reports 95%, not 100%.",
                        "Commanding above 300 mA produces an error response, not a clamped value.",
                    }, Heading: "What you should see"),
                    new SubheadingBlock("Why it works"),
                    new ParagraphBlock(
                        "Every number in that list comes from the command path only. Firmware receives a current, runs it through the calibration to get a duty, drives the PWM at that duty, and reports both back. Nothing in that loop involves measuring the load. An error at 400 mA is firmware refusing a value it knows is out of range - which it can do without measuring anything, because the limit is a constant."),
                    // The block this whole module was chosen to exercise: a real
                    // predicted-vs-observed comparison the learner fills in.
                    new ValueTableBlock(
                        Id: "duty-sweep",
                        Columns: new[] { "Commanded current", "Duty you predict", "Duty reported", "Measured current", "Notes" },
                        Rows: new[]
                        {
                            new[] { "10 mA" },
                            new[] { "50 mA" },
                            new[] { "100 mA" },
                            new[] { "150 mA" },
                            new[] { "300 mA" },
                        },
                        Heading: "Record what you find"),
                    new CalloutBlock(CalloutKind.Observe,
                        "If your measured current disagrees with what you commanded, the board will never tell you. Which of the two numbers would you trust, and why?"),
                }),

                // ---------------------------------------------------------- 6
                new ManualSection("challenge", "Creative challenge", new ManualBlock[]
                {
                    ManualBoilerplate.Missing(
                        "2-4 unequal starting ideas for open-ended work with a programmable load. Nothing has been written for E05, and this section is required by the template for every module - so it is a genuine gap in the content, not just in this demo."),
                    ManualBoilerplate.NoSingleCorrectAnswer,
                }),

                // ---------------------------------------------------------- 7
                new ManualSection("questions", "Follow-up & reflection questions", new ManualBlock[]
                {
                    ManualBoilerplate.Missing(
                        "4-6 questions mixing recall, reasoning and at least one open-ended prompt. Not written for E05. The answer field below is wired up and would persist per learner - it is showing the interaction, with one placeholder question standing in for the real set."),
                    new QuestionsBlock("reflection", new[]
                    {
                        "[Placeholder question, to show the answer field] This board reports a current it never measured. Where else in electronics does an instrument report a commanded value as though it were an observation, and how would you tell the difference?",
                    }),
                }),

                // ---------------------------------------------------------- 8
                new ManualSection("next", "Future ProtoMods for you", new ManualBlock[]
                {
                    ManualBoilerplate.Missing(
                        "One paragraph pointing at the next module. No document in this project states what follows E05 - and progression links are never inferred from circuit-code order here, so none is offered."),
                }),

                // ---------------------------------------------------- Appendix A
                new ManualSection("appendix-a", "Appendix A - Answer key", new ManualBlock[]
                {
                    ManualBoilerplate.Missing(
                        "Answers to the reflection questions above. Not written, because the questions aren't either."),
                }, IsAppendix: true, IsSpoiler: true),

                // ---------------------------------------------------- Appendix B
                new ManualSection("appendix-b", "Appendix B - Facilitator notes", new ManualBlock[]
                {
                    ManualBoilerplate.Missing(
                        "Timing guide, common misconceptions, and extension ideas. Not written for E05. One misconception is already documented elsewhere in this project and would belong here: learners (and engineers) read the echoed current as a measurement."),
                }, IsAppendix: true),

                // ---------------------------------------------------- Appendix C
                new ManualSection("appendix-c", "Appendix C - Schematic reference", new ManualBlock[]
                {
                    new ParagraphBlock(
                        "A bit-banged PWM signal from ProtoCore drives an op-amp that forces current through a 10 Ω sense resistor. This board revision is open-loop - there is no ADC feedback path, so it reports the commanded current and the PWM duty cycle it is driving, never a measurement."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "PWM runs at 5 kHz. It was raised from 1 kHz to reduce ripple on the op-amp's filtered input, trading duty-step resolution to keep the interrupt rate fixed.",
                        "Tech note"),
                    new FigureBlock("Electronic Load (E05) schematic - Protomod_ElectronicLoad.pdf exists in PROTOVERSE/Finished Modules/PC01_E05_ProtoMod_ElectronicLoad/Rev01 but is not bundled with the app"),
                }, IsAppendix: true),
            });
    }
}
