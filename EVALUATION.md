# Evaluation: ProtoMod scale framework (ID / revision / multi-slot)

**Branch:** `eval/long-term-scaling` · **Date:** 2026-09-07

**Recommendation: proceed, with one change to the plan and one thing flagged
for a firmware-side decision, not an app-side one.** ID capacity is already
solved — nothing to do there. Keying the catalog on `(ID, Revision)` is a
contained, mechanical change that ripples less than expected. The
compatibility-override design holds up cleanly at scale because it's
per-identity, not global. Multi-slot inference works today for the cases the
identification handshake can actually see, but two of the three edge cases
the original task asked for turn out to be **the same case from the app's
point of view** — see below, this is a real finding, not a shortcut taken
here. The one thing that needs a human decision before this goes further:
revision is real, working hardware data that **never reaches the app today**
— see "The wire-protocol gap" below. That's a flag, not a build-blocker; all
of this evaluation's app-side work is fully demonstrated and tested without
it, using the one revision that exists (Rev A) as the assumed value at the
one integration point that would need a real revision byte.

---

## What was built

- `Models/Registry/ProtoModRegistryEntry.cs` — the identity record:
  `(Id, Revision) → { Name, ManualReference, ControlScheme, SlotSpan,
  CompatibleWithRevision }`. Identity only, no capability description, per
  the task's hard constraint.
- `Models/Registry/ProtoModRegistry.json` — the real registry: the four
  ProtoMods that actually exist today, all Revision "A", all slot span 1.
  No fictional entries in this file — see the stress test below for where
  synthetic data lives instead.
- `Models/Registry/ProtoModRegistryLoader.cs` / `ProtoModRegistryValidator.cs`
  — JSON → entries, then static checks (duplicate `(Id, Revision)`, missing
  revision, slot span vs. slot count, dangling compatibility override).
- `Models/Registry/RevisionAwareModuleCatalog.cs` — keys lookup on
  `(Id, Revision)` with three outcomes (`Found` / `UnrecognizedRevision` /
  `UnknownId`), resolving a compatibility-override chain for control scheme
  only (a revision keeps its own manual regardless of any override — see
  "the fillable-table-shaped bug this caught" below).
- `Models/Registry/MultiSlotResolver.cs` — infers occupancy from the user's
  explicit rule: a multi-slot module identifies from its lowest-numbered
  occupied slot and spans upward. Surfaces two conflict types (span doesn't
  fit at the anchor position; a secondary slot unexpectedly reports its own
  identification).
- `ProtoVerseApp.Tests` (new xUnit project) — 27 tests: registry validation
  as an actual CI check (loads the real JSON file, fails the build if it's
  invalid), catalog resolution including override chains and cycle safety,
  every multi-slot edge case, and a 1000-entry synthetic stress test
  (performance, and that the validator catches injected duplicate/span/
  dangling-override errors specifically, not just "something's wrong
  somewhere").

**Deliberately not built:** no live-UI integration displaying a detected
revision, because there is no real revision to detect yet (see below) and
fabricating one in the running app would misrepresent what the hardware
actually reports — this project's standing rule against unsourced content
applies exactly as much to a revision number as to manual text. The registry
subsystem is fully exercised by the test suite instead, which is what a
"working prototype" needs to be for an identity/data-model evaluation like
this one.

---

## Evaluation questions

### 1. What's the real ceiling on distinct ProtoMods, and would the wire protocol need to change?

**Already solved, and already shipped.** `ProtoModId` was widened from 1 byte
to a 2-byte little-endian value on 2026-08-30 (see the doc comment on
`Models/ProtoModId.cs`) specifically to support a 1,000+-entry catalog. That
gives **65,536 possible values**, with `0x0005–0xFFDF` (**65,504 values**)
reserved for the catalog after reserved IDs (`Unknown`, `Core`, `Broadcast`)
were moved to the top of the range. At 1,000 ProtoMods that's 1.5% of the
available space used — there is no realistic scale (short of a redesign of
the product itself) where this needs revisiting.

The `Length` field (frame payload size) is a separate axis and is not the
bottleneck either: it's 1 byte (max 255), already capped in code at 250
(`ProtocolFrame.MaxPayloadLength`), and a `PresenceReport` payload is
`SlotCount * bytesPerSlot` — 6 bytes today (`3 slots × 2-byte ID`), and would
still be well under the cap even if a revision byte were added per slot
(`3 × 3 = 9 bytes`). **No wire-protocol change is needed for ID capacity.**
The wire-protocol change this evaluation *does* flag is a different one — see
the next section.

### 2. Is keying `ModuleCatalog` on `(ID, Revision)` contained, or does it ripple?

**Contained, and less disruptive than expected — with one real gap.** The
mechanical part is clean: `RevisionAwareModuleCatalog` is a straight
drop-in replacement for `ModuleCatalog`'s dictionary, same O(1) lookup
shape, same "fall back to the unsupported placeholder" behavior on a miss.
Nothing about `MainViewModel`'s hot-swap rebuild loop, `SlotViewModel`, or
any panel `ViewModel` needed to change to prove this out — the registry
lives entirely underneath the existing catalog contract.

**The gap is upstream, not downstream: nothing currently produces a
`Revision` to key on.** This was confirmed directly, not assumed —
`PresenceReport`'s payload format
(`MainViewModel.OnFrameReceived`) is a fixed `SlotCount * 2` bytes, exactly
one `ProtoModId` per slot, and `ProtocolFrame`'s wire layout has no revision
field at all. `Models/ProtoModBoardCatalog.cs` documents PCB/PCBA revision
fields that genuinely exist in every ProtoMod's EEPROM — this is the "already
stores HW revision" fact the task starts from, confirmed real — but that file
is a **static, hardcoded mirror of firmware's C header**, written for a
person reading the Help tab, never sent over the wire or read from a live
board. So the honest answer to "does keying on revision ripple further than
expected" is: **the catalog change itself doesn't, but making it mean
anything for real hardware requires PresenceReport to actually carry a
revision byte per slot, which it does not today.** That is a wire-protocol
change (extending `PresenceReport`'s payload from ID-only to ID+revision per
slot) — flagged per the task's instructions, not implemented, since it's a
cross-session decision with firmware.

### 3. Does the compatibility-override design hold up as revisions/overrides accumulate?

**Yes — because it's per-identity, not global.** Each override is one field
on one entry (`CompatibleWithRevision`), pointing at one other revision of
the *same* `Id`. A registry with 1,000 identities and a mix of overridden and
independent revisions costs nothing extra to resolve: `Resolve()` is a single
dictionary lookup plus, only when an override is actually present, a short
walk (bounded by however many revisions one `Id` accumulates — realistically
single digits, never proportional to registry size). The stress test
confirms a 3-hop override chain resolves correctly and a cycle (which
validation should already reject, but the catalog defends against directly)
terminates instead of hanging.

The one place this *could* get unwieldy is a human-process risk, not a data-
structure one: nothing stops someone from authoring a long override chain
(Rev D → Rev C → Rev B → Rev A) that becomes hard to audit by eye. The
registry format doesn't need to change to handle that — a future validation
rule capping chain depth, or a lint that suggests collapsing a chain to
`CompatibleWithRevision` pointing directly at the ultimate source revision,
would be a cheap follow-up if this becomes a real pattern. Not needed at
today's scale (one real hardware revision total).

### 4. Are the multi-slot edge cases actually detectable, or does firmware need to change?

**Two are detectable today with the current handshake. The third isn't a
separate case — this is the actual finding here.** With the user's explicit
rule for this evaluation (a multi-slot module identifies from its lowest
occupied slot, spanning upward), the original task's three edge cases
collapse to two detectable conditions:

1. **Declared span doesn't fit at the anchor position** (e.g. a 2-slot module
   identifying from the last physical slot) — fully detectable from the
   identification handshake alone: the resolver knows the anchor slot and the
   registry's declared span, and 0-based arithmetic against the slot count
   is enough. `MultiSlotResolverTests` covers this for 2-slot and 3-slot
   spans.
2. **A secondary slot reports its own identification instead of staying
   silent** — also fully detectable: any non-empty detection at a slot the
   resolver has already claimed as a secondary slot is a conflict.

The original task listed a *third* case — "two independently-identified
single-slot modules occupy slots the registry says should belong to one
multi-slot module" — as physically-unlikely-but-software-detectable. **It
is not separately detectable, because it is not separately distinguishable.**
From the wire's point of view, a hardware fault, a partially-seated board,
and two genuine independent boards colliding with a registered multi-slot
claim all produce the exact same observable fact: *a slot the resolver
expected to be silent reported an identity of its own.* `MultiSlotResolver`
(and `MultiSlotResolverTests.SecondarySlot_ReportingItsOwnIdentification_IsAConflict`)
treats all three as one `SlotConflict` for exactly this reason. Distinguishing
between them would require information the identification handshake doesn't
carry today (e.g., a per-slot "why am I reporting this" reason code) — that
would be a firmware/protocol change, and this evaluation's recommendation is
not to build one speculatively: the single merged conflict type is the
correct app-side behavior regardless of root cause, since the action is the
same either way (flag it, don't silently guess).

**Confirmed, not assumed, before building any of this:** ProtoCore's slot
count is 3 (`MainViewModel.SlotCount`), and the anchor rule came from an
explicit hardware-interface answer rather than an assumption this evaluation
made on its own.

### 5. Any part of the current architecture that didn't stretch cleanly to 100s–1000+ entries?

**No — the one thing that looked like it might (a `Dictionary`-backed
catalog scanned per lookup) is a non-issue.** `ScaleStressTests` builds a
1,000-entry catalog and performs 1,000+ lookups (including intentional
misses) in low tens of milliseconds — three orders of magnitude under the
generous 1-second threshold the test asserts. `ModuleCatalog`'s existing
`Dictionary<ProtoModId, ...>` shape already scales the same way;
`RevisionAwareModuleCatalog` just widens the key. The only place that would
need attention at real 1,000-entry scale is **authoring and reviewing the
registry itself** — a human/tooling problem (schema validation, maybe a
generator from firmware's own catalog source, both out of scope here), not
an architectural one this codebase needs to change to accommodate.

---

## The fillable-table-shaped bug this caught

Worth recording because it's a small, concrete example of exactly the kind
of mistake a "revision defaults to different module" framework is supposed
to prevent, and the test suite is what caught it, not code review: the first
version of `RevisionAwareModuleCatalog` resolved `ManualReference` through
the *same* compatibility-override chain as `ControlScheme`. That's wrong —
an override declares "this revision's controls behave like that one's," not
"this revision's physical assembly is identical," and the entry's own doc
comment already said so before the resolver code contradicted it.
`RevisionAwareCatalogTests.CompatibilityOverride_ResolvesToTargetRevisionsControlScheme`
failed immediately (expected the overriding revision's own manual, got the
target's), which is exactly what a real Rev B sharing Rev A's command set but
needing its own updated assembly instructions would need to work correctly.
Fixed before this branch's first commit that included it.

---

## The wire-protocol gap (flagged, not implemented, per the task)

Summarizing the finding from question 2, since it's the one thing a reader
of this report needs to act on: **`PresenceReport` needs a revision byte (or
more) per slot before any of this can key on real hardware revision.**
Concretely, that means growing the per-slot payload from `2 bytes` (ID) to
`2 + N bytes` (ID + revision), which is a firmware-and-app cross-session
change, not something to decide unilaterally here. The `RevisionAwareModuleCatalog`
and `MultiSlotResolver` built on this branch are already shaped to accept
whatever that byte turns out to be (a `string` today — swap-compatible with
whatever encoding firmware settles on, e.g. a single ASCII letter or a small
integer mapped to a letter at the app boundary) — this is a protocol
extension to schedule, not a design this evaluation needs to redo once it
happens.

---

## Recommendation

**Proceed.** ID capacity needed no work. The registry/catalog/resolver
pattern built here is a contained, well-tested, drop-in layer under the
existing `ModuleCatalog` contract, holds up cleanly at 1,000-entry synthetic
scale, and its one real limitation (revision data not reaching the app) is a
scheduling question for a firmware conversation, not a flaw in this
approach. Next step if this proceeds past evaluation: raise the
`PresenceReport` revision-byte extension with the firmware session, since
every app-side piece here is otherwise ready to consume it.
