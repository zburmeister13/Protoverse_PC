# Adding a new ProtoMod — checklist

Reflects the registry-driven architecture on `feature/protomod-single-source-of-truth`
(not yet on `main`). If you're working against `main` before that merges, `ModuleCatalog`/
`ManualLibrary`/`ProtoModBoardCatalog` are still three separate hardcoded dictionaries —
skip the registry steps below and add one line to each of those three files instead.

Two things are **not actually live yet**, even on this branch — flagged inline below,
not glossed over: multi-slot occupancy folding, and revision-aware lookup. Both exist
and are tested in `Models/Registry/`, but nothing in `MainViewModel` calls them yet.

---

## 0. Coordinate with firmware (always, for a new ID)

- [ ] Confirm the new `ProtoModId` numeric value and circuit code with whoever owns
      the firmware session — it must match `protomod_catalog.c` exactly, same as every
      existing ID.
- **IF** this is conceptually a new hardware *revision* of an already-shipped board
  (not a new board type) → **THEN** note that `PresenceReport` cannot report a revision
  today (see `EVALUATION.md`, "the wire-protocol gap"). A second registry entry for a
  new revision is safe to add (tested, validated) but will have **no live effect** —
  `ModuleCatalog`/`ManualLibrary` only ever look up `ProtoModRegistryService.DefaultRevision`
  ("A"). Don't expect real hardware to pick it up until that wire-protocol extension
  ships.

## 1. `Models/ProtoModId.cs`

- **IF** this is a brand-new board type → **THEN** add a new enum value in the
  `0x0005..0xFFDF` range, matching firmware's value from step 0.
- **IF** this is a new revision of an existing ID → **THEN** skip this step; reuse the
  existing enum value.

## 2. `Models/Registry/ProtoModRegistry.json`

Add one entry (or edit `ProtoVerseApp.Tests` fixtures first if you want to prototype
the shape without touching real data):

| Field | Value |
|---|---|
| `id` | The enum value from step 1, as decimal. |
| `revision` | `"A"` unless this is a genuinely new revision. |
| `name` | Display name shown in the slot navigator and Library. |
| `circuitCode` | Exactly what's printed on the board / burned into EEPROM (e.g. `"F03"`). This is the field that drifted out of sync once before (AccelTemp/F02) — get it from the manual or a real EEPROM read, not a guess. |
| `manualReference` | **IF** a manual already exists (or you're writing one now) → the factory key you'll register in step 5. **ELSE** → `null`. |
| `controlScheme` | **IF** passive (switches/jumpers only, no commands) → the literal string `"Passive"`. **ELSE IF** this board behaves identically to an already-supported one → that scheme's exact existing string key. **ELSE** → a new string you'll register in step 4. |
| `slotSpan` | `1` unless this is a real multi-slot board — see the callout below before setting this > 1. |
| `compatibleWithRevision` | Leave unset unless a human has explicitly confirmed this revision reuses an earlier revision's control scheme. Never inferred from revision letters/numbers. |
| `pcbRev` / `pcbaRev` | Optional, informational only (nothing in the UI reads them today) — carry over from the EEPROM record if you have it. |

- [ ] Run `dotnet test ProtoVerseApp.Tests` — `RegistryValidationTests` fails loudly on
      a duplicate `(id, revision)`, a missing circuit code/revision, or a slot span
      that doesn't fit a 3-slot ProtoCore.

> **IF `slotSpan > 1` (multi-slot board):** the app assumes the module identifies
> itself from its **lowest-numbered occupied slot**, occupying that slot plus the
> next one(s) upward. Confirm this matches the real hardware interface before
> shipping it. **More importantly: `MultiSlotResolver` is not wired into
> `MainViewModel` yet.** Today, `MainViewModel.OnFrameReceived` still asks "what's in
> slot 2" independently of slot 1 — a real 2-slot board's secondary slot would very
> likely show as "Empty" rather than being folded into the first slot's occupancy.
> Integrating `MultiSlotResolver` into `MainViewModel`'s slot-building loop is a real,
> separate task to do *before* a multi-slot board can render correctly, not a
> side-effect of adding a registry row.

## 3. Is it passive?

- **IF** `controlScheme` is `"Passive"` → **THEN** you're done with code — skip
  straight to step 6. `PassiveModuleViewModel` renders it automatically (green dot,
  message pointing at the board's own switches).
- **ELSE** → continue to step 4.

## 4. New control scheme? (skip if reusing an existing one)

- **IF** `controlScheme` in step 2 matches an *existing* key already registered in
  `ModuleCatalog.Factories` → **THEN** skip this step entirely. No code needed — the
  registry row alone is enough.
- **ELSE** (genuinely new interactive behavior) → **THEN**:
  - [ ] Create `ViewModels/<Name>ViewModel.cs` extending `ModulePanelViewModelBase`:
        set `ModuleId`, `DisplayName`, override `OnFrameReceived`, call
        `SendCommand(...)` to talk back.
  - [ ] Create `Views/<Name>Panel.xaml` (+ code-behind) as a `UserControl`.
  - [ ] Register its implicit `DataTemplate` in `MainWindow.xaml`'s
        `<Window.Resources>` (matches the pattern already there for the existing
        panels).
  - [ ] Add one line to `ModuleCatalog.Factories`, keyed by the **exact same string**
        used as `controlScheme` in step 2's JSON.

## 5. Manual (optional, but do it before flipping any lock features live for this board)

- **IF** you're writing a manual now → **THEN**:
  - [ ] Create `Models/Manual/<Name>Manual.cs` with a `Build()` factory returning a
        `ManualDocument`, following `ManualBlocks.cs`'s shape (Overview, How it works,
        Set up and try it, What you should see, Go further, optional Appendix).
  - [ ] **The "Set up and try it" section's `Id` must be exactly `"setup"`.** This
        isn't cosmetic — the sign-in/manual-progress control lock
        (`SlotViewModel.IsControlLocked`) keys off that literal string. A different id
        means the board's controls can never unlock via the manual.
  - [ ] Add one line to `ManualLibrary.Factories`, keyed by the **exact same string**
        used as `manualReference` in step 2's JSON.
- **ELSE** → leave `manualReference: null`. The slot will show "No in-app manual for
  this slot yet," same as Accel+Temp today, and the control lock never gates this
  board (no manual to gate against).

## 6. Library tab entry (recommended even before a manual exists)

- [ ] Add an entry to `ProtoModLibraryCatalog.Entries` with `Code` matching step 2's
      `circuitCode` exactly and `ProtocolId` matching step 1's enum value.
- [ ] Follow the no-fabrication rule already in force there: only fill
      `Description`/`Ideas`/`SchematicSummary`/etc. from real, cited source material;
      leave a field `null` (renders as "coming soon") rather than writing something
      plausible-sounding.
- [ ] Run `dotnet test` — `CrossCatalogConsistencyTests` fails if this entry and the
      registry entry disagree on circuit code, in either direction.

## 7. Build, test, verify

- [ ] `dotnet build` (plain — no flags needed, `GenerateAssemblyInfo` is already off
      in the csproj).
- [ ] `dotnet test ProtoVerseApp.Tests` — full suite, not just the registry ones.
- [ ] Run the app. **IF** you want to demo the new board without real hardware →
      **THEN** note `MockSerialService` only fakes presence for the 3 original demo
      modules today; it doesn't automatically pick up new registry entries, so
      extending it is a separate, optional step, not required just to ship the board.
- [ ] **IF** real hardware is available → **THEN** connect for real, run "Identify
      slots," and confirm the new board's panel/manual/lock state all behave as
      expected before calling this done.

## 8. Firmware (always, for a new ID)

- [ ] Confirm firmware's own catalog was actually updated to match (step 0) — this
      app can never verify that from its own side; a mismatch here reads as "Unknown
      module" or the wrong panel on real hardware, not an error anywhere in this repo.
