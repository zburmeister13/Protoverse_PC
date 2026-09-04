using System.Collections.Generic;
using System.Linq;

namespace ProtoVerseApp.Models
{
    /// <summary>
    /// The three families of ProtoMod. The family is determined by the first letter of
    /// the board's circuit code: F -> Fundamentals, E -> Explorers, A -> Advanced.
    ///
    /// Confirmed as a product rule by the user 2026-08-31. Worth stating explicitly
    /// because it's the one field in this catalog that may legitimately be derived
    /// rather than quoted: each manual states its own series in prose, but no document
    /// had written down the letter mapping, so entries for boards with no manual (E05,
    /// F00) previously carried it as a flagged inference. That caveat is resolved.
    ///
    /// The no-fabrication rule still applies to everything else - a module's
    /// description, ideas, schematic, and especially its "leads into" progression are
    /// never inferred from the circuit code, its number, or its family.
    ///
    /// (The manuals write the middle family as "Explorers"; that's what's displayed.)
    /// </summary>
    public enum ProtoModSeries
    {
        Fundamentals,
        Explorers,
        Advanced
    }

    /// <summary>One project prompt for a ProtoMod, lifted from that module's manual.
    /// <paramref name="Source"/> names the section it came from and is shown in the UI -
    /// these are never generated, so the provenance is worth displaying rather than
    /// hiding.</summary>
    public record ProtoModIdea(string Text, string Source);

    /// <summary>A "this module leads into that one" link. <paramref name="Evidence"/>
    /// is the actual sentence (and its manual/section) that establishes the
    /// progression - a link is only ever added here when such a sentence exists.
    /// Pedagogical sequence is NOT inferred from circuit codes, series, or
    /// difficulty.</summary>
    public record ProtoModNextStep(string Code, string Evidence);

    /// <summary>
    /// One ProtoMod in the Library tab's catalog. Every nullable field means "no real
    /// source material exists for this yet" and is rendered as an explicit
    /// "coming soon" in the UI rather than being filled with plausible-sounding text.
    /// </summary>
    /// <param name="Code">Circuit code as printed on the board / burned into its
    /// EEPROM, e.g. "F01".</param>
    /// <param name="Name">Module name exactly as its manual's "Module Name:" line
    /// gives it, or - for a board with no manual - the name of its hardware folder /
    /// <see cref="ViewModels.ModuleCatalog"/> registration.</param>
    /// <param name="Difficulty">Only set where a manual actually states one. Most
    /// manuals don't, so this is usually null.</param>
    /// <param name="TimeEstimate">Same rule as Difficulty.</param>
    /// <param name="Description">One-sentence "what this teaches", quoted from the
    /// manual's Core Concept line where one exists.</param>
    /// <param name="SchematicSummary">Brief text description of the actual circuit,
    /// from the manual's schematic appendix / overview, or from this repo's own docs
    /// for a board with no manual. Null where neither exists.</param>
    /// <param name="SchematicDrawingReference">The real KiCad-exported schematic PDF
    /// for this board, if one exists in the ProtoVerse hardware tree. None of these
    /// are bundled with the app as images yet, so the UI shows this as a reference to
    /// a file rather than rendering a thumbnail.</param>
    /// <param name="ProtocolId">The wire-protocol ID ProtoCore reports for this board,
    /// or null for a board that has no <see cref="ProtoModId"/> assigned yet (which
    /// also means this app can never show it as installed).</param>
    /// <param name="ManualReference">Path of the module's manual within the ProtoVerse
    /// docs tree, or null if no manual has been written.</param>
    public record ProtoModCatalogEntry(
        string Code,
        string Name,
        ProtoModSeries Series,
        string? Difficulty,
        string? TimeEstimate,
        string? Description,
        string? DescriptionSource,
        string? SchematicSummary,
        string? SchematicSummarySource,
        string? SchematicDrawingReference,
        IReadOnlyList<ProtoModIdea> Ideas,
        IReadOnlyList<ProtoModNextStep> NextSteps,
        ProtoModId? ProtocolId,
        string? ManualReference);

    /// <summary>
    /// The full ProtoMod catalog shown in the Library tab - deliberately broader than
    /// <see cref="ViewModels.ModuleCatalog"/> (which is only the module types this
    /// build can render a live control panel for) and broader than what's plugged into
    /// the ProtoCore right now. The Library is a discovery surface: "here's the whole
    /// ProtoVerse, here's the part of it you already own."
    ///
    /// HARDCODED ON PURPOSE FOR v1. This should eventually move to a JSON/manifest
    /// source generated from - or shared with - the ProtoMod manual documents in
    /// `PROTOVERSE/Manuals/`, so a new module's catalog entry falls out of writing its
    /// manual instead of being transcribed here by hand. The record shape above is
    /// already JSON-friendly (flat, no behavior) and <see cref="Entries"/> is the only
    /// thing the rest of the app touches, so that swap should be a change to this file
    /// alone. See the "ProtoMod Library" section of README.md.
    ///
    /// NO FABRICATED CONTENT. Every string below is either quoted from, or directly
    /// summarized from, a document that actually exists in this repo or the ProtoVerse
    /// hardware/manual tree - the `*Source` fields name which one, and the UI shows
    /// them. Where no source material exists (no manual written, no schematic drawn,
    /// no stated difficulty, no documented next step), the field is null and the UI
    /// says so. Do not fill a null in here with something that "sounds right" - write
    /// the manual first, then quote it.
    ///
    /// Sources used, all verified by reading the files directly:
    ///   - `PROTOVERSE/Manuals/Gen2/F01_Blinky_Manual.docx`  (F01, newest manual)
    ///   - `PROTOVERSE/Manuals/F02_Simple_LED.docx`          (F02)
    ///   - `PROTOVERSE/Manuals/E03_Sensors1.docx`            (E03)
    ///   - `PROTOVERSE/Manuals/A01_DDS.docx`                 (A01)
    ///   - `PROTOVERSE/Finished Modules/PC01_*/Rev01/*.pdf`  (schematic exports)
    ///   - this repo's own CLAUDE.md / ElectronicLoadViewModel.cs (E05, no manual)
    /// </summary>
    public static class ProtoModLibraryCatalog
    {
        public static readonly IReadOnlyList<ProtoModCatalogEntry> Entries = new[]
        {
            // ---------------------------------------------------------------- F01
            // Two manuals exist for this board: `Manuals/F01_Blinky.docx` (older -
            // titles the board "Blink", calls the series "Foundations", and points at
            // F02 as "the Switch ProtoMod") and `Manuals/Gen2/F01_Blinky_Manual.docx`
            // (newer, written against the ProtoVerse_ProtoMod_Manual_Template - titles
            // it "Blinky", says "Fundamentals Series", and describes F02 as "Simple
            // LED", matching F02's own manual). Everything below is quoted from the
            // Gen2 manual, which is the one that agrees with the rest of the docs.
            new ProtoModCatalogEntry(
                Code: "F01",
                Name: "Blinky",
                Series: ProtoModSeries.Fundamentals,
                Difficulty: "Beginner",
                TimeEstimate: "20-30 min",
                Description: "Control an LED with a digital output. This ProtoMod is the classic “hello, world” of electronics - the first time a line of code becomes something you can see.",
                DescriptionSource: "Core concept - Blinky (F01) manual, §1 Overview",
                SchematicSummary: "LEDs D1-D4, each with a 100 Ω series resistor (R1-R4), are driven individually from ProtoCore's GPIO1-GPIO4 header pins. Module identification is provided by U1, an AT24CS02-SSHM EEPROM, read by ProtoCore over the I²C identification bus.",
                SchematicSummarySource: "Blinky (F01) manual, Appendix C - Schematic reference",
                SchematicDrawingReference: "Protomod_Blinky_Rev01.pdf (PROTOVERSE/Finished Modules/PC01_F01_ProtoMod_Blinky/Rev01)",
                Ideas: new[]
                {
                    new ProtoModIdea(
                        "A chasing pattern: light LED1, then LED2, then LED3, then LED4, each one turning off as the next turns on - a simple “marquee” effect",
                        "Creative challenge - Blinky (F01) manual"),
                    new ProtoModIdea(
                        "A back-and-forth scanner: chase from LED1 to LED4, then back again, like a classic scanner light",
                        "Creative challenge - Blinky (F01) manual"),
                    new ProtoModIdea(
                        "A 4-bit binary counter: treat LED1-LED4 as four binary digits and count from 0 to 15 in binary, lighting the LEDs that correspond to each 1",
                        "Creative challenge - Blinky (F01) manual"),
                },
                NextSteps: new[]
                {
                    // The only progression link in the whole catalog that has an
                    // explicit source sentence behind it. Both F01 manuals agree that
                    // F02 comes next; the Gen2 wording is quoted here.
                    new ProtoModNextStep(
                        "F02",
                        "“Once you're comfortable driving LEDs directly with GPIO, move on to Simple LED (F02)” - Blinky (F01) manual, §8 Future ProtoMods for you"),
                },
                ProtocolId: ProtoModId.BlinkyLed,
                ManualReference: "PROTOVERSE/Manuals/Gen2/F01_Blinky_Manual.docx"),

            // ---------------------------------------------------------------- F02
            // This manual has no Creative Challenge section (only the Gen2 template
            // introduced one), so the ideas below are quoted from its guided
            // "Now try this" experiments instead - real manual content, just a
            // different section, which is why each idea carries its own source label.
            new ProtoModCatalogEntry(
                Code: "F02",
                Name: "Simple LED",
                Series: ProtoModSeries.Fundamentals,
                Difficulty: null,   // manual states none
                TimeEstimate: null, // manual states none
                Description: "This ProtoMod shows how supply voltage and resistance work together to control the current through an LED, and how that current changes the LED's on/off behavior and brightness.",
                DescriptionSource: "Core concept - Simple LED (F02) manual, Overview",
                SchematicSummary: "Two LED paths, one red and one green. Each path has a voltage selector switch (SW1 for red, SW2 for green) choosing between 3.3 V and 1.8 V, and a resistor selector switch (SW3 for red, SW4 for green) choosing between 470 Ω and 100 Ω.",
                SchematicSummarySource: "Simple LED (F02) manual, Overview",
                SchematicDrawingReference: "Protomod_simpleLED_Rev01.pdf (PROTOVERSE/Finished Modules/PC01_F02_ProtoMod_simpleLED/Rev01)",
                Ideas: new[]
                {
                    new ProtoModIdea(
                        "Change only the resistor on the red LED. Here you keep the voltage the same and see what happens when you make the current-limiting resistor smaller.",
                        "Try this - Simple LED (F02) manual (no creative challenge section written yet)"),
                    new ProtoModIdea(
                        "Change only the voltage on the red LED. Now you keep the resistor the same and lower the electrical “push” that drives current.",
                        "Try this - Simple LED (F02) manual (no creative challenge section written yet)"),
                    new ProtoModIdea(
                        "Repeat the same experiments with the green LED. The green LED needs a slightly higher forward voltage than the red LED, so it is more sensitive to the lower 1.8 V setting.",
                        "Try this - Simple LED (F02) manual (no creative challenge section written yet)"),
                },
                NextSteps: System.Array.Empty<ProtoModNextStep>(), // manual has no "Future ProtoMods" section
                ProtocolId: ProtoModId.BasicLed,
                ManualReference: "PROTOVERSE/Manuals/F02_Simple_LED.docx"),

            // ---------------------------------------------------------------- E03
            new ProtoModCatalogEntry(
                Code: "E03",
                Name: "Sensors 1",
                Series: ProtoModSeries.Explorers,
                Difficulty: null,
                TimeEstimate: null,
                Description: "This ProtoMod introduces two types of sensors: an STM LIS3DH accelerometer, a digital motion sensor that communicates over I²C, and an Analog Devices TMP36 temperature sensor, an analog sensor that outputs a voltage proportional to temperature.",
                DescriptionSource: "Core concept - Sensors I (E03) manual, Overview",
                SchematicSummary: "An STM LIS3DH MEMS accelerometer reports X/Y/Z acceleration digitally over the I²C bus. An Analog Devices TMP36 outputs roughly 750 mV at 25 °C, rising 10 mV per °C, which ProtoCore's ADC digitizes.",
                SchematicSummarySource: "Sensors I (E03) manual, Background & theory",
                SchematicDrawingReference: "Protomod_Accel_Rev01.pdf (PROTOVERSE/Finished Modules/PC01_E03_ProtoMod_Accelerometer/Rev01)",
                Ideas: new[]
                {
                    new ProtoModIdea(
                        "Read LIS3DH accelerometer values over I²C. Tilt the board and watch X, Y, Z values change.",
                        "Try this - Sensors I (E03) manual (no creative challenge section written yet)"),
                    new ProtoModIdea(
                        "Tap the board lightly - see the spikes in Z-axis acceleration.",
                        "Try this - Sensors I (E03) manual (no creative challenge section written yet)"),
                    new ProtoModIdea(
                        "Read TMP36 voltage. Warm it with your hand and notice the increase.",
                        "Try this - Sensors I (E03) manual (no creative challenge section written yet)"),
                },
                NextSteps: System.Array.Empty<ProtoModNextStep>(),
                ProtocolId: ProtoModId.AccelTemp,
                ManualReference: "PROTOVERSE/Manuals/E03_Sensors1.docx"),

            // ---------------------------------------------------------------- E05
            // No manual has been written for this board - the only writing that exists
            // about it anywhere is this repo's own documentation of its wire format and
            // the open-loop hardware constraint behind it (CLAUDE.md's "Electronic
            // Load's wire format is settled" section, mirrored in
            // ElectronicLoadViewModel). So: description and schematic summary come from
            // there, and ideas are genuinely absent rather than invented.
            // Series follows the confirmed E -> Explorers family rule (see
            // ProtoModSeries) - legitimate to derive even with no manual written.
            new ProtoModCatalogEntry(
                Code: "E05",
                Name: "Electronic Load",
                Series: ProtoModSeries.Explorers,
                Difficulty: null,
                TimeEstimate: null,
                Description: "A programmable current sink: ProtoCore commands a current between 0 and 300 mA and the board draws it, so you can load a supply or source and watch what happens.",
                DescriptionSource: "This repo's own docs - CLAUDE.md “Electronic Load's wire format” + ElectronicLoadViewModel.cs (no module manual written yet)",
                SchematicSummary: "A bit-banged PWM signal from ProtoCore drives an op-amp that forces current through a 10 Ω sense resistor. This board revision is open-loop - there is no ADC feedback path, so it reports the commanded current and the PWM duty cycle it is driving, never a measurement.",
                SchematicSummarySource: "This repo's own docs - CLAUDE.md “Electronic Load's wire format” (no module manual written yet)",
                SchematicDrawingReference: "Protomod_ElectronicLoad.pdf (PROTOVERSE/Finished Modules/PC01_E05_ProtoMod_ElectronicLoad/Rev01)",
                Ideas: System.Array.Empty<ProtoModIdea>(), // no manual -> no real prompts to quote
                NextSteps: System.Array.Empty<ProtoModNextStep>(),
                ProtocolId: ProtoModId.ElectronicLoad,
                ManualReference: null),

            // ---------------------------------------------------------------- A01
            // Has a full manual and finished hardware, but no ProtoModId has been
            // assigned to it on either side of the wire protocol yet - so ProtoCore
            // cannot report it and this app can never mark it installed. That's why
            // ProtocolId is null rather than a guessed value.
            new ProtoModCatalogEntry(
                Code: "A01",
                Name: "Direct Digital Synthesis",
                Series: ProtoModSeries.Advanced,
                Difficulty: null,
                TimeEstimate: null,
                Description: "This ProtoMod introduces direct digital synthesis (DDS) using an AD9837 waveform generator controlled by the ProtoCore over SPI.",
                DescriptionSource: "Core concept - Direct Digital Synthesis (A01) manual, Overview",
                SchematicSummary: "A precision ECS-3225MV crystal oscillator clocks an AD9837ACP DDS chip driven over SPI. Its output passes through a 10 Ω / 2000 pF RC low-pass filter, then an OPA365 op amp powered from +3.3 V and -3.3 V as a non-inverting amplifier with a gain of three, then a 50 Ω series resistor to a coax connector.",
                SchematicSummarySource: "Direct Digital Synthesis (A01) manual, Background & theory",
                SchematicDrawingReference: "Protomod_DDS.pdf (PROTOVERSE/Finished Modules/PC01_A01_ProtoMod_DDS/Rev01)",
                Ideas: new[]
                {
                    new ProtoModIdea(
                        "Generate a sine wave from the AD9837 via the ProtoVerse GUI and view it using an oscilloscope or the GUI's plotting tools at the coax output.",
                        "Try this - Direct Digital Synthesis (A01) manual (no creative challenge section written yet)"),
                    new ProtoModIdea(
                        "Adjust the DDS output frequency from low to high. Notice how the waveform shape stays the same while the spacing between cycles at the 50 Ω coax output changes.",
                        "Try this - Direct Digital Synthesis (A01) manual (no creative challenge section written yet)"),
                    new ProtoModIdea(
                        "Switch between at least two waveform types (for example, sine and square) and compare how their shapes differ at the same frequency using the same output connector.",
                        "Try this - Direct Digital Synthesis (A01) manual (no creative challenge section written yet)"),
                },
                NextSteps: System.Array.Empty<ProtoModNextStep>(),
                ProtocolId: null,
                ManualReference: "PROTOVERSE/Manuals/A01_DDS.docx"),

            // ---------------------------------------------------------------- F00
            // Finished hardware exists (PC01_F00_ProtoMod_Headers, with a Rev01
            // schematic export) and that is the entire extent of what's written about
            // it: no manual, no ProtoModId, no description anywhere in any codebase or
            // doc. Listed here because it is a real board, with every unwritten field
            // left null so the UI says "coming soon" instead of this file guessing at
            // what a headers board is for. Series follows the confirmed
            // F -> Fundamentals family rule (see ProtoModSeries).
            new ProtoModCatalogEntry(
                Code: "F00",
                Name: "Headers",
                Series: ProtoModSeries.Fundamentals,
                Difficulty: null,
                TimeEstimate: null,
                Description: null,
                DescriptionSource: null,
                SchematicSummary: null,
                SchematicSummarySource: null,
                SchematicDrawingReference: "Protomod_Headers_Rev01.pdf (PROTOVERSE/Finished Modules/PC01_F00_ProtoMod_Headers/Rev01)",
                Ideas: System.Array.Empty<ProtoModIdea>(),
                NextSteps: System.Array.Empty<ProtoModNextStep>(),
                ProtocolId: null,
                ManualReference: null),
        };

        /// <summary>Looks up an entry by circuit code (e.g. "F02"), for resolving a
        /// next-step link. Returns null rather than throwing if a link ever points at
        /// a code that isn't in the catalog.</summary>
        public static ProtoModCatalogEntry? FindByCode(string code) =>
            Entries.FirstOrDefault(e => e.Code == code);
    }
}
