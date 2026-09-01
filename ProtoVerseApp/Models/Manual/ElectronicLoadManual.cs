using System.Collections.Generic;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// The Electronic Load (E05) manual, as in-app content.
    ///
    /// WRITTEN AGAINST THE REAL BOARD. `Electronic_Load_E02_Manual.docx` was supplied
    /// as a reference for the *kind* of material wanted - tone, pedagogy, the mix of
    /// theory and hands-on - and explicitly not as a source of truth (user, 2026-08-31).
    /// That matters here, because that document describes a closed-loop design with a
    /// power MOSFET, a 1 Ω sense resistor, and DAC/ADC readback of live voltage and
    /// current. The board this app actually talks to is open-loop: a bit-banged PWM
    /// signal into an op-amp, a 10 Ω sense resistor, no ADC anywhere on the current
    /// path. So the structure and voice below follow the reference; every technical
    /// claim describes the hardware that exists.
    ///
    /// It also means this manual can teach the thing that's actually interesting about
    /// this board - the gap between a commanded value and a measured one - instead of
    /// asking the learner to watch readouts it cannot produce.
    ///
    /// SOURCES for the technical content, all of it already verified against real
    /// hardware elsewhere in this project: CLAUDE.md's "Electronic Load's wire format
    /// is settled" section, CHANGELOG entries 36-42 (including a full 1-300 mA sweep
    /// against the bench board), and `ViewModels/ElectronicLoadViewModel.cs`.
    ///
    /// DELIBERATELY FEWER SECTIONS than the template's twelve. The reference manual's
    /// eight body sections plus three appendices made for a lot of headings on one
    /// scroll; this is five body sections and two appendices, with Setup and "now try
    /// this" merged, Observations carrying the value table, and the creative challenge
    /// and reflection questions sharing one "Go further" section. The schematic moved
    /// out of an appendix entirely and is linked from the top of the manual.
    /// </summary>
    public static class ElectronicLoadManual
    {
        public static ManualDocument Build() => new(
            ModuleCode: "E05",
            SchematicFile: "E05_schematic.pdf",
            Header: new ManualHeader(
                Series: "Explorers Series",
                Code: "E05",
                Name: "Electronic Load",
                Tagline: "Command a current and watch a real circuit obey - then find out why the board can't tell you whether it did.",
                Difficulty: "Intermediate",
                Time: "45-60 min",
                Prerequisites: "Simple LED (F02)"),
            SourceNote: "Written against the board's verified behaviour (CLAUDE.md, CHANGELOG 36-42, ElectronicLoadViewModel.cs). Electronic_Load_E02_Manual.docx was used as a reference for structure and tone, not for technical content - it describes a different, closed-loop design.",
            Sections: new[]
            {
                // ============================================================== 1
                new ManualSection("overview", "1. Overview", new ManualBlock[]
                {
                    new SubheadingBlock("Core concept"),
                    new ParagraphBlock(
                        "An electronic load is a resistor you set in software. Instead of choosing a resistance and letting Ohm's law decide the current, you name the current you want and the circuit arranges itself to draw it - turning the power it pulls into heat. That makes it a test instrument: it lets you ask a power source \"what do you do when I demand this much from you?\""),
                    new BulletsBlock(new[]
                    {
                        "You command a current from 0 to 300 mA, in milliamps, from software.",
                        "ProtoCore generates that command as a PWM signal, which an op-amp turns into a real current through a 10 Ω sense resistor.",
                        "The board is open-loop on this revision: nothing on it measures what the current actually is.",
                        "So it reports back the current you asked for and the PWM duty cycle it is driving - and never a measurement.",
                    }),
                    new ParagraphBlock(
                        "That last bullet is the one worth sitting with, and it is what this module is really about. Most instruments tell you what they measured. This one tells you what it was told to do. Learning to notice that difference - and knowing when it matters - is a habit that will outlast this board."),
                    new ImageBlock("E05_circuit.png",
                        "The whole circuit. Click for the full schematic, with revision and title block."),
                }),

                // ============================================================== 2
                new ManualSection("how", "2. How it works", new ManualBlock[]
                {
                    new SubheadingBlock("Making an analog voltage without a DAC"),
                    new ParagraphBlock(
                        "ProtoCore has no analog output on this pin, so it fakes one. A PWM signal switches cleanly between 0 V and 3.3 V, thousands of times a second. Spend 30% of each cycle high and the average value of that square wave is 30% of 3.3 V, or about 1 V. Filter away the switching and an averaged, steady-ish voltage is what remains - a voltage you set by choosing a ratio rather than by having a dedicated analog part."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "The PWM runs at 5 kHz. It was raised from 1 kHz specifically to reduce ripple on the op-amp's filtered input - a faster switch is easier to average smoothly. The trade was duty-cycle resolution, kept affordable by holding the interrupt rate fixed.",
                        "Tech note - why 5 kHz"),
                    new SubheadingBlock("From voltage to current"),
                    new ParagraphBlock(
                        "That averaged voltage drives an op-amp, which forces the same voltage to appear across a 10 Ω sense resistor. Once you fix the voltage across a known resistance, you have fixed the current through it - Ohm's law, used in reverse from how it's usually taught. Change the duty cycle and you change the voltage; change the voltage and you change the current."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "I = V ÷ R, with R = 10 Ω. To draw 100 mA you need 1.0 V across the sense resistor, which is a duty cycle of roughly 30% of a 3.3 V rail. In practice the duty comes out a little higher - see below.",
                        "Tech note - the arithmetic"),
                    new SubheadingBlock("Open loop, and what it costs"),
                    new ParagraphBlock(
                        "A closed-loop instrument measures its own output and corrects itself continuously - if the current drifts, it notices and pushes back. This board cannot. There is no sense amplifier feeding an ADC, no measurement of any kind on the current path. It computes a duty cycle from your request, drives it, and trusts the analog stage to do the rest."),
                    new ParagraphBlock(
                        "That trust is mostly justified, and firmware helps it along: rather than the textbook ratio, it applies a correction measured against real hardware, because the 3.3 V rail is never exactly 3.3 V and the op-amp is not ideal. But nothing in the loop closes. If the supply sags, if the resistor warms and drifts, if a connection is poor - the board neither knows nor tells you."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "Firmware's calibration constants are CAL_SLOPE_MV_PER_MA = 9.3828 and CAL_OFFSET_MV = 32.906. They overshoot the naive formula on purpose, to compensate for a rail sitting below its nominal value. At the 300 mA maximum the duty reports 95%, not 100% - expected, not a fault.",
                        "Tech note - real constants"),
                    new BulletsBlock(new[]
                    {
                        "A duty cycle plus a filter is a serviceable substitute for a DAC when you only need a slow-moving voltage.",
                        "Fixing a voltage across a known resistor is a way to fix a current - Ohm's law read backwards.",
                        "Open loop means \"set and hope\". It is cheaper, simpler, and puts the burden of verification on you.",
                        "A number an instrument reports is not automatically a number it measured.",
                    }, Heading: "Key takeaways"),
                }),

                // ============================================================== 3
                new ManualSection("setup", "3. Set up and try it", new ManualBlock[]
                {
                    // No board / ProtoCore / USB cable in this list, and no assembly
                    // steps section at all. In a printed manual those come first
                    // because the reader might not have started yet; in-app, this
                    // manual only exists *because* the board is already seated and
                    // talking, so telling them to plug it in is noise. Only what they
                    // might still be missing is listed.
                    new ChecklistBlock(new[]
                    {
                        "A multimeter that can read DC current - not optional for this module, since the board reports no measurements of its own",
                    }, Heading: "You'll also need"),
                    new FigureBlock("Multimeter in series with the load path"),
                    new StepsBlock(new[]
                    {
                        new ManualStep(
                            "Command 10 mA and click Apply. Watch the panel above this manual rather than the box you typed into.",
                            Observe: "Two values come back: the current, echoed exactly as you sent it, and a duty percent. Work out what duty the formula in Section 2 predicts for 10 mA. Is the reported figure higher or lower?"),
                        new ManualStep(
                            "Step up through 50 mA, 100 mA and 150 mA, pausing at each. Note the duty reported at every step."),
                        new ManualStep(
                            "Before you send the next one, predict the duty yourself. Then send it and check.",
                            Observe: "Is the gap between your prediction and the reported figure constant, or does it grow with current? That gap is the calibration correction, and its shape tells you what kind of error it was written to cancel."),
                        new ManualStep(
                            "Command 300 mA, then try 400 mA.",
                            Observe: "300 mA is the ceiling. Open the Traffic Log at the bottom of the window and look at what came back from the 400 mA attempt - the board rejects it outright rather than quietly clamping. Silent clamping would have been the more dangerous design. Why?"),
                        new ManualStep(
                            "Now put the meter in the load path and measure the actual current at two or three settings. Compare each reading against the number you commanded."),
                    }, Numbered: true, Heading: "Now try this"),
                }),

                // ============================================================== 4
                new ManualSection("observations", "4. What you should see", new ManualBlock[]
                {
                    new BulletsBlock(new[]
                    {
                        "The commanded current always echoes back exactly as sent - it is your own number returning to you.",
                        "The reported duty rises by roughly 1% for every 3 mA commanded.",
                        "At 300 mA the duty reports 95%, not 100%.",
                        "Commanding more than 300 mA produces an explicit error, not a clamped value.",
                        "Your meter reads close to the commanded current, but almost certainly not exactly.",
                    }, Heading: "What you should see"),
                    new SubheadingBlock("Why it works"),
                    new ParagraphBlock(
                        "Every one of those observations except the last comes from the command path alone. Firmware takes your current, runs it through the calibration to get a duty, drives the PWM at that duty, and reports both numbers back. Nothing in that round trip touches the load. It can reject 400 mA without measuring anything, because the limit is a constant it already knows."),
                    new ParagraphBlock(
                        "The last observation is the only one that required an instrument, and it is the only one that tells you what the circuit actually did. Everything else was the board describing its own intentions."),
                    new CalloutBlock(CalloutKind.Observe,
                        "Where your measured current and your commanded current disagree, the board will never say so. Which of the two would you write down as \"the current\", and what would you have to do to earn the right to trust it?"),
                }),

                // ============================================================== 5
                new ManualSection("further", "5. Go further", new ManualBlock[]
                {
                    new SubheadingBlock("Creative challenge"),
                    new ParagraphBlock(
                        "Design a short experiment that measures something the board doesn't report - a property you have to derive from a series of readings rather than read off a screen."),
                    new BulletsBlock(new[]
                    {
                        "Calibrate the board against your meter: sweep the commanded current, record what you actually measure at each point, and produce a correction curve. How linear is the error?",
                        "Find where it stops behaving. Push toward 300 mA and watch whether measured current keeps tracking commanded current as faithfully at the top of the range as at the bottom.",
                        "Work out the duty resolution by hand: command currents 1 mA apart and find the smallest change that moves the reported duty at all. What does that tell you about the finest current step this board can really make?",
                    }),
                    ManualBoilerplate.NoSingleCorrectAnswer,
                    new SubheadingBlock("Follow-up & reflection questions"),
                    new QuestionsBlock("reflection", new[]
                    {
                        "The board reports a current and a duty cycle. Which of those two is a measurement, and which is a restatement of what you asked for?",
                        "Why does fixing a voltage across a known resistor also fix the current through it?",
                        "Firmware's calibration deliberately overshoots the textbook duty for a given current. What real-world imperfection is that overshoot compensating for?",
                        "This board rejects a 400 mA request instead of clamping it to 300 mA. Why might an explicit refusal be safer than a silent adjustment?",
                        "If you were asked to make this a closed-loop instrument that could report its true current, what would you have to add - and what would become possible that isn't now?",
                    }),
                }),

                // ====================================================== Appendix A
                new ManualSection("appendix-a", "Appendix A - Answer key", new ManualBlock[]
                {
                    new ParagraphBlock("Attempt the reflection questions before reading this."),
                    new BulletsBlock(new[]
                    {
                        "Neither is a measurement of the load. The current is your own commanded value echoed back; the duty cycle is real in that it is genuinely what firmware is driving, but it describes the board's output, not the current actually flowing.",
                        "Ohm's law relates the three quantities, so fixing any two fixes the third. The resistor's value is fixed by construction and the op-amp holds the voltage across it fixed, which leaves the current with only one value it can take.",
                        "Mainly a supply rail that sits below its nominal 3.3 V, plus non-ideal behaviour in the op-amp stage. A duty computed from an assumed 3.3 V would under-deliver against a rail that is actually lower, so the correction pushes it up.",
                        "A clamped value would leave the caller believing they got what they asked for. An error makes the disagreement visible at the moment it happens, which is the only point at which it is cheap to notice - especially on a board that reports no measurements to contradict the assumption later.",
                        "You would need a sense amplifier across the existing resistor feeding an ADC, and firmware to close the loop by comparing measurement to setpoint. That would make the board able to report true current, correct for drift and load changes on its own, and detect faults such as an open circuit - none of which it can do today.",
                    }),
                }, IsAppendix: true, IsSpoiler: true),

                // ====================================================== Appendix B
                new ManualSection("appendix-b", "Appendix B - Facilitator notes", new ManualBlock[]
                {
                    new SubheadingBlock("Timing guide"),
                    new ParagraphBlock(
                        "About 45-60 minutes. The stepped-current exercise in Section 3 takes longest, particularly for learners meeting a multimeter's current mode for the first time - budget time there rather than in the theory."),
                    new SubheadingBlock("Common misconceptions"),
                    new BulletsBlock(new[]
                    {
                        "Reading the echoed current as a measurement. This is the central point of the module and it catches experienced engineers too - the number looks like telemetry because it arrives from the hardware.",
                        "Expecting the load to push current into a source. It only sinks; it can pull current out of a source, never supply it.",
                        "Assuming the reported duty is wrong because it doesn't match the textbook formula. The calibration overshoot is deliberate and documented.",
                    }),
                    new SubheadingBlock("Extension ideas"),
                    new BulletsBlock(new[]
                    {
                        "Have learners compare their own calibration curves. Boards and meters differ, and the spread is a good conversation about tolerance.",
                        "Ask what a closed-loop version would cost in parts and complexity, then discuss why a teaching board might reasonably choose open loop anyway.",
                    }),
                }, IsAppendix: true),
            });
    }
}
