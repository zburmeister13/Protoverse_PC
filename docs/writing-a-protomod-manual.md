# Writing a new ProtoMod manual

Companion to [`adding-a-protomod.md`](adding-a-protomod.md), which covers wiring a
board into the app's code. This one is about the manual's *content* — the part no
checklist step can do for you, since it means reading a source document and making
real editorial judgment calls.

**The one rule every other rule in this file serves: quote or summarize real source
material, or leave the field `null`.** Every description, schematic summary, project
idea, and progression link in this app is held to that standard already (see
`ProtoModLibraryCatalog.cs`'s doc comment and CLAUDE.md) — a manual is where that
standard matters most, since it's the thing a learner trusts most directly. Do not
fill a gap with something that "sounds right." Write the manual first, then quote it;
if there's nothing to quote, leave it as "coming soon" and say so.

---

## 1. Figure out which family this board is in, and what that implies

Family is the **one exception** to "never derive, only quote" — it's mechanical, not
editorial: the first letter of the circuit code decides it, always.

| Code prefix | Family | What's confirmed about pitching a manual for it |
|---|---|---|
| `F` | Fundamentals | **Assume almost nothing.** First-touch electronics. The learner has never used a multimeter — treat that as the default, not an edge case. Build every concept in plain language *before* naming it, then tie it to something the panel visibly does. Explain a term the first time it appears rather than assuming the reader has met it elsewhere. Prefer more, smaller steps, with an `Observe` prompt on nearly every one. |
| `E` | Explorers | Mid-series. Can assume the reader already accepts Ohm's law, can read a schematic, and owns a multimeter. `ElectronicLoadManual.cs` (E05) is written at this level and is the template every new manual starts from structurally (see §2) — but its *voice*, not just its structure, assumes that background. Don't copy E05's opening line ("an electronic load is a resistor you set in software") into an F-series manual; it assumes exactly the knowledge F-series can't. |
| `A` | Advanced | **Not yet established by precedent.** No Advanced-family manual has been written in-app yet (A01/DDS exists only in the Library catalog's reference metadata, pointing at a `.docx` — it has no `ManualLibrary` entry). Don't invent an "Advanced pitch" by extrapolating past E's — confirm the intended level with the user before treating any specific calibration as settled, the same way F01's three real adaptations (§4) were each confirmed rather than assumed. |

This rule is confirmed as of 2026-08-31; if it's ever in question, that's still the
answer, and the Library tab's family filter is built on it directly.

## 2. Start from the settled template shape, not the original Word template

`ElectronicLoadManual.cs` (E05) is the reference implementation. Match its shape, not
the source `.docx`'s full twelve-section template — fewer, better-grouped headings
read better in-app:

1. **Overview** — core concept in plain language, circuit image inline
   (`ImageBlock`, built by `tools/build_schematics.ps1` from the KiCad source), the
   full schematic PDF linked at the top of the page (`ManualDocument.SchematicFile`)
   rather than embedded — a learner wants to zoom/pan/keep it open beside the app,
   which a dedicated PDF viewer already does better than this app could.
2. **How it works**
3. **Set up and try it** — **its section `Id` must be the literal string `"setup"`**,
   in every manual, no exceptions. This isn't a style preference: the account
   sign-in/manual-progress control lock (`SlotViewModel.IsControlLocked`, see
   CLAUDE.md) keys off that exact id to decide when a learner has earned access to
   the board's live controls. A different id silently breaks that feature for this
   board — nothing will crash, the board's controls will simply never unlock.
4. **What you should see**
5. **Go further**
6. *(Appendix)* **Facilitator notes**

No separate "observations" section — steps carry their own inline `Observe` prompt
(`ManualStep.Observe`) right where the observation is relevant, not batched at the
end. No assembly-steps section — an in-app manual is only reachable once ProtoCore
has already identified the board, so "plug this in" is moot by construction. Prefer
`MultipleChoiceBlock` over `QuestionsBlock` for anything with a checkable answer: it
marks itself the instant the learner answers, with the reasoning shown either way,
which is the one thing this format can do that a printed manual can't — and it means
no separate answer-key appendix, since there's no key to hide.

Adjust the shape where the actual board genuinely differs (E05 has no data table
because open-loop hardware can't measure anything to put in one) — that's a real
difference, not a template violation. Don't drop a section just because it's more
work to write.

## 3. Reconcile with real, current hardware/firmware behavior — not the source docx

The Word manuals in `PROTOVERSE/Manuals/` were often written before, or independent
of, the firmware that actually shipped. Two distinct situations, two different
callouts — do not conflate them:

- **The manual and the hardware disagree.** Use `CalloutKind.Discrepancy`. This is an
  app-side note, not authored manual content — always attributed as such, never
  silently resolved by guessing which side is right. Rendering a manual next to live
  board state makes it read as more authoritative than a Word file ever was, so an
  unresolved discrepancy shown beside real hardware is the worst-case outcome this
  format can produce. Resolve it with a real source (a firmware read, a schematic, a
  second opinion from whoever owns that side) before shipping the manual, not after.
- **The manual asks for something the app/firmware can't do, or already does
  automatically.** Rewrite the activity, don't quote it as-is. Two concrete patterns
  already hit, both worth checking for on every new manual:
  - The source manual assumes the learner programs the board directly ("set LED1's
    pin HIGH") — there is no such path; this app *is* the whole interface. Re-express
    the activity against the real panel, and say so outright ("clicking this
    indicator is what driving that pin HIGH means here") — the concept doesn't land
    if the bridge between the old wording and the real control is left implicit.
  - A "Creative Challenge"/"try this" activity asks the learner to build something
    firmware already ships as a selectable option (F01's manual asked for a chase and
    a scanner pattern; both already exist as dropdown patterns). Quoting it unchanged
    turns the exercise into "operate this dropdown," which teaches nothing. Convert it
    to predict-then-check against the built-in behavior, and keep only the parts that
    are genuinely still hands-on builds (F01 kept its 4-bit binary counter, since
    firmware doesn't implement that one).

Neither problem is visible from the source `.docx` alone — actually check the real
panel/firmware behavior against every activity before transcribing it, and settle the
rewrite with the user rather than picking a direction alone.

## 4. Fill unavoidable gaps honestly, don't erase them

Most of the source library is in an older, pre-template `.docx` format with no
Creative Challenge, no facilitator notes, and no stated difficulty or time estimate.
When a manual needs content that format doesn't have:

- Use `CalloutKind.NeedsReview` for a passage written for the app with no source
  document behind it. It renders visibly (blue, counted in a banner via
  `ManualDocument.NeedsReviewCount`) — distinct from `Placeholder` (nothing exists
  yet at all) and `Discrepancy` (contradicts hardware). The dangerous case is
  unsourced content that *looks* finished; the only defense is having it announce
  itself.
- Leave `Difficulty`/`TimeEstimate` (both on `ManualHeader`/`ProtoModCatalogEntry`)
  `null` unless the source manual actually states one — most don't. Never invent one
  "for consistency" with manuals that do have it stated.
- Never silently promote an estimate to a stated fact anywhere in the document —
  that includes the Library catalog entry that will eventually point at this manual,
  not just the manual body itself.

## 5. Check for links to other ProtoMods — both directions, every time

A progression link (`ProtoModNextStep` in `ProtoModLibraryCatalog.cs`) is added only
when an actual sentence in a manual establishes it — never inferred from circuit-code
number, series, or "this seems like a natural next step." Exactly one exists today
(F01 → F02) because exactly one sentence in F01's manual says so, quoted verbatim as
its evidence field. Before finishing a new manual:

- **Forward:** does *this* manual's own text suggest what to try next? If so, that's
  a real `ProtoModNextStep` from the new board, quoted with its section reference —
  add it even if the target board has no in-app manual yet (F01→F02 existed as
  reference metadata before F02 had one).
- **Backward:** now that this board has real in-app content, re-check *every
  already-written manual* for a sentence that was pointing at it before it existed.
  A manual written earlier may already say "move on to \<this board\>" in prose that
  was previously unusable because the target didn't exist in the catalog yet — that
  sentence is real evidence sitting unused, not something to re-derive.
- If no such sentence exists in either direction, leave `NextSteps` empty. A missing
  progression link is an honest "coming soon," not a bug — don't manufacture a
  plausible-sounding one to avoid an empty list.

This is the same discipline as every other field in the catalog: the link is only as
good as the sentence that proves it, and that sentence must be quoted, not summarized
into something that reads better.

## 6. Passive boards still get full manual treatment

A board with no software controls (every input is a switch/jumper — see
`ModuleCatalog`'s `"Passive"` control scheme) is not exempt from having a real
manual; F02 (Simple LED) is passive *and* has one. The two facts are unrelated:
"passive" means there's no live panel to dock above the manual and no
sign-in/progress lock ever applies to it (the lock only gates real
`ModulePanelViewModelBase` controls, which a passive board doesn't have) — it says
nothing about whether the manual itself needs less care. If anything, a passive
board's "Set up and try it" section carries more weight, since flipping a physical
switch and observing the result *is* the entire interactive experience for that
board.

## 7. Before calling it done

- [ ] Every description/summary/idea/next-step in both the manual and (if you're
      also adding one) the Library catalog entry has a `*Source` — a real document,
      section, or quoted sentence — not a plausible restatement.
- [ ] The "Set up and try it" section's `Id` is exactly `"setup"`.
- [ ] Every activity has been checked against real current firmware behavior, not
      just transcribed from the source `.docx`.
- [ ] Any manual/hardware disagreement is marked `Discrepancy` and resolved (or
      explicitly still open, flagged as such) — never silently rendered as settled.
- [ ] Any unsourced-but-necessary passage is marked `NeedsReview`, not blended in
      indistinguishably.
- [ ] Checked for a progression link in both directions (§5) — added if real
      evidence exists, left empty if it doesn't.
- [ ] `Difficulty`/`TimeEstimate` are `null` unless the source manual states them.
- [ ] Family (F/E/A) is derived from the circuit code, and this manual's voice
      actually matches what's confirmed for that family (§1) — an F-series manual
      that reads like E05 is a content bug even if every fact in it is accurate.
- [ ] Registered in `ManualLibrary.Factories`, keyed by the same `manualReference`
      string used in `Models/Registry/ProtoModRegistry.json` (see
      `adding-a-protomod.md` §2/§5).
- [ ] Run the app, open this manual for real, and scroll it end to end — the
      renderer is one shared set of `DataTemplate`s (`Views/ManualView.xaml`) across
      every manual, so a rendering bug here is usually a content-shape mistake (a
      block used in a way no other manual uses it), not a renderer bug.
