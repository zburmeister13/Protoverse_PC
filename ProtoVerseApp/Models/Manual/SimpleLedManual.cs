using System.Collections.Generic;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// The Simple LED (F02) manual, as in-app content.
    ///
    /// SOURCE: `PROTOVERSE/Manuals/F02_Simple_LED.docx`. Unlike F01 and E05 this is the
    /// OLDER pre-template format - it has no Creative Challenge, no facilitator notes,
    /// and states no difficulty or time. The user chose (2026-09-01) to fill those in
    /// and flag them for review rather than leave them out, so every passage with no
    /// source document behind it carries a `CalloutKind.NeedsReview` callout and is
    /// counted in the banner at the top of the manual. Three of them: the header
    /// metadata, the creative challenge, and the facilitator notes. Everything else is
    /// from the manual or from the schematic.
    ///
    /// VERIFIED AGAINST THE SCHEMATIC AND BOM before use, same as F01. Red D1
    /// (LTST-C150KRKT) and green D2 (LTST-C150KGKT); SW1/SW2 select 3.3 V or 1.8 V for
    /// the red and green paths; SW3 selects R1 (470) or R2 (100) for red, SW4 selects R3
    /// (470) or R4 (100) for green. All four switches are SW_SPDT slide switches. That
    /// matches the manual's Overview exactly.
    ///
    /// TWO HARDWARE DETAILS THE MANUAL NEVER MENTIONS, both read off the schematic:
    ///   - TP1-TP4 are real Keystone test points. TP1 sits on the red path's selected
    ///     supply and TP2 at its resistor/LED junction; TP3 and TP4 are the same on the
    ///     green side. That makes the whole circuit measurable: TP1 minus TP2 is the
    ///     voltage across the selected resistor, TP2 to ground is the LED's forward
    ///     voltage, and the current follows from the first divided by the resistance.
    ///     Far better than F01's single measurement, and it is why this manual has a
    ///     real measurement section.
    ///   - J1 and J40 are 2-pin headers in series with each LED. The user confirmed
    ///     (2026-09-01) that the default configuration is a jumper fitted, and that they
    ///     exist so current can be measured through the LED by removing the jumper and
    ///     putting an ammeter across the pins - "for the more adventurous or curious".
    ///     That is written up as an optional step, and the manual says plainly that the
    ///     jumpers stay put otherwise, since a learner who pulls one and doesn't know
    ///     why the LED went out will assume the board is broken.
    ///
    /// NO SOFTWARE CONTROLS AT ALL. This board is passive by design, which makes it the
    /// first manual where the panel above it is not part of the exercise - see
    /// `PassiveModuleViewModel`. Every instruction here is a physical switch.
    /// </summary>
    public static class SimpleLedManual
    {
        public static ManualDocument Build() => new(
            ModuleCode: "F02",
            SchematicFile: "F02_schematic.pdf",
            Header: new ManualHeader(
                Series: "Fundamentals Series",
                Code: "F02",
                Name: "Simple LED",
                Tagline: "Two LEDs, two switches each, and the three quantities that decide how bright a light is.",
                // Difficulty and Time are NOT from the source manual - see the
                // NeedsReview callout in section 1. Prerequisites IS sourced: this
                // manual opens "In F01 (LED Blinky), you used the ProtoCore to turn an
                // LED on and off in software", and F01's own manual says to move on to
                // F02 next.
                Difficulty: "Beginner",
                Time: "25-35 min",
                Prerequisites: "Blinky (F01)"),
            SourceNote: "Written from PROTOVERSE/Manuals/F02_Simple_LED.docx, with its technical content verified against the board's KiCad schematic and BOM. That manual predates the current template, so the difficulty/time metadata, the creative challenge and the facilitator notes were written for the app and are flagged in place as needing review.",
            Sections: new[]
            {
                // ============================================================== 1
                new ManualSection("overview", "1. Overview", new ManualBlock[]
                {
                    new SubheadingBlock("Core concept"),
                    new ParagraphBlock(
                        "This ProtoMod shows how supply voltage and resistance work together to control the current through an LED, and how that current changes the LED's on/off behavior and brightness."),
                    new ParagraphBlock(
                        "In Blinky (F01) you turned LEDs on and off from software and left the circuit alone. Here there is no software at all. You change the circuit itself, with switches, and watch what your eyes tell you - which is the only way to build a feel for what voltage and resistance actually do."),
                    new BulletsBlock(new[]
                    {
                        "Two separate LED paths, one red and one green.",
                        "Each path has a voltage selector switch - SW1 for red, SW2 for green - choosing between 3.3 V and 1.8 V.",
                        "Each path also has a resistor selector switch - SW3 for red, SW4 for green - choosing between 470 ohms and 100 ohms.",
                        "That gives four combinations per LED, and eight experiments in total.",
                    }),
                    new ParagraphBlock(
                        "By working with this board, you'll see how changing voltage and resistance changes current, and how red and green LEDs behave slightly differently because they need different forward voltages to light."),
                    new CalloutBlock(CalloutKind.NeedsReview,
                        "The difficulty rating and time estimate at the top of this manual were written for the app - this module's source document states neither, so they are an estimate rather than a decision, and should be confirmed. (The prerequisite is sourced: this module's own manual opens by referring back to F01, and F01's manual names F02 as what comes next.)",
                        "Needs review - header metadata"),
                    new ImageBlock("F02_circuit.png",
                        "The whole circuit. The two LED paths are on the right - red on the left of the pair, green on the right. Click for the full schematic."),
                }),

                // ============================================================== 2
                new ManualSection("how", "2. How it works", new ManualBlock[]
                {
                    new SubheadingBlock("LEDs in plain language"),
                    new ParagraphBlock(
                        "An LED lights up when current flows through it in the forward direction. It needs a certain minimum voltage, called its forward voltage, before it turns on at all. Below that, essentially nothing happens. Above it, current flows and the LED glows - and more current means a brighter light, up to a point where too much would damage it. That is why there is always a resistor in series."),
                    new ParagraphBlock(
                        "Red LEDs turn on at a lower voltage than green ones. That single fact is behind most of what you are about to see, and it is why this board has one of each rather than two of the same colour."),

                    new SubheadingBlock("Voltage, resistance, and current"),
                    new ParagraphBlock(
                        "For this board you can think in simple rules. Higher voltage and lower resistance mean more current and a brighter LED. Lower voltage and higher resistance mean less current and a dimmer LED - or no light at all."),
                    new ParagraphBlock(
                        "The 3.3 V and 1.8 V options change how much push the LED gets. The 470 ohm and 100 ohm resistors act like bigger or smaller speed bumps for that current. Because you can change one at a time, you can see what each one does on its own, which is the whole point of the board."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "Only ever flip one switch at a time. If you change the voltage and the resistor together and the LED gets dimmer, you have learned nothing about which change did it. This is the oldest rule in experimental work and it is worth practising here, where the cost of ignoring it is only confusion.",
                        "Tech note - change one thing"),

                    new SubheadingBlock("What the switches actually do"),
                    new BulletsBlock(new[]
                    {
                        "SW1 connects the red path to either the 3.3 V or the 1.8 V supply.",
                        "SW3 puts either R1 (470 ohms) or R2 (100 ohms) in the red path.",
                        "SW2 and SW4 do exactly the same for the green path, using R3 (470 ohms) and R4 (100 ohms).",
                        "The two paths are completely separate. Nothing you do to the red LED affects the green one.",
                    }),

                    new SubheadingBlock("The test points, and why they matter"),
                    new ParagraphBlock(
                        "There are four small metal loops on the board labelled TP1 to TP4. They are there to give a multimeter probe something to hold on to, and they are placed so that this circuit can be measured rather than just watched."),
                    new BulletsBlock(new[]
                    {
                        "TP1 sits on the red path's supply, after SW1 - so it tells you which voltage you actually selected.",
                        "TP2 sits between the resistor and the red LED - so it tells you the LED's own voltage.",
                        "TP3 and TP4 are the same two points on the green path.",
                    }),
                    new CalloutBlock(CalloutKind.TechNote,
                        "This gives you the whole circuit. The voltage across the resistor is TP1 minus TP2. The LED's forward voltage is TP2 measured against ground. And since current through a resistor is the voltage across it divided by its resistance, you can work out the current without ever breaking the circuit - which is what section 3 has you do.",
                        "Tech note - measuring without breaking anything"),

                    new SubheadingBlock("Where you have seen this"),
                    new ParagraphBlock(
                        "LEDs are used everywhere as simple indicators - power lights, charging status LEDs, notification lights on routers or game consoles. Traffic signals, brake lights and signs use carefully chosen LED colours and brightness levels so they are easy to see without being blinding. Inside electronics, different colours often signal different states: red for error, green for okay, yellow for warning - partly because their visibility and forward voltages differ."),
                }),

                // ============================================================== 3
                new ManualSection("setup", "3. Set up and try it", new ManualBlock[]
                {
                    new CalloutBlock(CalloutKind.TechNote,
                        "There is nothing to click for this module. The panel above this manual has no controls because this board has none - every setting is a physical switch you flip with a fingernail. Leave the small jumpers on J1 and J40 in place; they complete the two LED circuits, and an LED with its jumper removed will simply stay dark.",
                        "Read this first"),
                    new ChecklistBlock(new[]
                    {
                        "A multimeter, for the measurement steps near the end. The first six steps need nothing at all.",
                    }, Heading: "You'll also need"),
                    new StepsBlock(new[]
                    {
                        new ManualStep(
                            "Set a reference point for the red LED: SW1 to 3.3 V, SW3 to R1 (470 ohms). This is the higher voltage with the larger resistor.",
                            Observe: "The red LED should light at a comfortable brightness - not dim, not blinding. Everything else you do will be compared against this, so look at it properly before moving on."),
                        new ManualStep(
                            "Change only the resistor: leave SW1 at 3.3 V, flip SW3 to R2 (100 ohms).",
                            Observe: "The red LED should get noticeably brighter. Nothing about the voltage or the LED changed - only how much of a speed bump was in the way."),
                        new ManualStep(
                            "Now change only the voltage: leave SW3 at 100 ohms, flip SW1 down to 1.8 V.",
                            Observe: "It should dim sharply - possibly much more than you expected for a change from 3.3 V to 1.8 V. Why might halving the push cut the brightness by more than half? Think about what the LED takes for itself before the resistor sees anything."),
                        new ManualStep(
                            "Still at 1.8 V, flip SW3 back to 470 ohms.",
                            Observe: "This is the most restricted setting on the red side: least push, biggest speed bump. Is the red LED still visible at all? Look at it in shadow if you are not sure."),
                        new ManualStep(
                            "Now repeat all four combinations on the green LED using SW2 and SW4, in the same order: 3.3 V with 470, then 3.3 V with 100, then 1.8 V with 100, then 1.8 V with 470.",
                            Observe: "Compare each green result against the red result at the same settings. Where do they agree, and where do they part company?"),
                        new ManualStep(
                            "Set both LEDs to 1.8 V and look at them side by side.",
                            Observe: "This is the important one. The green LED should be much worse off than the red at the same settings - very faint, or completely dark. Both are getting the same voltage through the same resistance, so the difference has to be in the LEDs themselves."),
                        new ManualStep(
                            "Multimeter, part one: set it to measure DC volts, put the black probe on a ground point, and touch the red probe to TP1 with the red LED set to 3.3 V and 470 ohms. Then move the red probe to TP2.",
                            Observe: "TP1 should read close to 3.3 V. TP2 will read something lower - that is the LED's forward voltage, the number section 2 said existed. Now subtract: the difference is what the resistor is dropping."),
                        new ManualStep(
                            "Work out the current: take that difference and divide it by 470. Then flip SW3 to 100 ohms and measure both points again.",
                            Observe: "The current should come out several times larger with the smaller resistor - and notice that TP2, the LED's own voltage, barely moved. The LED holds its forward voltage roughly steady and lets the current change instead. That is the single most useful thing to take away from this board."),
                        new ManualStep(
                            "Optional, for the curious: pull the jumper off J1 and bridge those two pins with your meter set to measure DC current instead. This puts the meter in series with the red LED, so all of the LED's current flows through it.",
                            Observe: "You are now measuring the current directly rather than calculating it. How close is it to the number you worked out in the last step? If they disagree, which one would you trust, and why? Put the jumper back when you are done."),
                    }, Numbered: true, Heading: "Now try this"),
                }),

                // ============================================================== 4
                new ManualSection("observations", "4. What you should see", new ManualBlock[]
                {
                    new BulletsBlock(new[]
                    {
                        "At 3.3 V with the 100 ohm resistor selected, each LED is noticeably brighter than it is with the 470 ohm resistor at the same voltage.",
                        "At 1.8 V, the LEDs are much dimmer, and in some combinations - especially the green LED with higher resistance - they may barely glow or switch off entirely.",
                        "The red and green LEDs do not behave the same at low voltage: the red LED tends to light more easily, while the green LED requires more voltage before it turns on.",
                        "Measured at TP2, an LED's own voltage stays roughly the same whether it is dim or bright - what changes is the current.",
                    }, Heading: "What you should see"),
                    new SubheadingBlock("Why it works"),
                    new ParagraphBlock(
                        "The switches change two things: how much voltage is available to push current through the LED, and how much resistance is in series with it. Higher voltage and lower resistance allow more current to flow, making the LED brighter; lower voltage and higher resistance limit the current and make it dimmer or off."),
                    new ParagraphBlock(
                        "The colour difference has a specific cause. Because red and green LEDs have different forward voltages, they begin to conduct at different supply levels. At 1.8 V the green LED has barely any margin left over once it has taken its own forward voltage, so very little is left to push current through the resistor - which is why it fades or goes out while the red one is still visibly lit."),
                    new CalloutBlock(CalloutKind.Observe,
                        "Notice that you never changed the LEDs, and you never changed what they need. You changed what was available to them. Most of practical electronics is that: the parts have fixed appetites, and design is arranging what they get."),
                }),

                // ============================================================== 5
                new ManualSection("further", "5. Go further", new ManualBlock[]
                {
                    new SubheadingBlock("Creative challenge"),
                    new CalloutBlock(CalloutKind.NeedsReview,
                        "This module's source manual has no Creative Challenge section - it predates the template that introduced one. The challenge below was written for the app from the board's own capabilities and needs review before it ships.",
                        "Needs review - creative challenge"),
                    new ParagraphBlock(
                        "There are four settings per LED and two LEDs, so sixteen combinations in total. Two things worth hunting for:"),
                    new BulletsBlock(new[]
                    {
                        "Make the red and green LEDs look equally bright. They will not match at the same settings - so find a pair of different settings that does. Predict which pair before you try it, using what you know about their forward voltages.",
                        "Find the dimmest setting at which the green LED still emits any light at all, then do the same for red. What does the gap between those two answers tell you about the two LEDs?",
                    }),
                    ManualBoilerplate.NoSingleCorrectAnswer,
                    new SubheadingBlock("Something to think about"),
                    new ParagraphBlock(
                        "In your own words, how are voltage, resistance and brightness connected in this circuit? If you can explain that to someone else without using the word \"electricity\", you have got it."),
                    new SubheadingBlock("Check yourself"),
                    new MultipleChoiceBlock("reflection", new[]
                    {
                        new ManualChoiceQuestion(
                            "Which switch combination made the red LED brightest?",
                            new[]
                            {
                                "1.8 V with the 470 ohm resistor.",
                                "3.3 V with the 100 ohm resistor.",
                                "3.3 V with the 470 ohm resistor.",
                                "1.8 V with the 100 ohm resistor.",
                            },
                            CorrectIndex: 1,
                            Explanation: "The higher voltage and the lower resistance together allow the most current to flow, and current is what brightness follows. The opposite pairing - 1.8 V with 470 ohms - is the dimmest, because both settings are working to restrict current at the same time."),

                        new ManualChoiceQuestion(
                            "You keep the voltage the same and switch from 470 ohms to 100 ohms. What happens, and why?",
                            new[]
                            {
                                "Brighter, because the smaller resistor allows more current to flow.",
                                "Brighter, because the smaller resistor delivers more voltage to the LED.",
                                "Dimmer, because less resistance means less power reaches the LED.",
                                "No change, because the voltage did not change.",
                            },
                            CorrectIndex: 0,
                            Explanation: "Less resistance in the path means more current for the same push, and more current means a brighter LED. The second option reaches the right answer by the wrong route, and it is worth being clear about: the LED's own voltage barely moves when you change the resistor - you measured that at TP2 in section 3. The resistor changes the current, not the LED's voltage."),

                        new ManualChoiceQuestion(
                            "You keep the resistor the same and drop the supply from 3.3 V to 1.8 V. What happens?",
                            new[]
                            {
                                "Nothing, as long as the supply stays above the LED's forward voltage.",
                                "It gets brighter, because less voltage is wasted in the resistor.",
                                "It gets dimmer, and may go out entirely - less push means less current through the same resistance.",
                                "It flickers, because the supply is now unstable.",
                            },
                            CorrectIndex: 2,
                            Explanation: "The lower voltage provides less electrical push, so less current flows through the LED and resistor. The first option is the tempting one and it is wrong in an instructive way: what matters is not merely clearing the forward voltage but how much is left over afterwards to drive current through the resistor. At 1.8 V there is very little left over."),

                        new ManualChoiceQuestion(
                            "At 1.8 V the green LED was much dimmer than the red one, or off entirely, at identical settings. Why?",
                            new[]
                            {
                                "Green LEDs are physically smaller, so they produce less light.",
                                "The green path uses larger resistors than the red path.",
                                "Human eyes are less sensitive to green light.",
                                "The green LED has a higher forward voltage, so it needs more before it starts to conduct at all.",
                            },
                            CorrectIndex: 3,
                            Explanation: "Different LED colours have different forward voltages, and green sits higher than red. At 1.8 V the green LED has almost nothing left over after taking its own forward voltage, so barely any current flows. The resistor option is worth ruling out deliberately: both paths offer the same 470 and 100 ohm choices, so the resistors are not the difference. (And human eyes are in fact more sensitive to green, not less - which makes the result more striking, since the green LED looks dimmer despite that advantage.)"),

                        new ManualChoiceQuestion(
                            "If you wanted an LED to last a long time and run cool, which resistor would you choose most of the time?",
                            new[]
                            {
                                "470 ohms - it limits the current more, so the LED runs under less stress.",
                                "100 ohms - more current keeps the LED working properly.",
                                "Neither; the resistor has no effect on the LED's lifetime.",
                                "Whichever one makes it brightest, since brightness and lifetime are unrelated.",
                            },
                            CorrectIndex: 0,
                            Explanation: "The 470 ohm resistor limits current more than the 100 ohm one. The LED is dimmer, but it is under less stress and less likely to overheat or age quickly. This is a real design trade rather than a puzzle with a hidden answer: brightness costs current, current costs heat and lifetime, and a designer picks a point on that line deliberately."),
                    }, Heading: "Follow-up questions"),
                }),

                // ======================================================= Appendix
                new ManualSection("appendix-a", "Appendix - Facilitator notes", new ManualBlock[]
                {
                    new CalloutBlock(CalloutKind.NeedsReview,
                        "This module's source manual has no facilitator notes - the whole appendix below was written for the app, from the board's behaviour and the source manual's own answer key. The misconceptions in particular are reasoned rather than observed, and should be checked against what learners actually do.",
                        "Needs review - facilitator notes"),
                    new SubheadingBlock("Timing guide"),
                    new ParagraphBlock(
                        "Roughly 25-35 minutes. The eight switch combinations go quickly; the multimeter steps are where the time goes, especially for learners meeting a meter for the first time. The optional current measurement on J1 adds about ten minutes and is worth it for anyone who finishes early."),
                    new SubheadingBlock("Common misconceptions"),
                    new BulletsBlock(new[]
                    {
                        "Believing the resistor sets the LED's voltage. It sets the current; the LED's voltage is roughly fixed by the LED itself. Section 3's TP2 measurement is designed to show this directly - if a learner takes only one thing from this board, this should be it.",
                        "Expecting the brightness change from 3.3 V to 1.8 V to be proportional. It is not, and it surprises people. The LED takes its forward voltage off the top first, so what is left to drive current falls much faster than the supply does.",
                        "Assuming the green LED is dimmer because green is somehow weaker. It is a forward-voltage difference, and it is worth naming explicitly - otherwise learners generalise it into a belief about colour and brightness that will mislead them later.",
                        "Changing two switches at once and drawing a conclusion. Worth watching for and interrupting early, since the resulting confusion is hard to unpick after the fact.",
                    }),
                    new SubheadingBlock("Extension ideas"),
                    new BulletsBlock(new[]
                    {
                        "Have learners measure at TP1 and TP2 for all four red settings and tabulate the computed current, then check whether the brightness they saw tracks the current they calculated.",
                        "For anyone who did the J1 current measurement: ask why the measured and calculated currents might differ slightly, and what that says about the tolerance of the resistors and the meter.",
                    }),
                    new SubheadingBlock("Answers"),
                    new ParagraphBlock(
                        "Section 5's questions are multiple choice and mark themselves. Choosing an option reveals both the correct answer and the reasoning, so there is no separate key to hand out."),
                }, IsAppendix: true),
            });
    }
}
