# Evaluation: in-app ProtoMod manuals

**Branch:** `eval/in-app-manuals-e05` · **Demo module:** Electronic Load (E05)
· **Date:** 2026-08-31

**Recommendation: proceed, with two changes to the plan.** Build a
`.docx` → content converter *before* transcribing any more manuals, and treat
learner-progress persistence as part of v1 rather than a v2 stretch. Detail
below.

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
- The E05 manual (`Models/Manual/ElectronicLoadManual.cs`) as content.
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

**Do not hand-transcribe manuals into C#.** Transcribing E05 by hand took the
better part of an hour for a module that had almost no written content. A full
manual (F01's Gen2 doc is ~10,000 words across 12 sections) would be 1–2 hours
of careful, error-prone copying — and it *drifts the moment someone edits the
Word file*, which is the actual authoring surface and always will be. Nobody
is going to write curriculum in a `.cs` file.

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
| Module with a written Word manual (F01, F02, E03, A01) | ~15 min cleanup | 1–2 h |
| Module with no manual (E05, F00) | Bottleneck is *writing the manual*, not the app — the app cost is zero either way |

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
manual is worse than no in-app manual.** The learner opens the app, finds six
of twelve sections are "not written yet", and opens Word anyway — now looking
at two sources that can disagree. The E05 demo shows exactly this state, on
purpose, because it's the state most manuals will be in during a rollout. The
UI is explicit about it (an orange banner counts the placeholder sections and
names the content's provenance), but the mitigation is really sequencing:
convert a manual completely, or don't surface it in-app at all.

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
4. **Only surface a manual in-app once it's complete.** A half-written manual
   sends the learner back to Word with two conflicting sources.
5. **Decide separately about print.** It's the one capability that genuinely
   goes away, and for a classroom product it may matter more than everything
   above.

Not blockers, but decide before rollout: whether figures get drawn (Appendix C
is a PDF reference until they do), and whether the one-module-at-a-time
workspace needs to handle exercises spanning two ProtoMods.

---

## Verified

Driven through UI Automation in Simulator mode: navigator shows three slots
with correct presence dots and "Manual available" only on E05; selecting the
slot renders the header, metadata row (correctly showing "Not rated" / "Not
stated" — E05 has no stated difficulty), the provenance banner, and all 11 TOC
entries; 3 Tech note callouts, 4 Observe prompts, 2 figure placeholders, the
reassurance callout and 5 of 6 placeholder callouts render (the sixth is
inside the gated answer key, correctly hidden); 14 checkboxes and 22 text
fields present; typing into a table cell and ticking a step both work;
"Reveal answers" removes the gate and shows Appendix A; a TOC click scrolls
the target section from y=658 to y=126; a slot with no manual shows the
explanatory placeholder instead.

**Not verified: appearance.** Screenshot capture returns a blank client area
for this app in this environment (documented in `CLAUDE.md`), so the layout
has been verified structurally and behaviourally but nobody has looked at it.
Spacing, density and whether the manual is genuinely readable at this width
are unassessed.
