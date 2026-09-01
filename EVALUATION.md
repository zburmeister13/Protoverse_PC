# Evaluation: in-app ProtoMod manuals

**Branch:** `eval/in-app-manuals-e05` · **Demo module:** Electronic Load (E05)
· **Date:** 2026-08-31

**Recommendation: proceed, with three changes to the plan.** Build a
`.docx` → content converter *before* transcribing any more manuals; treat
learner-progress persistence as part of v1 rather than a v2 stretch; and
resolve manual-vs-hardware conflicts before a manual goes in-app, because
rendering one beside live board state makes it more authoritative than a Word
file ever was. Detail below.

> **Still needs your decision before this branch goes further.** The supplied
> Electronic Load manual describes a **closed-loop DAC/ADC design** where the
> real board is **open-loop PWM with no measurement path**. (The module-code
> conflict is resolved — "E02" was confirmed a typo for E05.) See "the finding
> I didn't expect" below.

---

## What was built

- A content model (`Models/Manual/ManualBlocks.cs`) mapping 1:1 to
  `ProtoVerse_ProtoMod_Manual_Template.docx` — header block, 9 body sections,
  3 appendices, with callouts, figures, checklists, step lists with inline
  Observe prompts, fillable value tables, and question lists as first-class
  block types.
- One renderer (`Views/ManualView.xaml`) — a set of `DataTemplate`s keyed by
  block type. **Adding a manual adds data, not XAML.** That was the core claim
  to test, and it held.
- The Electronic Load manual (`Models/Manual/ElectronicLoadManual.cs`) as
  content, transcribed from `Electronic_Load_E02_Manual.docx` — a complete
  12-section manual written against the template.
- A rebuilt Slots tab: a left-hand navigator showing all three physical slots
  with a presence dot, and a workspace per slot with the module's live control
  panel docked above its manual.

### Deviations from the original spec

| Spec said | Actual | Why |
|---|---|---|
| Demo Blinky F01 | Electronic Load E05 | Your call. Also the better choice — see "the fillable table" below. |
| Branch off `main` | Branched off `feature/protomod-library` | The account store that progress persistence needs lives there, and both branches restructure the same `MainWindow`. |
| Branch `eval/in-app-manuals-blinky-f01` | `eval/in-app-manuals-e05` | Module changed. |

---

## Feasibility: what was actually hard

**Easy, and cheaper than expected.** Callouts (four variants — Tech note,
Observe, Reassurance, Placeholder — off one template with triggers on a `Kind`
enum), figure placeholders, spoiler-gating the answer key, and the
appendix/body split. Each is a handful of lines.

**The app's existing styles stretched without complaint.** Everything is drawn
with the `App.xaml` brushes and implicit control styles already in the app; no
new colour, no new control style, no new converter was needed. For a design
system that grew out of one hardware-control window, that's a better result
than expected.

**Hard, and worth knowing about:**

1. **Fillable tables — the weakest part of the build.** WPF has no table
   primitive that handles "N fixed columns then M editable columns" with
   ragged rows. The current implementation nests `UniformGrid`s, which
   *aligns by convention rather than by constraint* — the header row and the
   body rows are separate `ItemsControl`s that happen to divide the same
   width. It looks right and it works, but a row with a different cell count
   would silently misalign. A `Grid` with `SharedSizeGroup` columns would be
   correct; it's maybe half a day to redo properly and should be done before
   a second table-bearing manual ships.

2. **TOC section jump needs code-behind.** There's no MVVM-friendly binding
   for "scroll this generated container into view", so it's a visual-tree walk
   plus `BringIntoView` (~40 lines). Same pattern the Library tab already uses.
   Not a problem, just not declarative.

3. **One thing the component library genuinely lacked:** there was no shared
   "callout" or "section heading" style, because nothing in the app had needed
   one. They currently live in `ManualView.xaml`'s own resources. If manuals
   proliferate — or if callouts start appearing outside manuals — they should
   move to `App.xaml`.

---

## Effort to scale

**The renderer is a fixed cost and it's now paid.** Per-manual cost is
authoring content, which is pure data. But *how* that data gets authored is
the whole question, and it's where I'd change the plan.

**Measured, not estimated:** transcribing the complete Electronic Load manual
(~2,900 words, 12 sections, 7 tables) into the content model took roughly
20–30 minutes. That's cheaper than I first guessed, and it revises down an
earlier estimate in this document's first draft.

**But still: don't hand-transcribe manuals into C#.** Per-manual time isn't
the argument — drift is. The `.docx` is the authoring surface and always will
be; nobody is going to write curriculum in a `.cs` file. Every hand
transcription is a fork that starts diverging the moment someone opens Word,
and there's no mechanism to detect that it has.

**Build a `.docx` → content converter instead.** This is more tractable than
it sounds, because the template is rigid and the filled manuals genuinely
follow it:

- A `.docx` is a zip containing OOXML; extracting structured text is already
  something this project does reliably (it's how the Library's catalog
  content was sourced).
- Headings map to sections, single-row tables map to callouts, numbered lists
  map to steps, multi-column tables map to value tables. The mapping is
  mechanical because the template made it so.
- Estimate: **~1 day** for a converter handling the template's block
  vocabulary, plus a half day of per-manual cleanup for docs that stray from
  it. After that each manual is close to free and stays in sync with the Word
  source.

Compare: hand-transcription is ~1.5h × every manual, forever, plus drift. The
converter pays for itself at roughly manual #5, and there are 6 boards today
against a stated ambition of a 1,000+ module catalog.

**Corollary:** once there's a converter, the content should be generated
**JSON** loaded at runtime, not C#. C# was right for this spike (compile-time
checking of the block model while it was still changing) and is wrong for
generated content.

**Realistic per-manual cost, both ways:**

| | With converter | By hand |
|---|---|---|
| Module with a written Word manual | ~15 min cleanup | 20–30 min (measured) |
| Module with no manual (F00) | Bottleneck is *writing the manual*, not the app — the app cost is zero either way |

---

## The finding I didn't expect: transcription surfaces content/hardware conflicts

The supplied manual **describes different hardware than the board this app
talks to**, in two ways:

| | The manual says | The board actually is | Status |
|---|---|---|---|
| Module code | **E02** throughout | **E05** — EEPROM code, `ProtoModBoardCatalog`, and the folder `PC01_E05_ProtoMod_ElectronicLoad` all agree | **Resolved** — confirmed a typo. The transcription says E05; the `.docx` still says E02 and should be fixed at source, or a converter will reintroduce it. |
| Circuit | Power MOSFET with op-amp feedback loop, **1 Ω** sense resistor, ProtoCore's **DAC** sets the target and its **ADC reads back live voltage and current** | **Open-loop**: bit-banged PWM into an op-amp, **10 Ω** sense resistor, **no ADC feedback path at all** | **Open** |

The circuit one isn't a values nit. Sections 4 and 5 ask the learner to watch
voltage sag on screen while current holds steady — and this board revision can
show neither quantity. It's the same constraint that led to removing the
Electronic Load panel's chart rather than plotting numbers the hardware can't
produce. The manual's own Appendix C flags its component values as
provisional, so the likeliest reading is that it describes an intended or
revised board; but that's a guess, and it needs your answer, not mine.

**Why this matters to the evaluation rather than just being a content bug:**

1. **A wrong manual is more dangerous in-app than in Word.** Rendered beside
   live board state, it borrows that state's credibility — the learner
   reasonably assumes the app wouldn't show them a manual for a different
   board. A `.docx` sitting in a folder makes no such implicit claim. The demo
   handles this with a `Discrepancy` callout kind that renders as the loudest
   thing on the page and is explicitly attributed as an app-side note rather
   than manual content (four of them appear), but the *only* real fix is that
   the content and the hardware agree.
2. **Expect this during rollout, and budget for it.** Transcribing forces
   someone to read each manual against what the app and firmware actually do.
   That's genuinely valuable — this conflict has presumably existed since the
   manual was written and nobody noticed — but it means per-manual cost
   includes reconciliation time that has nothing to do with the app.
3. It argues for the converter doing a **validation pass**, not just a
   conversion: cross-check the manual's stated module code against
   `ProtoModBoardCatalog`, and flag manuals whose code doesn't match any known
   board. **The E02/E05 typo is proof this pays for itself** — a four-line
   check would have caught, automatically and at conversion time, an error
   that had sat unnoticed in the document and took a human to adjudicate.
   Extending the same idea to component values (a manual asserting a 1 Ω sense
   resistor where the app's own docs say 10 Ω) is harder but the same shape,
   and would have caught the open question below too.

---

## Does this actually remove a window?

**Yes, but less than the framing assumes, and only conditionally.**

What goes away: today there is no in-app manual and *no link to one*. The
manuals are `.docx` files in `PROTOVERSE/Manuals/`, which isn't even in this
repo. A learner opens Explorer, finds the file, opens Word. That whole loop
goes away.

What does **not** go away:

- **The schematic.** Appendix C references `Protomod_ElectronicLoad.pdf` in
  the hardware tree. No manual in this project has real figures — the Word
  sources carry literal `[ figure / photo ]` markers — so a learner who wants
  to see the circuit still opens a PDF. In-app manuals don't fix this;
  drawing the figures does.
- **Word itself, for anyone authoring or editing.**

And one genuine risk of making things *worse*: **a partially-transcribed
manual is worse than no in-app manual.** The learner opens the app, finds half
the sections are "not written yet", and opens Word anyway — now looking at two
sources that can disagree. An earlier draft of this demo was six-twelfths
placeholder and showed exactly that; the UI handles it honestly (an orange
banner counts the placeholder sections and names the provenance), but the real
mitigation is sequencing — convert a manual completely, or don't surface it
in-app at all. The current demo is the complete case, which is markedly more
convincing, and that contrast is itself the argument for the sequencing rule.

---

## Regression risk

| Risk | Severity | Notes |
|---|---|---|
| **Print to PDF** | **Real, for this product** | Word/PDF prints; this doesn't. Classroom worksheets and offline use are a plausible need for an education product, and the fillable value table is exactly the thing a facilitator would want to hand out on paper. No mitigation currently. |
| **Text scaling / low-vision** | **Real** | Font sizes are fixed px throughout and don't honour Windows text scaling. Word and tagged PDF have mature accessibility tooling this doesn't match. |
| Screen-reader access | Low | WPF exposes UI Automation for free — the entire demo was driven and verified through UIA, which is decent evidence the tree is navigable. Not audited against a real screen reader. |
| Shareable links | None | Nothing is shared today; manuals are local files. |
| Offline use | None | Everything is local either way. |
| Losing the Word source | None | Word stays the authoring surface under every option here. |

---

## Stretch: should the interactive parts be real?

**Yes, and I'd pull it into v1 rather than leaving it as v2.**

The reasoning is a defect this demo already has. The table cells and answer
fields accept input, but nothing is saved — and because `SlotViewModel`
rebuilds its `ManualViewModel` on every `PresenceReport`, **hot-swapping any
module while you have work typed in silently discards it.** Firmware sends
unsolicited presence reports on hot-swap, so this isn't hypothetical.

An unsaved fillable field is worse than paper: paper doesn't lose your
measurements when someone bumps a board. Shipping the interactive table
without persistence would be a regression against the PDF it's meant to beat.

**The good news is that it's mostly already done.** `Models/Manual/ManualProgress.cs`
defines the shape, and every interactive block already carries a stable
persistence key (`sectionId/blockIndex/itemIndex`, authored ids rather than
list positions, so inserting a step doesn't reattach someone's answer to a
different question). The storage exists too: `Services/AccountStore` already
persists per-account, per-module records to `%AppData%`, with account
switching, sign-out and IO-error handling solved. Adding
`ManualProgress? ManualProgress` to `ModuleRecord` is the entire storage story
— no new file, no new lifecycle.

Estimate: **~half a day**, including load-on-open and save-on-change.

Checking off "Now try this" steps and remembering the last-read section fall
out of the same mechanism for free.

---

## Recommendation

**Proceed, with changes:**

1. **Build the `.docx` → JSON converter first** (~1 day). Do not transcribe
   manuals by hand; it doesn't scale to the catalog you're targeting and it
   drifts from the Word source immediately.
2. **Include progress persistence in v1** (~half a day). The interactive table
   is the main differentiator over a PDF, and unsaved it's a downgrade.
3. **Fix the value-table layout properly** (~half a day) before a second
   table-bearing manual ships.
4. **Only surface a manual in-app once it's complete *and* agrees with the
   hardware.** A half-written manual sends the learner back to Word with two
   conflicting sources; a manual describing the wrong board is worse, because
   sitting next to live slot state makes it look authoritative.
5. **Have the converter validate, not just convert** — at minimum, check the
   manual's stated module code against `ProtoModBoardCatalog` and refuse to
   emit content for a code no board reports.
6. **Decide separately about print.** It's the one capability that genuinely
   goes away, and for a classroom product it may matter more than everything
   above.

Not blockers, but decide before rollout: whether figures get drawn (Appendix C
is a PDF reference until they do), and whether the one-module-at-a-time
workspace needs to handle exercises spanning two ProtoMods.

---

## Verified

Driven through UI Automation in Simulator mode: the navigator shows three
slots with correct presence dots and "Manual available" only on the Electronic
Load; selecting it renders the header (Explorers Series · Module E05), the
metadata row (Intermediate / 45–60 min / Simple LED (F02)), the provenance
note, and all 11 TOC entries; 2 Tech notes, 1 Observe prompt, 2 figure
placeholders, 4 discrepancy callouts, the reassurance callout and 0
placeholders (this manual is complete) render; 13 checkboxes and 15 text
fields present; typing into a value-table cell and ticking a step both work;
"Reveal answers" removes the gate and shows the real Appendix A answer key; a
TOC click scrolls the target section to y=188; a slot with no manual shows the
explanatory placeholder instead.

One process note worth recording: an intermediate build failed with a XAML
`error MC3000` that a too-narrow grep (`error CS|error MSB`) hid, so one round
of testing ran against a stale executable. Same failure mode as the stale-lock
problem noted in `CLAUDE.md` — when scripting a build, match `error` broadly.

**Not verified: appearance.** Screenshot capture returns a blank client area
for this app in this environment (documented in `CLAUDE.md`), so the layout
has been verified structurally and behaviourally but nobody has looked at it.
Spacing, density and whether the manual is genuinely readable at this width
are unassessed.
