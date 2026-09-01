using System.Collections.Generic;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// The Electronic Load manual, as in-app content.
    ///
    /// SOURCE: `Electronic_Load_E02_Manual.docx`, supplied 2026-08-31. Transcribed
    /// faithfully - every paragraph, callout, question and answer below is that
    /// document's wording, not this app's. It is written against the Gen2 template and
    /// is complete: all twelve sections, no gaps. (An earlier version of this file was
    /// six-twelfths placeholder, assembled from this repo's own notes because no manual
    /// existed at the time.)
    ///
    /// RESOLVED - the module code. The source `.docx` says "E02" throughout; the user
    /// confirmed (2026-08-31) that this is a typo and the module is E05, which is what
    /// every other source in the project already said: `ProtoModBoardCatalog` maps
    /// <see cref="ProtoModId.ElectronicLoad"/> to circuit code "E05", the hardware
    /// lives in `Finished Modules/PC01_E05_ProtoMod_ElectronicLoad`, and the Library
    /// catalog lists it as E05. The transcription below therefore says E05 wherever the
    /// document said E02 - the one place this file knowingly departs from its source,
    /// and only because the departure was confirmed.
    ///
    /// **The Word document still has the typo.** It should be corrected at the source,
    /// or a future `.docx` -> content converter will faithfully reintroduce E02. This
    /// is also the exact class of error the converter should validate for: a manual
    /// whose stated module code matches no board in `ProtoModBoardCatalog`.
    ///
    /// STILL UNRESOLVED: THE MANUAL AND THE BOARD DESCRIBE DIFFERENT CIRCUITS. Flagged
    /// here, and surfaced in the UI, rather than quietly reconciled - it needs a human
    /// decision. This manual describes a
    /// power MOSFET with an op-amp feedback loop, a 1 Ω sense resistor, and ProtoCore's
    /// DAC setting the target while its ADC reads back live voltage and current for
    /// on-screen monitoring. The board this app actually talks to is open-loop:
    /// bit-banged PWM into an op-amp, a 10 Ω sense resistor, and no ADC feedback path
    /// at all - which is why `ElectronicLoadViewModel` reports commanded current and
    /// PWM duty rather than measurements, and why the user chose to remove that panel's
    /// chart entirely rather than plot numbers the hardware cannot produce (CLAUDE.md;
    /// CHANGELOG 36).
    ///
    /// This matters for the learner, not just for tidiness: sections 4 and 5 ask them
    /// to watch voltage sag on screen while current holds steady, and this board
    /// revision cannot show them either quantity. The manual's own Appendix C already
    /// flags its component values as provisional, so the likeliest explanation is that
    /// it describes an intended or revised board rather than the Rev01 hardware on the
    /// bench - but that is a guess, and this project doesn't resolve hardware questions
    /// by guessing.
    ///
    /// The <see cref="CalloutKind.Discrepancy"/> blocks below are the only text here
    /// that isn't from the manual. They are attributed as app-side notes wherever they
    /// appear.
    /// </summary>
    public static class ElectronicLoadManual
    {
        /// <summary>The app-side mismatch note, shown at the points where following the
        /// manual would have the learner looking for something this board can't
        /// display.</summary>
        private static CalloutBlock HardwareMismatch(string detail) => new(
            CalloutKind.Discrepancy,
            detail + "  (Note from the ProtoVerse app, not from the manual.)",
            "This manual doesn't match the board in your slot");

        public static ManualDocument Build() => new(
            ModuleCode: "E05",
            Header: new ManualHeader(
                Series: "Explorers Series",
                // E05, not the document's "E02" - confirmed typo, see the class remarks.
                Code: "E05",
                Name: "Electronic Load",
                Tagline: "Draw a precise, adjustable current from any DC source, and watch feedback control keep it steady as voltage sags.",
                Difficulty: "Intermediate",
                Time: "45-60 min",
                Prerequisites: "Simple LED (F02)"),
            SourceNote: "Transcribed from Electronic_Load_E02_Manual.docx (2026-08-31). The document's module code \"E02\" is a confirmed typo and is shown here as E05. One conflict with the physical board is still unresolved and flagged in-line: the manual describes a closed-loop DAC/ADC design, where the board this app talks to is open-loop PWM with no measurement path.",
            Sections: new[]
            {
                // ---------------------------------------------------------- 1
                new ManualSection("overview", "1. Overview", new ManualBlock[]
                {
                    new ParagraphBlock("Module name: Electronic Load (E05)"),
                    new SubheadingBlock("Core concept"),
                    new ParagraphBlock(
                        "A power MOSFET, driven by an op-amp feedback loop, can act as an adjustable current sink - pulling a precise, chosen current from any DC source and turning the power into heat, so you can test how that source behaves under real load."),
                    new BulletsBlock(new[]
                    {
                        "A power MOSFET “pass element” that behaves like a resistor whose value you set electronically, not by swapping parts",
                        "A current-sense resistor and op-amp feedback loop that hold the load current steady even as the input voltage changes",
                        "ProtoCore's DAC sets the target current; ProtoCore's ADC reads back live voltage and current for on-screen monitoring",
                        "A heatsinked layout rated for modest continuous power dissipation - enough to safely test small batteries, USB supplies, and voltage regulators",
                    }),
                    HardwareMismatch(
                        "The third bullet describes a board that measures what it is drawing. This one doesn't: it is open-loop, with no ADC on the current path, so the app can show you the current you asked for and the PWM duty it is driving - never a reading. Expect no live voltage or current anywhere in the panel above."),
                    new ParagraphBlock(
                        "By connecting this ProtoMod to any DC power source and watching how it holds current constant while voltage sags underneath it, you'll directly observe source impedance, power dissipation, and closed-loop feedback control - the same core ideas behind every real bench electronic load and battery tester."),
                }),

                // ---------------------------------------------------------- 2
                new ManualSection("theory", "2. Background & theory", new ManualBlock[]
                {
                    new SubheadingBlock("What is an electronic load?"),
                    new ParagraphBlock(
                        "A resistor is a passive load: plug it into a source and the current it draws depends entirely on the source's voltage (Ohm's law, I = V / R). An electronic load flips that relationship around. Instead of a fixed resistance, you choose a target current, and the circuit actively adjusts its own effective resistance - moment to moment - to keep pulling exactly that much current, no matter what the source's voltage does."),
                    new ParagraphBlock(
                        "That's what makes it useful as a test instrument: it lets you ask a power source “what do you do when I demand this much current from you?” and get a clean, repeatable answer."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "I_load = V_sense ÷ R_sense. With a 1 Ω sense resistor, 100 mV across it means 100 mA of load current. Power dissipated as heat: P = V_in × I_load - at 5 V and 200 mA, that's 1 W the MOSFET has to absorb.",
                        "Tech note"),
                    new SubheadingBlock("Constant current via feedback (the control loop)"),
                    new ParagraphBlock(
                        "An op-amp compares two voltages: the voltage across the sense resistor (which tracks actual current) and a setpoint voltage coming from ProtoCore's DAC (which represents your target current). If actual current is too low, the op-amp drives the MOSFET's gate more open, letting more current through; if it's too high, it closes the gate down. This happens continuously and automatically - it's a negative feedback loop, the same principle behind cruise control or a thermostat, just applied to current instead of speed or temperature."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "The MOSFET's gate voltage is not something you set directly - it's whatever the op-amp decides, many thousands of times per second, to keep V_sense equal to your setpoint. You control the target; the loop finds the gate voltage that gets there.",
                        "Tech note"),
                    HardwareMismatch(
                        "The board in your slot uses a 10 Ω sense resistor, not 1 Ω, and its setpoint comes from a bit-banged PWM signal rather than a DAC. Both tech notes above are worth understanding as theory; neither describes the numbers this hardware runs on."),
                    new BulletsBlock(new[]
                    {
                        "An electronic load doesn't supply power - it consumes it and converts it to heat, which is why it needs a heatsink.",
                        "Feedback control (comparing a measured value to a setpoint, continuously) is what makes the current constant instead of Ohm's-law-dependent.",
                        "Power = Voltage × Current isn't just a formula here - it's heat you can watch build up on the heatsink in real time.",
                    }, Heading: "Key takeaways"),
                }),

                // ---------------------------------------------------------- 3
                new ManualSection("applications", "3. Real-world applications", new ManualBlock[]
                {
                    new ParagraphBlock(
                        "Constant-current discharge testing is exactly how battery capacity ratings (mAh) are measured: discharge a cell at a known, steady current and time how long it takes to hit a cutoff voltage. Every battery datasheet you've ever seen was produced with a circuit doing what this ProtoMod does."),
                    new ParagraphBlock(
                        "Engineers use electronic loads to answer a simple but critical question about any power supply or voltage regulator: does it hold its output voltage steady when something actually draws current from it? A supply that looks perfect with nothing connected can sag badly under real load - and an electronic load is how you'd find that out before shipping a product."),
                    new ParagraphBlock(
                        "Reviewers and hobbyists use small programmable loads to check whether USB chargers and power banks actually deliver the current they claim on the box - a very common real-world use of exactly this circuit, at a slightly larger scale."),
                    new ParagraphBlock("Let's get started!"),
                }),

                // ---------------------------------------------------------- 4
                new ManualSection("setup", "4. Setup & testing", new ManualBlock[]
                {
                    new ChecklistBlock(new[]
                    {
                        "Electronic Load (E05) ProtoMod board",
                        "ProtoCore board",
                        "USB cable + computer",
                        "A DC power source to test - a AA/AAA battery, USB power bank, or bench supply, no higher than 5 V for this exercise",
                        "Multimeter (optional, for cross-checking the app's voltage/current readout)",
                    }, Heading: "You'll need"),
                    new StepsBlock(new[]
                    {
                        new ManualStep("Insert the Electronic Load (E05) ProtoMod into any slot on your ProtoCore."),
                        new ManualStep("Power the ProtoCore with USB-B."),
                        new ManualStep("Open the ProtoVerse app (or use a serial monitor)."),
                        new ManualStep("Click Identify slots and confirm “E05” appears in the correct slot."),
                    }, Numbered: true, Heading: "Assembly steps"),
                    HardwareMismatch(
                        "The multimeter listed as optional above isn't, on this board revision: it's the only way to see the voltage and current this exercise asks you to watch. The panel above shows the current you commanded and the PWM duty being driven - neither is a measurement."),
                    new FigureBlock("Figure 1 - Electronic Load board, input terminals and heatsink visible"),
                    new StepsBlock(new[]
                    {
                        new ManualStep(
                            "Connect a single AA battery (or similarly low-power source) to the Electronic Load's input terminals. In the app, set the target current to a low value - 50 mA - and enable the load.",
                            Observe: "The app should immediately show a current reading close to 50 mA and a voltage reading close to the battery's normal resting voltage (about 1.5 V for a fresh AA). If current reads near zero, check polarity and that the load is enabled."),
                        new ManualStep(
                            "Raise the current setpoint in steps - 50 mA, then 100 mA, then 200 mA - pausing at each step. Change only the current setpoint; leave everything else connected exactly as it was."),
                        new ManualStep(
                            "Before increasing current once more, predict what you think will happen to the voltage reading. Then raise the setpoint and check whether your prediction held."),
                    }, Numbered: true, Heading: "Now try this"),
                }),

                // ---------------------------------------------------------- 5
                new ManualSection("observations", "5. Observations & results", new ManualBlock[]
                {
                    new BulletsBlock(new[]
                    {
                        "Voltage drops as current increases, even though the current itself stays close to whatever you set it to.",
                        "The heatsink and MOSFET area of the board become noticeably warm at higher current settings - that warmth is the power you calculated in the Tech note, made physical.",
                        "The current reading tracks your setpoint closely across the whole range you tried, not just at one value.",
                    }, Heading: "What you should see"),
                    new SubheadingBlock("Why it works"),
                    new ParagraphBlock(
                        "Every real power source has some internal resistance - think of a battery as a perfect voltage source with a small resistor built in. When you draw more current, more voltage gets “lost” across that internal resistance before it ever reaches your load, so the voltage you measure at the terminals sags. This isn't a flaw in the Electronic Load; it's the Electronic Load successfully revealing a property of the source that was always there but invisible with nothing connected."),
                    new ParagraphBlock(
                        "The reason the current itself stays steady through all of this is the feedback loop from Section 2: the op-amp is continuously re-adjusting the MOSFET's gate to compensate for the changing voltage, so that V_sense - and therefore I_load - keeps matching your setpoint regardless of what the source's voltage is doing underneath it."),
                    new ParagraphBlock(
                        "Guidance: log each setpoint you tried alongside the voltage the app reported - tested current / predicted voltage / observed voltage / notes - as a simple table in your own notebook or the app if it supports one. That table becomes the evidence for Section 6."),
                    // The manual asks for exactly this table and wonders whether the app
                    // supports one. It does.
                    new ValueTableBlock(
                        Id: "sag-log",
                        Columns: new[] { "Tested current", "Predicted voltage", "Observed voltage", "Notes" },
                        Rows: new[]
                        {
                            new[] { "50 mA" },
                            new[] { "100 mA" },
                            new[] { "200 mA" },
                        },
                        Heading: "Log your readings"),
                }),

                // ---------------------------------------------------------- 6
                new ManualSection("challenge", "6. Creative challenge", new ManualBlock[]
                {
                    new ParagraphBlock(
                        "Using only what you learned in this ProtoMod, design a short experiment that measures some real characteristic of a power source - not just its current or voltage, but something you have to derive from a series of readings."),
                    new BulletsBlock(new[]
                    {
                        "Estimate a battery's internal resistance by taking voltage readings at two different current setpoints and using the change in voltage divided by the change in current (ΔV / ΔI).",
                        "Build a simple runtime/capacity test: hold the load at a fixed current, time how long the source takes to sag to a chosen cutoff voltage, and calculate the capacity in mAh from the current and the time.",
                    }),
                    new CalloutBlock(CalloutKind.Reassurance,
                        "Both ideas above are starting points, not the target. What matters is that your method is your own reasoning about cause and effect - not whether it matches anyone else's numbers.",
                        "There's no single correct answer here"),
                }),

                // ---------------------------------------------------------- 7
                new ManualSection("questions", "7. Follow-up & reflection questions", new ManualBlock[]
                {
                    new QuestionsBlock("reflection", new[]
                    {
                        "What component in this ProtoMod acts as the adjustable “resistor,” and what does it do with the power it consumes?",
                        "What role does the current-sense resistor play in the control loop?",
                        "Why does a power source's voltage drop as you draw more current from it, even though the Electronic Load is asking for a constant current, not a changing one?",
                        "Why might a full-size benchtop electronic load need a heatsink rated for tens or hundreds of watts, while this ProtoMod's is much smaller?",
                        "If you were designing a larger electronic load capable of testing something like a car battery, what would have to change about this design, and why?",
                    }),
                }),

                // ---------------------------------------------------------- 8
                new ManualSection("next", "8. Future ProtoMods for you", new ManualBlock[]
                {
                    new ParagraphBlock(
                        "Once you're comfortable pulling a controlled current and reading back the result, the DDS / Sinusoid Generator (A01) is a natural next step: it moves from a control loop that holds a value steady to one that actively generates a changing signal, and both ProtoMods share the same instinct for using ProtoCore's DAC/ADC to set and measure real electrical quantities. Combining the two later - driving a changing signal into a circuit while the Electronic Load holds a steady demand on its output - is exactly the kind of interaction ProtoMods are designed to build toward."),
                }),

                // ---------------------------------------------------- Appendix A
                new ManualSection("appendix-a", "Appendix A - Answer key", new ManualBlock[]
                {
                    new ParagraphBlock("Attempt the Follow-Up Questions before reading this."),
                    new BulletsBlock(new[]
                    {
                        "The power MOSFET acts as the adjustable resistor. It doesn't reuse or store the power it consumes - it dissipates it as heat, which is why the board needs a heatsink.",
                        "The current-sense resistor converts the load current into a small, measurable voltage. The op-amp compares that voltage to your setpoint to decide how much to open or close the MOSFET - without it, there'd be nothing for the feedback loop to measure.",
                        "Every real source has some internal resistance. Drawing more current means more voltage is lost across that internal resistance before it reaches the terminals, so terminal voltage sags - even though the current itself is being held constant by the feedback loop.",
                        "Power dissipation scales with both voltage and current (P = V × I). A benchtop load tests supplies at much higher voltages and currents than this ProtoMod, so it has to shed far more heat - hence the much larger heatsink (often with a fan).",
                        "A car-battery-capable load would need higher-current-rated MOSFETs, a beefier current-sense path, active cooling instead of a passive heatsink, and safety features like reverse-polarity and over-temperature protection - all scaled-up versions of exactly what's already on this board.",
                    }),
                }, IsAppendix: true, IsSpoiler: true),

                // ---------------------------------------------------- Appendix B
                new ManualSection("appendix-b", "Appendix B - Facilitator notes", new ManualBlock[]
                {
                    new SubheadingBlock("Timing guide"),
                    new ParagraphBlock(
                        "About 45-60 minutes total. Learners new to reading a multimeter or interpreting sag under load tend to move slower through Section 4's stepped-current exercise - budget extra time there rather than in the theory sections, which most learners move through quickly if Simple LED (F02) is fresh."),
                    new SubheadingBlock("Common misconceptions"),
                    new BulletsBlock(new[]
                    {
                        "Thinking the Electronic Load “produces” current like a power supply does. It only sinks/consumes current - it can never push current into a source, only pull it out.",
                        "Assuming voltage should stay perfectly constant regardless of load. Voltage sag under load is normal and expected for any real source; a source that never sags simply hasn't been tested hard enough yet.",
                    }),
                    new SubheadingBlock("Extension ideas"),
                    new BulletsBlock(new[]
                    {
                        "Pair with the DDS / Sinusoid Generator ProtoMod to test a power supply's transient response - step the load current suddenly and watch (via ProtoCore's ADC) how quickly the supply's voltage recovers.",
                        "Pair with the Accelerometer + Temperature ProtoMod to correlate heatsink temperature rise directly against calculated power dissipation over time.",
                    }),
                }, IsAppendix: true),

                // ---------------------------------------------------- Appendix C
                new ManualSection("appendix-c", "Appendix C - Schematic reference", new ManualBlock[]
                {
                    new ParagraphBlock(
                        "Referenced from Setup & Testing. Component values shown in this manual (1 Ω sense resistor, target current range, continuous power rating) are illustrative pending confirmation against the Electronic Load ProtoMod's real schematic - update this appendix and the Tech notes in Section 2 once that schematic is verified, the same way the Blinky (F01) manual's resistor values were corrected after checking its real schematic."),
                    HardwareMismatch(
                        "That verification hasn't happened. The Rev01 board's real design - bit-banged PWM into an op-amp across a 10 Ω sense resistor, open-loop, no ADC feedback - is documented in this repo and was confirmed against hardware. Either this manual describes an intended revision, or it needs correcting; the app can't tell which."),
                    new FigureBlock("Figure A1 - schematic excerpt, with reference designators visible. Protomod_ElectronicLoad.pdf exists in PROTOVERSE/Finished Modules/PC01_E05_ProtoMod_ElectronicLoad/Rev01 but is not bundled with the app."),
                }, IsAppendix: true),
            });
    }
}
