# Evaluation: in-app ProtoMod manuals

**Branch:** `eval/in-app-manuals-e05` · **Demo module:** Electronic Load (E05)
· **Date:** 2026-08-31

**Recommendation: proceed, with four changes to the plan.** Build a
`.docx` → content converter *before* transcribing any more manuals; treat
learner-progress persistence as part of v1 rather than a v2 stretch; budget
per-manual time to write multiple-choice questions, which is the one cost a
converter can't absorb and the one thing here a PDF genuinely cannot do; and
resolve content-vs-hardware conflicts before a manual goes in-app, because
rendering one beside live board state makes it more authoritative than a Word
file ever was. Detail below.

> **Both hardware conflicts are now closed.** "E02" was confirmed a typo for
> E05; and since the Word manuals are reference material rather than sources of
> truth, E05's in-app content is now **written against the real open-loop
> board** instead of transcribing the reference doc's closed-loop DAC/ADC
> design. See "the finding I didn't expect" below — it still matters for
> rollout.

---

## What was built

- A content model (`Models/Manual/ManualBlocks.cs`) mapping 1:1 to
  `ProtoVerse_ProtoMod_Manual_Template.docx` — header block, 9 body sections,
  3 appendices, with callouts, figures, images, checklists, step lists with
  inline Observe prompts, fillable value tables, and both free-text and
  multiple-choice question blocks as first-class block types.
- One renderer (`Views/ManualView.xaml`) — a set of `DataTemplate`s keyed by
  block type. **Adding a manual adds data, not XAML.** That was the core claim
  to test, and it held.
- The Electronic Load manual (`Models/Manual/ElectronicLoadManual.cs`) as
  content — five body sections plus one appendix, written against the real
  board's verified behaviour, using `Electronic_Load_E02_Manual.docx` as a
  reference for structure and voice rather than for technical content.
- **Self-marking follow-up questions.** Multiple choice, marked the instant the
  learner answers, with the reasoning revealed either way. See "what actually
  differentiates this from a PDF" below — this displaced both the fillable
  table and the answer-key appendix.
- Schematic assets for all six boards, exported from the KiCad sources: the
  full original PDF linked from the top of the manual, and a cropped
  white-on-black raster of the circuit shown inline in the Overview.
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
new colour and no new control style was needed. (One new converter, an
`InverseBoolConverter`, came later with the multiple-choice work.) For a design
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

**A second data point, added after this document's first draft.** The Blinky
(F01) manual was then written from E05 as a template, and it confirms the core
claim: **it needed no XAML and no new block type** — it is one content file plus
one line in `ManualLibrary`. The renderer really is a fixed cost that is now
paid.

But it also sharpens where the remaining cost actually sits, and it is not
transcription. Two things had to be reconciled before a word could be written,
neither of them visible in the `.docx`:

- **The Gen2 manuals assume the learner writes firmware** ("set LED1's pin
  HIGH"). No such path exists; the app is the whole interface. Every activity
  had to be re-expressed against the real panel.
- **F01's Creative Challenge asks the learner to invent a chase and a
  scanner — both of which firmware already ships as selectable patterns.**
  Transcribed faithfully, it would have asked someone to build what a dropdown
  already does.

Both were settled with the user, along with dropping the manual's references to
a "Logic ProtoMod" that has no circuit code. **A converter would have produced a
manual with all three problems intact**, and none of them would have looked like
a conversion error — which is the strongest argument in this document for the
validation pass recommended below, and for budgeting editorial time per manual
rather than treating conversion as the whole job.

**A third manual, F02, changes one estimate materially.** It confirmed the
renderer again — the content is still pure data — but it needed app work first,
which F01 and E05 hadn't exposed: a passive-board slot type (F02 has no software
controls at all) and a way to mark content that has no source document. So the
"adding a manual is data, not XAML" claim holds for content and does *not* hold
for a new **kind** of board. Budget for that occasionally, not per manual.

**The bigger finding is about the converter, and it is worth checking before
committing the day.** The converter estimate above assumes manuals follow
`ProtoVerse_ProtoMod_Manual_Template.docx`. Counting what actually exists in
`PROTOVERSE/Manuals/`:

| Format | Module manuals |
|---|---|
| Gen2 (follows the template) | 2 — E02/E05 and F01 |
| Older, pre-template | 4 — A01, E03, F02, and an older F01 |

So **the format the converter would target is currently the minority**. F02's
older manual has no creative challenge, no facilitator notes, and states no
difficulty or time — those are template sections that simply do not exist in the
source, and no converter can produce them. That is not a defect in the older
manuals; they were written before the template. But it means the realistic
sequence is **bring the manuals up to the template first, then write the
converter** — otherwise the converter is being built against documents most of
which it cannot fully consume.

Two things follow. Where a gap has to be filled anyway, mark it: `NeedsReview`
callouts render in place and count into a banner, so an estimate can ship as an
estimate rather than quietly becoming a fact — F02 carries three. And the day
estimated for the converter should be read as *conditional on the manuals being
templated*, which is itself unbudgeted work.

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

**And a third conflict, of a different and more troubling kind.** The first two
are manual-versus-hardware. This one is **docs-versus-hardware**: `CLAUDE.md`
and firmware's own source both describe `SetCurrentLimitMa` as rejecting
anything above `MAX_CURRENT_MA` (300) with `PROTOCOL_ERR_BAD_VALUE`. The real
board does not. Commanding 400 mA returns a normal `Response` — 400 mA echoed,
100% duty — reported by the user against the bench board on 2026-08-31.

That matters to this evaluation more than a content bug would, for two reasons.
First, it was written into the manual *as a teaching point*, complete with a
question asking why an explicit refusal is safer than a silent clamp. Writing
in-app content against project documentation rather than against the board is
the same failure mode as transcribing a manual that describes different
hardware — it just arrives from a source that felt trustworthy. Second, the
real behaviour teaches the module's point better: the echo confirms nothing,
the duty has run out of headroom, and the readout and the circuit have quietly
stopped agreeing with no way for the board to say so. **Recommendation 5 below
should be read as covering the app's own docs, not just the Word manuals.**

**How the circuit conflict was resolved:** the Word manuals are reference
material for the *kind* of content wanted, not sources of truth. So E05's
in-app manual is now written against the board that exists — open-loop, PWM,
10 Ω, no measurement path — keeping the reference doc's structure and voice.
That turned out better than a transcription would have been, because the real
board teaches something the reference version couldn't: the difference between
a value an instrument was *told* and a value it *measured*. The four
discrepancy callouts are gone; there's nothing left to flag.

The conflict is still worth recording, because the rollout lesson survives it.
The circuit mismatch wasn't a values nit. Sections 4 and 5 ask the learner to watch
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

- **A PDF viewer for the schematic** — though this got much better. See below:
  the schematic is now generated, cropped, shipped with the app and one click
  from the top of the manual, rather than being a path the learner has to go
  find. It still opens in the system viewer, deliberately, because zooming and
  panning a schematic beside the app is what a real viewer is for.
- **Setup and figure photos.** The manuals carry literal `[ figure / photo ]`
  markers and no such images exist. In-app manuals don't fix that; taking the
  photos does.
- **Word itself, for anyone authoring or editing.**

### In-app manuals make some print sections redundant

Not a rendering problem, a content one, and it only became obvious once the
manual was rendered in place: **the assembly steps were removed** because the
manual is reachable only when the board is already seated and enumerated. "Insert
the ProtoMod into any slot, power the ProtoCore, open the app, click Identify
slots" is sound in a printed manual — the reader may not have started — and
absurd next to a live panel showing the board already running. The same logic
trimmed "You'll need" down to the multimeter, since the board, ProtoCore and
USB cable are self-evidently present.

This cuts against the templated-conversion plan in a small way: a converter
that faithfully reproduces all twelve template sections will faithfully
reproduce content that no longer makes sense in context. Expect a per-manual
editorial pass, not just a conversion, and consider marking sections in the
template as print-only.

### Schematics turned out to be a solved problem

Worth calling out because it removes what I first listed as a permanent
caveat. `tools/build_schematics.ps1` produces two assets per board from the
KiCad sources, and takes seconds to re-run — **schematics are a build step,
not per-manual work.**

- **`{CODE}_schematic.pdf`** — the complete original drawing, copied verbatim
  from `Finished Modules`, border and title block intact. This is what the
  manual's schematic button opens: someone opening the full drawing wants the
  full drawing, revision block and all.
- **`{CODE}_circuit.png`** — a cropped **white-on-black** raster of just the
  circuit, shown inline in the Overview. Made by exporting SVG with kicad-cli's
  exclude-drawing-sheet option, tightening the viewBox to the drawn content,
  recolouring every stroke and fill white over a black page, and rasterising
  with **headless Edge** — there is no SVG rasteriser, PDF tool or Python on
  this machine, and Edge is on every Windows box.

**The monochrome step is not decoration.** KiCad's palette (dark red wires,
teal pins, green junctions) is meaningful in KiCad and meaningless in a manual,
and dropping a white-background drawing into a near-black app reads as a lit
rectangle pasted onto the page. Recolouring is a two-line regex over the SVG
before rasterising, so it costs nothing per board — worth knowing, because the
obvious alternative (inverting the finished PNG) would also invert the colours
into muddy pastels rather than producing clean white lines. The linked PDF
keeps KiCad's colours: it opens in its own viewer, where they're at home.

Two things that had to be got right, both found by looking at the output
rather than the numbers:

- **The bounds must include text, but not all of it.** Measuring only `<path>`
  and `<circle>` geometry shaved reference designators and pin values off the
  edges. Including every `<text>` instead over-corrected: kicad-cli leaves the
  sheet's own title text on the page even with the drawing sheet excluded, and
  that stranded label stretched the crop by ~60 mm of whitespace. The fix is a
  second pass that takes text near the circuit and ignores text far from it.
- **`CopyToOutputDirectory="PreserveNewest"` silently kept a stale copy.**
  `Copy-Item` preserves the source timestamp, and these schematics are dated
  2025, so MSBuild judged the file already in `bin\` to be newer and left it —
  the tool reported success while the app kept shipping the previous asset.
  The tool now stamps each copied PDF with the current time.

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
| **Print to PDF** | **Real, for this product** | Word/PDF prints; this doesn't. Classroom worksheets and offline use are a plausible need for an education product. Note the tension with self-marking questions: the thing that most justifies the in-app format is also the thing that cannot be printed, since a printed multiple-choice question is just a quiz with the answers on another page. |
| **Text scaling / low-vision** | **Real** | Font sizes are fixed px throughout and don't honour Windows text scaling. Word and tagged PDF have mature accessibility tooling this doesn't match. |
| Screen-reader access | Low | WPF exposes UI Automation for free — the entire demo was driven and verified through UIA, which is decent evidence the tree is navigable. Not audited against a real screen reader. |
| Shareable links | None | Nothing is shared today; manuals are local files. |
| Offline use | None | Everything is local either way. |
| Losing the Word source | None | Word stays the authoring surface under every option here. |

---

## Stretch: should the interactive parts be real?

**Revised down, then revised back up — but around a different mechanism.**

**What didn't work: the fillable value table.** It was built and then **removed
at the user's request** — "not filled out, awkward and useless in this case".
That is worth taking seriously, because E05 was the strongest candidate in the
catalog for it: the one board with a dial to turn and a number to read back. If
the pattern doesn't earn its place there, the case for it being *the*
differentiator over a PDF is weak.

What went wrong is the framing, not the mechanism. A blank five-row grid asking
a learner to predict duty cycles is homework, and it sat in the middle of a
section they were reading rather than doing. A worksheet wants to be something
you open deliberately, not something you scroll past.

So: **don't build interactive tables into the manual flow.** If the idea comes
back, it should be a separate, opt-in surface, tested on one module before
being designed for the catalog.

### What actually differentiates this from a PDF: self-marking questions

The follow-up questions were free text, which is exactly what paper already
does — and does no worse, since nothing marks either one. Converting them to
multiple choice changed what the format *is*: the app holds the correct answer,
so it marks the question the moment it's answered and shows the reasoning
whether the learner was right or wrong. Paper cannot do that. Neither can a
PDF.

Three consequences worth carrying into rollout:

1. **It removes the answer-key appendix rather than adding to it.** With the
   explanation attached to its own question, a key is the same text in a second
   place — free to drift, and gating answers to questions already answered.
   E05 went from two appendices to one. Content that marks itself is *less*
   content, not more.
2. **The spoiler gate moved down a level and got better.** Instead of one
   reveal button unlocking every answer at once, each question opens only its
   own explanation, only after it's been committed to. `IsSpoiler` on a section
   is still in the model for manuals that need it, but nothing uses it now.
3. **It is cheap per manual and mechanical to author** — a prompt, four
   options, an index, an explanation. But it's *not* mechanical to convert:
   distractors have to be written, and a `.docx` with free-text questions
   contains no distractors to convert. This is a real cost the converter can't
   absorb; budget authoring time per manual, or accept free text where nobody
   writes them.

**A caution the E05 questions illustrate.** A question is only as sound as the
behaviour it describes. One of these five originally asked why refusing an
out-of-range command is safer than clamping it — a good question about a thing
the board does not do (see the hardware conflicts section). Multiple choice
raises the stakes on that error: free text leaves a learner room to disagree
with the premise, whereas a marked answer tells them, with the app's authority,
that the wrong premise is correct.

The persistence argument survives all of this, and applies to the things that
*did* stay:

**Ticked steps and revealed answers should persist, and I'd still pull that
into v1.**

The reasoning is a defect this demo already has. Ticked steps and answered
questions are held in memory only — and because `SlotViewModel` rebuilds its
`ManualViewModel` on every `PresenceReport`, **hot-swapping any module resets
every answer the learner has committed to.** Firmware sends unsolicited
presence reports on hot-swap, so this isn't hypothetical.

Self-marking questions make this sharper, not milder. A first answer is meant
to stand — that's what stops the question becoming a lock to pick — so silently
resetting it hands back the ability to "answer" a question whose explanation
has already been read. Paper doesn't lose your work when someone bumps a
board.

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

0. **Bring the existing manuals onto the template before anything else.** Only
   2 of the 6 module manuals follow it today; the other 4 are missing sections
   the template defines. This wasn't visible until a pre-template manual (F02)
   was actually built, and it gates the item below.
1. **Then build the `.docx` → JSON converter** (~1 day, *conditional on the
   above*). Do not transcribe manuals by hand; it doesn't scale to the catalog
   you're targeting and it drifts from the Word source immediately.
2. **Include progress persistence in v1** (~half a day). Self-marking questions
   are the real differentiator over a PDF, and unsaved they reset on any
   hot-swap — handing back answers the learner has already seen explained.
3. **Budget question-authoring time per manual.** Multiple choice is what makes
   the format worth having, and a Word manual full of free-text questions
   contains no distractors for a converter to lift. This is the one per-manual
   cost the converter genuinely cannot absorb.
4. **Only fix the value-table layout if the table comes back** (~half a day).
   It was removed from E05; the nested-`UniformGrid` alignment issue below is
   dormant, not live.
5. **Only surface a manual in-app once it's complete *and* agrees with the
   hardware.** A half-written manual sends the learner back to Word with two
   conflicting sources; a manual describing the wrong board is worse, because
   sitting next to live slot state makes it look authoritative. Multiple choice
   raises this stake: a marked answer asserts a premise on the app's authority.
6. **Have the converter validate, not just convert** — at minimum, check the
   manual's stated module code against `ProtoModBoardCatalog` and refuse to
   emit content for a code no board reports.
7. **Decide separately about print.** It's the one capability that genuinely
   goes away, and for a classroom product it may matter more than everything
   above.

Not blockers, but decide before rollout: whether figures get drawn (Appendix C
is a PDF reference until they do), and whether the one-module-at-a-time
workspace needs to handle exercises spanning two ProtoMods.

---

## Verified

Driven through UI Automation in Simulator mode. The navigator shows three
slots with correct presence dots and "Manual available" only on the Electronic
Load; selecting it renders the header, metadata row and TOC; ticking a step
works; a slot with no manual shows the explanatory placeholder. (Earlier runs
also verified typing into a value-table cell and the "Reveal answers" spoiler
gate; both features have since been removed from this manual.)

The six changes requested on 2026-08-31 (evening), each verified:

| # | Change | Verified |
|---|---|---|
| 1 | Indent body under section headings | body text sits 16 px right of its heading |
| 2 | TOC tracks the section being read | scrolling to 15/35/60/85% moved the highlight through sections 2 → 3 → 4 → 5 |
| 3 | Fewer, better-grouped sections | 8 body + 3 appendices → **5 body + 2 appendices** |
| 4 | Schematic PDF linked at top | button present, resolves to the cropped `E05_schematic.pdf` shipped beside the exe |
| 5 | Drop "Try this" from Library cards | no "TRY THIS" heading and no quoted idea text remains |
| 6 | Library search by name or code | "load" → E05; "E0" → E03+E05; "zzz" → none, with a no-match message; clear restores all six |

The three changes requested later on 2026-08-31, each verified:

| # | Change | Verified |
|---|---|---|
| 1 | Multiple choice in section 5 | 20 option rows (4 × 5 questions). No mark and no explanation before answering; answering Q1 wrongly shows "Not quite", reveals that question's explanation only, and leaves the other four hidden; re-clicking the correct option does not change the mark; answering Q4 and Q2 correctly brings the count to 3 marks, 2 correct |
| 2 | 400 mA behaviour corrected | "rejects it outright" and "produces an explicit error" gone; pegged-duty bullet and overshoot step present; answer-key appendix gone, section list now 5 body + 1 appendix |
| 3 | Monochrome schematic | regenerated PNG viewed directly — white on black, every designator, value and pin name intact; card behind it black |

Two process notes worth recording. An intermediate build failed with a XAML
`error MC3000` that a too-narrow grep (`error CS|error MSB`) hid, so one round
of testing ran against a stale executable. Same failure mode as the stale-lock
problem noted in `CLAUDE.md` — when scripting a build, match `error` broadly.
And a UIA assertion briefly appeared to show explanation text leaking before
the question was answered; it hadn't — the regex was loose enough to also match
a sentence legitimately on screen elsewhere in the manual. A loose assertion
fails in the direction of inventing defects, which costs more than it saves.

**Not verified: appearance.** Screenshot capture returns a blank client area
for this app in this environment (documented in `CLAUDE.md`), so the layout
has been verified structurally and behaviourally but nobody has looked at it.
Spacing, density and whether the manual is genuinely readable at this width
are unassessed.
