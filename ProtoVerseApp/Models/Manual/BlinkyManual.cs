using System.Collections.Generic;

namespace ProtoVerseApp.Models.Manual
{
    /// <summary>
    /// The Blinky (F01) manual, as in-app content.
    ///
    /// SOURCE: `PROTOVERSE/Manuals/Gen2/F01_Blinky_Manual.docx`, which unlike E05's
    /// reference doc describes the board that actually exists. Its technical content
    /// was verified against the real schematic before being used: D1-D4 are green LEDs,
    /// each with a 100 ohm series resistor (R1-R4), driven from ProtoCore's GPIO1-GPIO4,
    /// with an AT24CS02-SSHM EEPROM (U1) on the I2C identification bus. That matches the
    /// manual's Appendix C word for word, so the forward-voltage and ~13 mA figures are
    /// used as the manual states them.
    ///
    /// (`ProtoModLibraryCatalog` cited this file as `Blinky_F01_Manual.docx` until it
    /// was corrected here - the real name is `F01_Blinky_Manual.docx`. That path is
    /// displayed in the Library card, so it was a broken reference shown to users.)
    ///
    /// PITCHED FOR FUNDAMENTALS, which is the main way this differs from E05. Per the
    /// user's standing direction (2026-08-31, recorded in CLAUDE.md), an F-series manual
    /// assumes far less than E05's mid-series Explorers voice: it builds the idea in
    /// plain language before naming it, explains a term the first time it appears,
    /// treats "has never used a multimeter" as the default reader, and uses more and
    /// smaller steps with an Observe on almost every one. E05 can open with "an
    /// electronic load is a resistor you set in software"; this manual cannot assume the
    /// reader knows what a resistor does.
    ///
    /// THREE ADAPTATIONS THE WORD MANUAL NEEDS, all settled with the user rather than
    /// guessed:
    ///
    ///   1. The source manual's activities are written as "set LED1's pin HIGH" - i.e.
    ///      for a learner writing firmware. There is no such path today; the app is the
    ///      whole interface. Section 3 is rewritten around the real panel, and says
    ///      plainly that clicking an indicator is what "drive this pin HIGH" means here,
    ///      so the concept still lands.
    ///   2. The Creative Challenge asks the learner to invent a chase and a scanner -
    ///      both of which firmware already ships as selectable patterns. Rewritten as
    ///      predict-then-check against the built-in patterns, keeping the 4-bit binary
    ///      counter as the hands-on build since firmware does not do that one and it
    ///      works by clicking the four LED toggles.
    ///   3. The source manual points twice at a "Logic ProtoMod" as what comes next. No
    ///      such board exists in the catalog or has a circuit code, so those references
    ///      are dropped; the F02 progression, which is real and sourced, is kept.
    ///
    /// Also dropped, per the template rule E05 established: assembly steps, since this
    /// manual is only reachable once the board is seated and enumerated.
    /// </summary>
    public static class BlinkyManual
    {
        public static ManualDocument Build() => new(
            ModuleCode: "F01",
            SchematicFile: "F01_schematic.pdf",
            Header: new ManualHeader(
                Series: "Fundamentals Series",
                Code: "F01",
                Name: "Blinky",
                Tagline: "Your first digital command, made visible - four LEDs, four pins, one idea.",
                Difficulty: "Beginner",
                Time: "20-30 min",
                Prerequisites: "None"),
            SourceNote: "Written from PROTOVERSE/Manuals/Gen2/F01_Blinky_Manual.docx, with its technical content verified against the board's KiCad schematic. Activities rewritten for the app, since the source manual assumes the learner is writing firmware.",
            Sections: new[]
            {
                // ============================================================== 1
                new ManualSection("overview", "1. Overview", new ManualBlock[]
                {
                    new SubheadingBlock("Core concept"),
                    new ParagraphBlock(
                        "Control an LED with a digital output. This ProtoMod is the classic \"hello, world\" of electronics - the first time a line of code becomes something you can see."),
                    new ParagraphBlock(
                        "If this is your first ProtoMod, that sentence may not mean much yet, so here is the plain version. A computer can turn a wire on or off. That is genuinely all it can do at the bottom. This board takes four of those wires and puts a small light on the end of each one, so that a decision made in software becomes a thing happening in front of you."),
                    new BulletsBlock(new[]
                    {
                        "Four lights you can control one at a time, or all together.",
                        "Each one has its own wire coming from ProtoCore, so they never interfere with each other.",
                        "Turning a wire on means putting 3.3 volts on it. That is what lights the LED.",
                        "A small memory chip on the board tells ProtoCore what it is, which is why this manual appeared as soon as you plugged it in.",
                    }),
                    new ParagraphBlock(
                        "By the end you will have made a digital output do something visible, and met the idea underneath every other board in this series: a wire is either on or off, and that is enough to build everything else from."),
                    new ImageBlock("F01_circuit.png",
                        "The whole circuit. The four lights are at the right - each one paired with a resistor. Click for the full schematic."),
                }),

                // ============================================================== 2
                new ManualSection("how", "2. How it works", new ManualBlock[]
                {
                    new SubheadingBlock("Two words you will need"),
                    new ParagraphBlock(
                        "Voltage is electrical push - how hard the electricity is being shoved. Current is how much actually flows as a result. The usual comparison is water in a pipe: voltage is the pressure behind it, current is the amount going past per second. You need both ideas to follow anything below, and you do not need any more than this."),
                    new ParagraphBlock(
                        "Voltage is always measured between two points, never at one. When this manual says a pin is at 3.3 volts, it means 3.3 volts higher than the board's ground - the common reference every part of the circuit shares. Ground is the zero mark that everything else is counted from."),

                    new SubheadingBlock("What an LED actually is"),
                    new ParagraphBlock(
                        "An LED - light-emitting diode - is a component that glows when current flows through it in one particular direction. Think of it as a one-way street: current flowing the right way produces light, and the wrong way, nothing happens at all."),
                    new ParagraphBlock(
                        "An LED also has a threshold, called its forward voltage. Below that, almost nothing flows and the LED stays dark no matter how patient you are. Cross it, and current climbs fast. The green LEDs on this board have a forward voltage of about 2 volts."),
                    new FigureBlock("LED schematic symbol and physical package, anode and cathode labelled"),

                    new SubheadingBlock("Why there is a resistor next to every LED"),
                    new ParagraphBlock(
                        "That fast climb is the problem. Once an LED is past its forward voltage, a small increase in voltage causes a large increase in current - and left alone, that current would rise until the LED destroyed itself. Nothing about the LED stops it. So each light on this board has a resistor in series with it, and the resistor is what decides how much current is allowed through."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "Each LED here uses a 100 ohm resistor (R1-R4 on the schematic). Ohm's law says current = voltage / resistance. The pin supplies 3.3 V, the LED takes about 2.0 V of that, and the remaining 1.3 V sits across the resistor: 1.3 V / 100 ohms is roughly 13 mA. That is a comfortable, ordinary brightness for an LED of this kind.",
                        "Tech note - where 13 mA comes from"),
                    new ParagraphBlock(
                        "Notice what the resistor is really doing. It is not converting 3.3 volts into 2 volts as though the LED needed protecting from the extra. The LED settles at its own forward voltage by itself; the resistor's job is to fix how much current flows once it has. Get that distinction and you have most of what LEDs will ever ask of you."),

                    new SubheadingBlock("Active high"),
                    new ParagraphBlock(
                        "Every LED on this board is wired active high. When ProtoCore drives a pin HIGH - puts 3.3 V on it - current flows from that pin, through the resistor, through the LED, down to ground, and the light comes on. When the pin goes LOW, it sits at 0 V, nothing flows, and the light goes out."),
                    new ParagraphBlock(
                        "Given what you now know about direction and forward voltage, it should make sense why HIGH is the on state here: the pin is supplying the higher voltage the LED needs on its input side in order to conduct at all."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "\"Active high\" describes which level turns something ON, not that it is always on. The opposite convention, active low, turns a part on when its pin is driven LOW. Both are common. This board is active high on all four channels.",
                        "Tech note - active high vs active low"),

                    new SubheadingBlock("Where you have seen this already"),
                    new ParagraphBlock(
                        "Look around and you will find this exact idea everywhere: the glowing dot on a laptop charger, the standby light on a TV, the green power light inside a router. During development, engineers often wire a spare LED to a pin purely for testing - if it blinks, the code is running. It is the electronics equivalent of a print statement."),

                    new BulletsBlock(new[]
                    {
                        "A pin is either HIGH (3.3 V) or LOW (0 V). That is the whole vocabulary.",
                        "Active high: HIGH turns the LED on. Active low would be the reverse, and is not used here.",
                        "An LED needs its forward voltage before it conducts at all - about 2 V for these.",
                        "The series resistor sets the current, which is what keeps the LED alive.",
                        "Four independent pins and four independent resistors mean four lights that do not affect each other.",
                    }, Heading: "Key takeaways"),
                }),

                // ============================================================== 3
                new ManualSection("setup", "3. Set up and try it", new ManualBlock[]
                {
                    new ParagraphBlock(
                        "Everything below happens in the Blinky panel directly above this manual. The board is already plugged in and talking - that is why you are reading this - so there is nothing to wire up."),
                    new CalloutBlock(CalloutKind.TechNote,
                        "The row of four circles in the panel is not a picture of the board. Each circle is a control: clicking one asks ProtoCore to drive that LED's pin HIGH or LOW, and the board reports back what it actually did. When you click a circle, you are doing by hand exactly what a line of firmware would do - setting one pin's level.",
                        "Read this first"),
                    new ChecklistBlock(new[]
                    {
                        "A multimeter, if you have one. Everything here works without it; the last two steps are better with it.",
                    }, Heading: "You'll also need"),
                    new StepsBlock(new[]
                    {
                        new ManualStep(
                            "Click the first of the four circles.",
                            Observe: "One LED on the physical board lights up. Is there any delay you can perceive between the click and the light, or does it look instantaneous?"),
                        new ManualStep(
                            "Click the same circle again to turn it back off.",
                            Observe: "You have now driven one pin HIGH and then LOW. That pair of actions is the entire foundation of digital electronics - everything else is this, repeated and organised."),
                        new ManualStep(
                            "Turn each of the other three on and off in turn, one at a time.",
                            Observe: "Does each circle control the LED you expect? Compare the order on screen against the order of the lights on the board - they may or may not run the same way round, and it is worth knowing which."),
                        new ManualStep(
                            "Now turn all four on at once.",
                            Observe: "Look carefully at the brightness. Does any LED look dimmer with all four lit than it did on its own? Before you decide, think about the schematic: each light has its own pin and its own resistor. What would that predict?"),
                        new ManualStep(
                            "Leave all four on and find the \"Pattern\" dropdown. Choose \"All\", then watch.",
                            Observe: "The board takes over and starts blinking them together. You have handed control back: instead of you setting the pins, ProtoCore is now setting them on a timer."),
                        new ManualStep(
                            "Change \"Blink rate (ms)\" to 150 and press Apply. Then try 1000.",
                            Observe: "The number is milliseconds per step - how long the board waits between changes. 1000 ms is one second. Which value makes it easiest to see exactly what the LEDs are doing, and which makes it hardest?"),
                        new ManualStep(
                            "Click any one of the four circles again.",
                            Observe: "The blinking stops and that LED goes to whatever you just set. Clicking a circle is what puts the board back under your control - the panel's \"Mode\" readout changes from Animated to Manual to tell you which of you is driving."),
                        new ManualStep(
                            "Optional, with a multimeter: set it to measure DC volts. Put the black probe on a ground point and the red probe on the resistor end nearest one LED, with that LED lit.",
                            Observe: "You should read somewhere near 2 volts - the LED's forward voltage, the same figure from section 2, now measured rather than read about. Turn that LED off and measure again: it should fall close to 0."),
                    }, Numbered: true, Heading: "Now try this"),
                }),

                // ============================================================== 4
                new ManualSection("observations", "4. What you should see", new ManualBlock[]
                {
                    new BulletsBlock(new[]
                    {
                        "Each LED lights when its pin is driven HIGH and goes dark when it is driven LOW.",
                        "All four can be lit at once, and each one stays exactly as bright as it was on its own.",
                        "The light appears to come on instantly - there is no warm-up.",
                        "Measured across a lit LED, you get roughly 2 volts; across an unlit one, close to zero.",
                        "Clicking any single LED stops the animation and hands control back to you.",
                    }, Heading: "What you should see"),
                    new SubheadingBlock("Why it works"),
                    new ParagraphBlock(
                        "When a pin is driven HIGH it provides the voltage the LED needs to conduct. Current flows through the resistor and the LED to ground, and the LED emits light. The resistor holds that current at about 13 mA, which is bright enough to see clearly and gentle enough to run indefinitely."),
                    new ParagraphBlock(
                        "The brightness result is the one worth dwelling on. Because every channel has its own pin and its own resistor, the four are electrically separate - they are not sharing a supply of current that has to be divided up between them. Lighting one has no effect on the others at all. This is different from the string of holiday lights people often have in mind, where the bulbs really are in one chain and really do affect each other."),
                    new CalloutBlock(CalloutKind.Observe,
                        "You have now controlled four separate things using nothing but on and off. Nothing on this board understands \"brightness\", or \"pattern\", or \"blink\" - those are all built out of on, off, and timing. Keep that in mind as the boards get more capable: underneath, it stays this simple for a long time."),
                }),

                // ============================================================== 5
                new ManualSection("further", "5. Go further", new ManualBlock[]
                {
                    new SubheadingBlock("Creative challenge - predict, then check"),
                    new ParagraphBlock(
                        "ProtoCore has a few LED patterns built into it, and you can watch them from the Pattern dropdown. But watching an animation teaches you very little. Predicting it first teaches you a lot, so do these in order: work out your answer, say it out loud or write it down, and only then select the pattern."),
                    new StepsBlock(new[]
                    {
                        new ManualStep(
                            "\"Chase\" lights one LED at a time and moves along. Before you select it: which LED starts, which direction does it travel, and what happens when it reaches the end - does it jump back to the start, or turn around?"),
                        new ManualStep(
                            "Now select Chase and watch. Then tick \"Reverse direction\" and predict what changes before you look."),
                        new ManualStep(
                            "\"Bounce\" also lights one at a time, but it does not jump back. Predict the full repeating sequence of LED positions, including how the ends behave. Write down the order before selecting it."),
                        new ManualStep(
                            "Select Bounce and check. If your sequence was wrong, work out where it differed - the ends are where nearly everyone gets it wrong.",
                            Observe: "Bounce repeats every six steps across four LEDs, not eight. Why six? What does that tell you about whether the two end LEDs are lit as often as the middle two?"),
                        new ManualStep(
                            "Slow the blink rate right down to 1000 ms and watch Bounce again if you are not sure."),
                    }, Numbered: true),
                    new SubheadingBlock("Build one yourself: count in binary"),
                    new ParagraphBlock(
                        "This is the one the board cannot do for you, so you will drive it by hand. Treat the four LEDs as four binary digits, with a lit LED meaning 1 and an unlit one meaning 0. Counting up in binary goes 0000, 0001, 0010, 0011, 0100 - each step adds one, exactly like ordinary counting except each column only ever holds 0 or 1 before it rolls over."),
                    new ParagraphBlock(
                        "Click your way from 0 to 15, one number at a time. It is fiddly on purpose: by about 7 you will have a strong instinct for why a computer does this with a clock rather than a person doing it with a mouse, and you will have seen that four on-or-off wires can represent sixteen different values."),
                    ManualBoilerplate.NoSingleCorrectAnswer,
                    new SubheadingBlock("Something to think about"),
                    new ParagraphBlock(
                        "Can you name a real device where a blink pattern - not just on or off, but the rhythm of it - tells you something? Router lights, charging indicators and fault lamps are all worth considering. What do you think the different speeds are meant to mean, and how would you know if you were guessing wrong?"),
                    new SubheadingBlock("Check yourself"),
                    new MultipleChoiceBlock("reflection", new[]
                    {
                        new ManualChoiceQuestion(
                            "What does \"active high\" mean in this circuit?",
                            new[]
                            {
                                "The LED is on all the time unless something switches it off.",
                                "The LED turns on when its pin is driven HIGH, at 3.3 V.",
                                "The LED turns on when its pin is driven LOW, at 0 V.",
                                "This LED needs a higher voltage than most LEDs do.",
                            },
                            CorrectIndex: 1,
                            Explanation: "Active high describes which level turns the LED on - HIGH is the on state here. It says nothing about how often the LED is actually lit, which is the usual confusion: an active-high LED spends most of its life off. The opposite convention, active low, turns a part on when its pin is driven LOW, and is just as common elsewhere."),

                        new ManualChoiceQuestion(
                            "Why might an engineer choose active-high wiring rather than active-low?",
                            new[]
                            {
                                "Active-high circuits use less current.",
                                "LEDs only work when wired active high.",
                                "\"HIGH means on\" matches most people's intuition, so the design is easier to reason about and harder to get wrong.",
                                "The GPIO pin can only drive HIGH, never LOW.",
                            },
                            CorrectIndex: 2,
                            Explanation: "It is a readability choice more than an electrical one - both work, and both draw the same current. Active low is often preferred for reset or enable lines, though, for a specific reason worth remembering: many failure states settle LOW on their own - a disconnected wire, a chip that has lost power - and it is usually safer for the failure state to mean \"inactive\"."),

                        new ManualChoiceQuestion(
                            "With all four LEDs lit at once, does any of them dim compared to being lit alone?",
                            new[]
                            {
                                "Yes - they share the available current, so each gets roughly a quarter.",
                                "Yes - the resistors warm up and let less through.",
                                "No - the LEDs are wired in series, which keeps them equal.",
                                "No - each LED has its own pin and its own resistor, so the four are electrically independent.",
                            },
                            CorrectIndex: 3,
                            Explanation: "Nothing is shared between the channels, so there is nothing to divide up. Each pin supplies its own LED through its own resistor. The tempting wrong answer is the first one, usually by analogy with a string of holiday lights - but those really are one chain in series, which is exactly what this board is not. Note that the third option gets the right answer for the wrong reason: series wiring would make them affect each other, not stay independent."),

                        new ManualChoiceQuestion(
                            "What is the 100 ohm resistor next to each LED actually for?",
                            new[]
                            {
                                "It limits the current through the LED, which is what stops the LED destroying itself.",
                                "It makes the LED brighter than it would otherwise be.",
                                "It converts the pin's 3.3 V down to the 2 V the LED can survive.",
                                "It lets ProtoCore identify which board is plugged in.",
                            },
                            CorrectIndex: 0,
                            Explanation: "Past its forward voltage an LED's current rises very steeply, and nothing about the LED itself limits it - so the resistor does, at about 13 mA here. The third option is the near-miss worth understanding: the resistor really does end up with about 1.3 V across it, but that is a consequence, not the purpose. The LED settles at its own forward voltage on its own; what the resistor decides is how much current flows once it has. (Identification is a separate part entirely - the small memory chip, U1 on the schematic.)"),
                    }, Heading: "Follow-up questions"),
                    new SubheadingBlock("Where to go next"),
                    new ParagraphBlock(
                        "Once you're comfortable driving LEDs directly with GPIO, move on to Simple LED (F02) - to slow down and look inside the circuit itself: how supply voltage and resistor choice change an LED's brightness, and why red and green LEDs behave differently at the same settings."),
                }),

                // ======================================================= Appendix
                // No answer-key appendix: section 5's questions are multiple choice and
                // explain themselves once answered. See the E05 manual for the same
                // reasoning.
                new ManualSection("appendix-a", "Appendix - Facilitator notes", new ManualBlock[]
                {
                    new SubheadingBlock("Timing guide"),
                    new ParagraphBlock(
                        "About 20-30 minutes for the guided portion. Most learners move quickly through the individual LED tests; the time goes on the all-four-at-once observation and on the binary counter, which is deliberately slow work."),
                    new SubheadingBlock("Common misconceptions"),
                    new BulletsBlock(new[]
                    {
                        "\"Active high\" read as \"always on\". It describes which level turns the LED on, not that it is permanently lit. Check the actual pin state, not just the wiring convention.",
                        "Expecting the LEDs to dim as more are turned on, by analogy with holiday lights wired in series. Worth stating explicitly that these four channels are independent - it is the single most valuable thing in the module and it does not land on its own.",
                        "Assuming the resistor exists to reduce the voltage to what the LED needs. It sets the current; the voltage across it is a side effect. Beginners who learn it the other way round struggle later with anything involving a diode.",
                        "Believing the four circles in the app are a display of the board's state. They are controls - and while a pattern is running they show the app's own reconstruction of what the board should be doing, not a live report from it.",
                    }),
                    new SubheadingBlock("Extension ideas"),
                    new BulletsBlock(new[]
                    {
                        "Ask a learner who finished the binary counter quickly to work out how high four LEDs could count if brightness could be varied as well as on and off - and what would make that harder to read reliably.",
                        "Have them time the Bounce pattern with a stopwatch at a known blink rate and check whether six steps really do take six times the rate. It is a gentle first encounter with the idea that a stated timing figure is a claim you can test.",
                    }),
                    new SubheadingBlock("Answers"),
                    new ParagraphBlock(
                        "Section 5's questions are multiple choice and mark themselves. Choosing an option reveals both the correct answer and the reasoning, so there is no separate key to hand out - and no way to read the answers before committing to one."),
                }, IsAppendix: true),
            });
    }
}
