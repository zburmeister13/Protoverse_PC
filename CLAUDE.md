# ProtoVerse PC App — project context

This file is read automatically by Claude Code at the start of every session in
this project. It covers the *why* behind decisions and gotchas already hit —
not a restatement of the code, which you can read directly.

## What this project is

ProtoVerse is a modular electronics education platform: a ProtoCore board plus
interchangeable ProtoMods (small boards teaching one electronics concept each).
This repo is the Windows PC companion app — a single WPF window that talks to
ProtoCore over USB serial and lets the user control whichever ProtoMods are
plugged in.

Full ProtoVerse business/product context (target audience, manual philosophy,
the six initial ProtoMods, etc.) lives outside this repo and generally isn't
needed to work on this app — this file has what's actually relevant here.

## Git

**Do not run any git command (commit, push, or otherwise) unless the user
explicitly says so in that session — no standing exception for "this is
just docs" or "I already committed similar changes earlier tonight."**
Earlier sessions fell into a pattern of committing and pushing after nearly
every change on the assumption that was the established rhythm; the user
corrected this directly (2026-08-31) and it applies going forward, not just
to that session. Keep `CHANGELOG.md`/`CLAUDE.md` updated per the section
below regardless — that's still expected every time — just leave the
actual `git add`/`commit`/`push` for the user to ask for or do themselves.

## Keeping this file and CHANGELOG.md current

- Every code change gets an entry in `CHANGELOG.md` (repo root) — the prompt that
  drove it, its purpose, and a summary of what changed — in chronological order.
  Do this for every change from here on, not just when asked.
- Each `CHANGELOG.md` entry carries its own date **and time**, not just a date
  (entries 1–8 predate this and are marked "time not recorded" rather than
  backfilled with a guess) — a shared per-day header isn't enough to tell
  which of several same-day changes came first.
- Update this file too, but only where a change actually affects something it
  documents: a platform decision, the wire protocol, "Current state," or a new
  gotcha. Routine changes belong in `CHANGELOG.md` only — don't duplicate them
  here.

## Platform decisions (already made, don't relitigate without asking)

- **Modularity for a 1,000+-ProtoMod catalog is a standing priority, not an
  aspiration.** The user has explicitly directed (2026-08-30) that both this
  app and the ProtoCore firmware (separate codebase/session) must be
  structured assuming the eventual catalog is 1,000+ distinct ProtoMod types,
  of which any given ProtoCore unit has only a handful of slots. This was
  already the direction `ModuleCatalog`/`PresenceReport`-driven dynamic
  panels were built in (see "Current state" below) — treat it as confirmed
  going forward, not a redesign to reconsider. When evaluating any new
  protocol field, data structure, or UI pattern, ask "does this still work
  reasonably at 1,000+ types," not just "does this work for the 3 we have
  today." **The one concrete conflict this surfaced is resolved (as of
  2026-08-30):** `ProtoModId` (`Models/ProtoModId.cs`) was a single byte on
  the wire (~253 usable values after reserved IDs) - both this app and
  firmware now use a 2-byte little-endian `ProtoModId`, agreed and
  implemented on both sides (see the Wire protocol section below for the
  exact layout). Everything else was already built with this scale in mind —
  dynamic slot population, `ModuleCatalog`'s one-line-per-type registration,
  `UnknownModuleViewModel` degrading gracefully for a recognized-by-firmware-
  but-unsupported-by-this-build type — and didn't need to change.
- **Windows-only.** WPF, .NET 8 (`net8.0-windows`), C#. No cross-platform
  requirement — this was an explicit choice, not an oversight.
- **CommunityToolkit.Mvvm** for MVVM boilerplate (`[ObservableProperty]`,
  `[RelayCommand]`).
- **Single window, single serial connection, no per-module "sessions."** An
  earlier design explored a browser-tab-per-module model with independent
  sessions; that was explicitly rejected. The current model is one window with
  stacked panels — see `MainViewModel.Panels` (an `ObservableCollection` bound
  through a plain `ItemsControl`). If asked to redesign this, don't reintroduce
  a session/tab abstraction unless directly asked to.

## Wire protocol (v1)

```
[STX 0x02] [ProtoModId_lo] [ProtoModId_hi] [MsgType] [Length] [Payload...] [Checksum] [ETX 0x03]
```

- `ProtoModId` is **2 bytes, little-endian** (widened from 1 byte on
  2026-08-30 — see the "Modularity for a 1,000+-ProtoMod catalog" platform
  decision above for why; frame header grew from 3 bytes to 4 accordingly,
  and checksum now XORs both ID bytes). Fixed, locked vocabulary shared with
  the ProtoCore firmware (a separate codebase) — `0x0001` BlinkyLed, `0x0002`
  AccelTemp, `0x0003` ElectronicLoad, `0x0004` BasicLed, `0xFFE0` Unknown
  (ProtoCore couldn't identify what's in the slot — valid EEPROM read, no
  catalog match — distinct from `None`/empty), `0xFFF0` Core, `0xFFFF`
  Broadcast (reserved IDs deliberately live at the top of the range so they
  read as system addresses, not catalog entries). Adding a ProtoMod means
  adding an ID here *and* in firmware, together. **`PresenceReport`'s
  payload is a FIXED `SlotCount*2`-byte array (as of 2026-08-30), not a
  variable-length list of only the occupied slots** — exactly one
  `ProtoModId` per physical slot, always in slot order, 2 bytes
  little-endian each; an empty slot reports `ProtoModId.None` (`0x0000`)
  rather than being omitted. This replaced an earlier skip-empty-slots format
  after the user hit a real bug
  it caused: with only one slot occupied, "module in slot 0" and "module in
  slot 1, slots 0/2 empty" produced an identical single-entry payload, so a
  module in any slot but the first always rendered in this app's *first*
  panel. `MainViewModel.OnFrameReceived` reads exactly `SlotCount` slots by
  index and assigns each straight to `Panels[slot]` — don't reintroduce
  `Distinct()`/filtering over the payload, `None` legitimately repeats
  across multiple empty slots now and filtering it out is exactly the bug
  that got fixed. A payload that isn't exactly `SlotCount*2` bytes is
  rejected (status message, slots left unchanged) rather than partially
  interpreted. `MockSerialService`'s presence reply already emits exactly 3
  entries in slot order (all currently non-`None` by default), so it didn't
  need to change, but its `InstalledMods` array is a convenient way to
  exercise a partial-occupancy/`None` scenario if that path needs retesting.
- No byte-stuffing/escaping yet, checksum is a simple XOR. Flagged as a
  possible future improvement, not a bug.
- **Firmware side implements this exact frame format now** (`protocol.h`/
  `protocol.c` in the separate `Protocore` codebase, agreed cross-session with
  the firmware Claude session to mirror `ProtocolFrame.cs` byte-for-byte) over
  **USB CDC** — `CDC_Receive_FS()` feeds bytes into a `Protocol_RxByte()` state
  machine with resync-on-error (bad checksum/ETX/oversized Length drops back
  to scanning for STX, same as `ProtocolFrameReader` does here), and
  `Protocol_SendFrame()` encodes and sends via `CDC_Transmit_FS()`. `main()`
  now actually calls `start_program()` (previously dead code) and
  `poll_protocol()` runs once per main-loop iteration, replying to
  `PresenceRequest` with a real `PresenceReport` built from `active_slots[]`.
  **As of 2026-08-30 this is compile+link verified only (`Protocore.elf`
  builds clean via CMake/Ninja) — nothing has been flashed to real hardware
  yet**, so slot detection over I2C/EEPROM is still unverified in practice.
  Don't treat presence detection as working end-to-end until that's
  reconfirmed after a real flash+test.
- **BlinkyLed Command is implemented firmware-side too now** (as of
  2026-08-30, still compile/link-verified only, same hardware caveat as
  above), and its `Response` format has been settled as a **fixed 7-byte
  full-state snapshot returned by every one of the 5 sub-commands**, not a
  per-command payload — this replaces the entry-14-era "SetState echoes a
  bool, SetBlinkRate is empty" design entirely:
  ```
  payload[0] = enabled     (0/1)
  payload[1] = mode        (0=Animated, 1=Manual)
  payload[2] = pattern     (0=Bounce, 1=Chase, 2=All, 3=Random — meaningful iff mode==Animated)
  payload[3] = reverse     (0/1)
  payload[4..5] = period_ms (uint16, little-endian)
  payload[6] = manual_mask (bits 0-3 = LED0-LED3 — meaningful iff mode==Manual)
  ```
  Sub-commands (`payload[0]` of the Command): `0x01` SetState (`payload[1]`
  = 0/1), `0x02` SetBlinkRate (`payload[1..2]` = uint16 LE
  **milliseconds-per-step on both sides** — firmware's internal
  representation was steps-per-second before this and was changed to match
  the wire value, zero conversion needed either direction), `0x03` SetPattern
  (`payload[1]` = pattern id, switches the slot to Animated mode), `0x04`
  SetDirection (`payload[1]` = 0 forward/1 reverse), `0x05` SetManualLeds
  (`payload[1]` = 4-bit mask, switches the slot to Manual mode — this is the
  *only* way into Manual mode, there's no separate mode-switch command).
  `ViewModels/BlinkyLedViewModel.cs` matches this exactly: every property is
  populated only from the device's echoed snapshot (no local optimism), and
  `Models/BlinkyLedState.cs` holds the `BlinkyLedMode`/`BlinkyLedPattern`
  enums. `ProtoModId` addresses a board *type*, not a specific slot, so
  firmware treats a BlinkyLed Command as broadcast-by-type (applies to every
  slot currently holding that type) — found and fixed a real pre-existing bug
  in the process (blinky pattern state was accidentally global instead of
  per-slot). Firmware also now sends unsolicited `PresenceReport` on any
  hot-swap detected in its main loop (previously request-only), and
  `MSG_ERROR` for not-present/bad-payload-length/unknown-MsgType/bad-value
  (`PROTOCOL_ERR_BAD_VALUE` = `0x05`, e.g. an unrecognized pattern id), plus
  `PROTOCOL_ERR_NOT_IMPLEMENTED` (`0x04`) for any Command addressed to
  AccelTemp or ElectronicLoad, since neither has firmware-side handling yet —
  covers `ElectronicLoadViewModel`'s SetCurrentLimitMa case, which previously
  would have silently no-opped.
- **Resolved (documentation-confirmed), hardware-read still pending, as of
  2026-08-30:** firmware originally mapped its `BOARD_BASIC_LED` enum value
  to this app's `AccelTemp` (`0x02`) by lookup-table position alone, and a
  follow-up `Core/{Inc,Src}/protomod_catalog.{h,c}` catalog (mirrored here in
  `Models/ProtoModBoardCatalog.cs`) still listed `AccelTemp` = circuit code
  `"F02"` — both were the same unverified slot-position guess, restated
  rather than confirmed, and the user directly contradicted it during a
  real-hardware session ("board two is not IMU and Temp"). Settled against
  the project's own module manuals (`Documents/.../PROTOVERSE/Manuals/`),
  not another guess — quotes independently verified by this session against
  the actual `.docx` files, not just trusted from a relay:
  `E03_Sensors1.docx` ("This ProtoMod introduces two types of sensors: STM
  LIS3DH accelerometer ... Analog Devices TMP36 temperature sensor") is
  `AccelTemp`, precisely; `F02_Simple_LED.docx` ("two LED paths ... plus two
  switches per path") is a static resistor/voltage demo board with zero
  sensors, not `AccelTemp`. `AccelTemp`'s circuit code is now `"E03"` on
  both sides. **Caveat, same honesty standard as everywhere else in this
  project: doc-confirmed, not yet hardware-confirmed** — no ProtoCore has
  done a live EEPROM read of a physical Sensors-1 board to verify `"E03"`
  is really what's burned into it. `identify_slots()`/`BoardID_ReadParsed()`
  are both non-destructive and already implemented firmware-side, so that
  read is trivial once real Sensors-1 hardware is on the bench, and would be
  the final word if this is ever in doubt again. The
  `ProtoMod_Programmer` Arduino sketch (a third codebase, programs a
  ProtoMod's EEPROM over I2C) has no read-only mode, so it can't be used for
  that check without risking overwriting the board.
- **Fully closed 2026-08-30 with real hardware evidence**, not just the
  documentation-confirmation above: a raw-serial capture showed the physical
  board behind this whole saga was never `AccelTemp` at all — it's real
  hardware with a valid EEPROM that simply wasn't in firmware's catalog yet,
  so it was reporting as `ProtoModId.None` (indistinguishable from an empty
  slot) the entire time. Firmware added `ProtoModId.BasicLed` (`0x0004`,
  circuit code `"F02"` — confirming the manual reading above was right all
  along, it just needed its own catalog entry) and `ProtoModId.Unknown`
  (`0xFFE0`, for a slot with a valid-but-uncataloged EEPROM read, so it's no
  longer indistinguishable from empty). `UnknownModuleViewModel` now shows a
  different message for `Unknown` ("Something's plugged in here, but
  ProtoCore doesn't recognize its EEPROM identity") than for a real,
  firmware-known `ProtoModId` this app just has no panel for — see
  CHANGELOG entry 29.
- **Two pre-existing firmware bugs found while wiring this in** (unrelated to
  the protocol itself, noted here since they affect whether ProtoCore even
  boots): `program.c` used `hi2c1` without including `init.h`, and `init.c`
  duplicates globals (`hi2c1`, `hadc1`, `SystemClock_Config`, `Error_Handler`)
  that also live in `main.c`, which fails to link if both are built together.
  The firmware session excluded `init.c` from the CMake build rather than
  fixing/deleting it (their user's call to make, not this session's). This
  likely means the CubeIDE/Eclipse build — which doesn't exclude `init.c` —
  has never linked successfully either, though that's unconfirmed.
- **Fixed 2026-08-30: an unbounded `CDC_Transmit_FS()` retry in
  `protocol.c`'s `Protocol_SendFrame()` could freeze ProtoCore's entire
  main superloop, silently and permanently.** `USBD_BUSY` only clears once
  the host actually drains the previous IN transfer, which firmware
  doesn't control — if this app's read side was ever briefly slow (a
  realistic case: the real report that found this was the user clicking a
  panel's Apply button several times in quick succession), the retry loop
  could spin forever, so `poll_protocol()` never ran again and *every*
  subsequent Command of any type — any module, any payload — was dropped
  silently forever, with no `Error` response either, until a physical
  reset. Diagnosed entirely from this app's Traffic Log (see CHANGELOG
  entry 38): the tell was two clean `SetCurrentLimitMa` exchanges followed
  by total silence on later commands regardless of the value sent, ruling
  out a payload-specific bug. Firmware fixed it by bounding the retry to a
  100ms timeout (`PROTOCOL_TX_TIMEOUT_MS`) that drops the one frame instead
  of hanging the system; flashed and download-verified on the bench board,
  firmware commit `9fce051`. **Confirmed fixed against real hardware**
  (2026-08-30) — the user repeated the original trigger (clicking a
  panel's Apply button rapidly, several times in a row) and the board
  stayed responsive throughout. If a module stops responding again after
  this, this exact class of bug is now fixed, so suspect something new
  rather than assuming it's this same issue recurring — but do check the
  Traffic Log
  first either way: silence (nothing at all, even though the connection
  otherwise looks healthy) and an explicit `Error` response are different
  failure modes on ProtoCore's side, and only the Traffic Log can tell
  them apart — this app has no other way to distinguish "firmware is alive
  and rejecting the frame" from "firmware's main loop itself stopped."
- Full spec and rationale: see `README.md` in the repo root.

## Current state

- Builds and runs. Blinky LED panel is fully wired end-to-end (real commands,
  real response parsing) and is the reference example for how a panel should
  work — including its device-echo-only pattern (no locally-optimistic state)
  and the property-changed-partial-method-sends-a-command pattern, both worth
  reusing for any future panel that needs more than one or two controls. Its 4
  LED indicators (`Views/BlinkyLedPanel.xaml`) are driven by a **local
  `DispatcherTimer`** in `BlinkyLedViewModel` that reconstructs the
  Bounce/Chase/All/Random animation from the last known Pattern/Reverse/rate —
  this is a client-side re-creation for display purposes, not real telemetry,
  since the protocol only reports a full-state snapshot per command rather
  than a frame per animation step. Confirmed cross-session against firmware's
  actual `protomod.c` sequences (as of 2026-08-30): Bounce
  `{0,1,2,3,2,1}`/period 6 and Chase `{0,1,2,3}`/period 4 (plain wrap) are an
  **exact match**, not an approximation — the only expected divergence from
  real hardware is timing jitter from blocking I2C calls elsewhere in
  firmware's main loop occasionally delaying a step by a few ms, not the
  sequence itself. Any panel that adds a similar "continuously animate
  between snapshots" control should follow the same pattern: guard the
  timer's own property writes (see `_animatingLocally`) so they don't loop
  back into sending a command, and stop the timer from an overridden
  `Detach()` (now `virtual` on `ModulePanelViewModelBase` for exactly this) so
  a discarded panel's timer doesn't keep ticking forever.
- **Hot-swap safety.** `App.xaml.cs` has a global `DispatcherUnhandledException`/
  `AppDomain.UnhandledException` handler (logs to `crash_log.txt`, copies to
  clipboard, shows a dialog, marks the WPF one handled so the process itself
  survives) — that's a last resort for genuinely unexpected bugs, not the
  intended response to routine hot-swapping. The actual hot-swap path is
  fault-isolated one level up: `MainViewModel.OnFrameReceived` builds the new
  slot lineup in a local list and only swaps it into the live `Panels`
  collection once fully built (a failure never disturbs already-working
  panels), and each slot's `ModuleCatalog.TryCreate` call is individually
  wrapped in try/catch (one misbehaving module type degrades just that slot to
  `UnknownModuleViewModel`, doesn't lose the whole report or trigger the
  global dialog). `DetachModulePanels` does the same per-panel for `Detach()`.
  Confirmed 2026-08-30 via genuine fault injection (temporarily made a
  factory throw, verified live) rather than just reasoning about it — see
  CHANGELOG entry 28. Any future change to `OnFrameReceived`'s rebuild loop
  should preserve this: build-then-swap, and catch around panel construction.
  Same principle applies to `Connect()`: a real-port `SerialPort.Open()`
  failure (bad/busy port, flaky USB CDC driver, board resetting mid-open —
  hit for real against actual hardware 2026-08-30, see CHANGELOG entry 31)
  is caught in `SerialService.Connect()` and `MainViewModel.Connect()` and
  turned into a `StatusMessage`, not left to reach the global handler either.
  Any code path that can hit a real OS/driver-level failure (port I/O,
  panel construction, anything touching hardware) should be assumed capable
  of throwing and caught at the point closest to the failure, converted into
  a status message or graceful UI state — the global dialog is for bugs,
  not expected failure modes.
- **Branded dark theme, near-black as of 2026-08-30.** `App.xaml` defines
  the ProtoVerse brand palette as named `Color`/`SolidColorBrush`
  resources, plus implicit (`TargetType`-only) `Style`s for `Button`,
  `TextBox`, `CheckBox`, `ComboBox`, `Expander`, and `DataGrid` so every
  control picks the theme up automatically. The background/surface ramp
  was originally a deep-navy/purple sampled from the logo lockup, and was
  swapped to a near-black neutral-gray ramp (`BgColor` `#0A0A0C` up through
  `BorderColor` `#35353C`, `TextSecondaryColor` a neutral `#9A9AA4`) per
  direct user feedback ("sick of the purple") — the teal/green/blue/orange
  accent colors and the off-white `TextPrimaryColor` are unchanged, since
  those are what's actually logo-derived and read cleanly against either
  background. If asked to touch the palette again, treat the accent
  colors as the settled brand identity and the background ramp as the
  part that's actually been revised once already at the user's request.
  New views should reference these `StaticResource` brushes rather than
  hardcoding colors; three spots can't bind to a `StaticResource` and must
  be kept in sync by hand if the palette in `App.xaml` ever changes:
  the two status-dot converters (`BoolToBrushConverter`,
  `SlotStateToBrushConverter` — the latter only uses the unchanged accent
  colors, so it didn't need updating this time) and `Charts/ChartTheme.cs`
  (OxyPlot's `PlotModel` is a plain C# object, not a WPF
  `DependencyObject`).
- Accel+Temp's panel exists and renders, but its command/response payload
  layout in `OnFrameReceived` is an **explicit placeholder** — marked with
  a `TODO` comment — because that ProtoMod's actual firmware command set
  isn't defined yet. Don't treat that byte layout as settled. **Its UI is
  real, though, not a placeholder** — it has live OxyPlot charts
  (`Charts/ChartTheme.cs` builds the dark-themed `PlotModel`s) built
  deliberately decoupled from that parsing: `OnFrameReceived` turns bytes
  into properties, a separate `AppendToChart(s)` method just plots whatever
  those properties currently hold — a temperature trend line, an X/Y
  "bubble-level" tilt plot (fixed ±1.5g axes so the origin is always the
  visual center, a single `ScatterSeries` point moved each update rather
  than a trend history), and a Z fill gauge (`LinearBarSeries` with
  `BaseValue` set to -1g, so the bar fills up above -1g and down below it,
  per the user's spec) — not three trend lines. This was built and verified
  entirely against Simulator mode's fake telemetry, on purpose, so the UI
  didn't have to wait on firmware defining the real payload — when that
  lands, only the parsing changes, not the charts. Follow the same split
  for any future panel that streams data.
- **Electronic Load's wire format is settled (as of 2026-08-30) and its UI
  deliberately has no chart at all** — this is a real hardware constraint,
  not an unfinished placeholder. The firmware session confirmed this board
  is genuinely open-loop on the current revision (bit-banged PWM into an
  op-amp forcing current through a 10-ohm sense resistor, no ADC feedback
  path), so there is no measured voltage/current to ever report. Command:
  `SetCurrentLimitMa` = `payload[0]=0x01`, `payload[1..2]` = uint16 LE mA,
  0-300 (`MAX_CURRENT_MA`; out of range → `PROTOCOL_ERR_BAD_VALUE`, wrong
  length → `PROTOCOL_ERR_BAD_PAYLOAD_LEN`, same pattern as BlinkyLed).
  Response is a 3-byte `[current_ma_lo, current_ma_hi, duty_percent]` — an
  echo of the commanded current (not a measurement) plus the PWM duty cycle
  firmware is actually driving. `ElectronicLoadViewModel` was originally
  built with a placeholder 4-byte measured-voltage/measured-current payload
  and a live dual-axis trend chart (mirroring the AccelTemp split above);
  once the real Response shape landed, the firmware session explicitly
  flagged that showing a "measured" chart here would be fabricating data no
  real board of this revision can produce, and declined to redesign the UI
  unilaterally. Given the choice (chart relabeled "commanded," replace with
  commanded+duty readouts, or drop the chart entirely), **the user chose to
  drop the chart entirely** — the panel now shows only the commanded
  current and duty % as plain readouts (`CommandedCurrentMa`,
  `DutyPercent`), with an explicit "values above are commanded, not
  measured" note in the UI itself. `MockSerialService.BuildLoadTelemetry`
  matches the 3-byte format and no longer fires on the periodic telemetry
  timer (nothing changes there except in response to a command). The
  current-to-duty calibration (I·R=V, V/VDD=duty, R=10Ω, VDD=3.3V nominal)
  is explicitly first-pass per the firmware session — real hardware
  verification is still pending there, so don't treat the exact constants
  as final, only the wire format (command bytes in, 3-byte Response shape
  out). **The `SetCurrentLimitMa` handler itself was compile+link verified
  only until the same day** — a user report ("hit 100mA and apply, nothing
  changes in the commanded portions") against real hardware turned out to
  be exactly this: the app was working correctly (confirmed both in
  Simulator mode and via a real Traffic Log capture showing
  `PROTOCOL_ERR_NOT_IMPLEMENTED` coming back from the board), the bench
  board just hadn't been reflashed yet with the build containing the real
  handler. The firmware session has since flashed and verified that build
  via STM32CubeProgrammer, and **this is now confirmed against real
  hardware** — a real Traffic Log capture the same day shows correct
  `SetCurrentLimitMa` exchanges for 10mA, 100mA, and 250mA, each with a
  `Response` matching the calibration math. If a future "nothing happens"
  report comes in against real hardware for any module, check the Traffic
  Log for an `Error` response before assuming an app bug — no panel
  currently surfaces `MSG_ERROR` to the UI, only the Traffic Log does, so a
  firmware-side rejection can look identical to the app silently doing
  nothing (a *third*, distinct possibility — total silence with no `Error`
  at all — turned out to mean ProtoCore's whole main loop had frozen; see
  the `Protocol_SendFrame` bug noted further up in this section).
  **PWM frequency changed 1kHz→5kHz firmware-side same day** (commit
  `64d6351`, to reduce ripple on the op-amp's filtered input the user
  observed on the bench) — traded duty-step resolution 50→10 steps to hold
  the ISR rate fixed at an already-safe rate. Wire format is unchanged
  (`duty_percent` is still a single 0-100 byte in the Response), but real
  hardware now only ever reports it in multiples of 10 rather than 2 — not
  a bug if a duty readout looks coarser than the calibration formula
  implies, that's now just the real achievable resolution.
  **Superseded by a full 1-300mA real-hardware sweep, 2026-08-31 (CHANGELOG
  entry 42):** duty actually increments by 1 roughly every 3mA in current
  firmware (fine-grained, matching the `10Ω/3.3V` calibration formula), not
  in multiples of 10 - the "multiples of 10" claim above was accurate for
  the 5kHz-PWM firmware build at the time it was written, but a later
  firmware calibration fix (for an unrelated 10mA/20mA duty-collision bug)
  evidently changed the achievable resolution back to fine-grained. The
  firmware session independently confirmed the sweep's numbers match their
  current calibration constants exactly. Trust the fine-grained reading as
  current; the "multiples of 10" note above is historical, not current
  behavior.
  **Also worth remembering, since it cost real back-and-forth with the
  firmware session to sort out:** that same sweep initially appeared to
  show the Response's echoed `current_ma` wrapping to 0 at 256mA (an
  apparent 8-bit truncation). That was **not a firmware bug** - firmware's
  `send_state()` correctly encodes both bytes. The wrap was a bug in this
  session's own one-off diagnostic PowerShell script: `-shl` in PowerShell
  preserves the type of its left operand, so shifting a `[byte]` left by 8
  truncates within an 8-bit container instead of promoting to a wider type
  first, silently zeroing the high-byte contribution for any value >= 256.
  Fixed in the script (cast to `[int]` before shifting). No firmware or app
  change resulted from this - flagging only as a reminder that a
  diagnostic tool's own bug can look exactly like the hardware bug it was
  built to find, and is worth ruling out before reporting a finding
  cross-session as if it were confirmed.
- Panels are populated dynamically from `PresenceReport`, not hardcoded. There
  will eventually be many more ProtoMod types than any given ProtoCore unit has
  slots for (currently three), so the app must never assume a fixed lineup.
  `MainViewModel.Panels` starts (and, on any disconnect, reverts to) three
  `EmptySlotViewModel` placeholders; a `PresenceReport` rebuilds it via
  `ModuleCatalog.TryCreate`, which maps a `ProtoModId` to its panel view model.
  A present `ProtoModId` this build has no panel for yet becomes an
  `UnknownModuleViewModel` (shown with an orange status dot) rather than
  crashing or being silently dropped. Its message leads with the module's
  circuit code from `ProtoModBoardCatalog` (e.g. "Unsupported module:
  BasicLed (circuit code F02)") rather than the raw hex `ProtoModId` — the
  hex value means nothing to a person, the circuit code is what's actually
  printed/programmed on the physical board. Falls back to the hex ID only if
  a type is genuinely uncataloged on this side too. If asked to add a new
  ProtoMod, the only new-module-specific code goes in `ModuleCatalog` plus
  the panel itself — `MainViewModel` and the XAML shouldn't need to change.
- **Simulator mode** exists for developing/testing without hardware. `FrameDispatcher`
  talks to an `ISerialService` interface rather than `SerialService` directly, so it
  can point at either the real port or `MockSerialService` (swapped at runtime via
  `FrameDispatcher.SetTransport`). The "Simulator mode" checkbox in the UI toggles
  this; `MockSerialService` fakes a `PresenceReport` for all three demo modules and
  streams synthetic telemetry. When adding a new ProtoMod, the simulator doesn't need
  to know about it unless you want to demo it without hardware.
- **Traffic log** — a collapsed-by-default panel at the bottom of the window
  (`TrafficLogViewModel`) shows every frame sent/received (hex + decoded fields) plus
  framing errors and disconnect events, capped at the last 500 entries. Useful first
  stop when something doesn't behave as expected against real hardware. **The Info
  column is populated as of 2026-08-30** — every row gets a human-readable summary
  via `Models/FrameInterpreter.cs` (`Describe(ProtocolFrame)`), e.g. `SetCurrentLimitMa:
  100 mA` or `Error: PROTOCOL_ERR_NOT_IMPLEMENTED (0x04)`, instead of showing "-" for
  every normal frame the way it used to. It mirrors the payload layouts each panel's
  own `OnFrameReceived`/`SendCommand` already implement — if one of those layouts
  changes, update the matching case in `FrameInterpreter` too, or the Info column will
  quietly go stale while the panel itself stays correct. The Traffic Log grid also has
  explicit `Length` and `Checksum` columns now (the two Raw Frame byte "blocks" not
  already broken out into Module/MsgType/Payload) and a tooltip on the Raw Frame
  column header explaining the full byte layout — added specifically so the raw hex
  didn't have to be removed to make the grid readable, per an explicit user
  instruction not to drop the raw data.
- **Disconnect detection** — `SerialService` treats `IOException`,
  `UnauthorizedAccessException`, `InvalidOperationException`, and (as of
  2026-08-30) `TimeoutException` from the background read loop or a `Send()`
  call as "the port just disappeared" (cable pulled, device reset), tears
  itself down, and raises `Disconnected`. `FrameDispatcher` forwards that to
  the UI thread and also swallows those same exception types out of `Send()`
  so a mid-command drop can't crash the UI thread from a button click. The
  UI's response is identical to a manual Disconnect: slots reset to empty.
  **`ReadTimeout`/`WriteTimeout` are explicitly bounded at 1000ms** (`SerialPort`
  otherwise defaults to infinite) — added after a real user report that
  re-inserting a ProtoMod board can brown out ProtoCore's supply and force a
  full MCU reset (confirmed via firmware's own `RCC_CSR` boot diagnostics:
  `POR/PDR`, a genuine hardware/connector issue, not fixable in software),
  and `Send()` runs synchronously on the UI thread — an unbounded hang on a
  dead handle there freezes the whole app, not just one command. **Not yet
  confirmed against a real reproduction** — `MockSerialService` doesn't touch
  `SerialPort`, so this can only be regression-tested (normal flow still
  works), not exercised against a genuinely hung OS handle. The firmware
  session can reproduce the real reset on demand via ST-Link; that's the
  next step to actually confirm this fixes the freeze rather than just
  being a plausible mechanism. See CHANGELOG entry 32. **Tried, didn't
  reproduce it (entry 33):** an ST-Link-triggered NRST reset leaves the COM
  port continuously present with zero flicker (polled at 500ms resolution)
  and logs nothing in the System event log — it disturbs the MCU core but
  not VBUS/the USB link enough for Windows to notice. The real event
  (`POR/PDR`, a supply brownout from physical board insertion) can't be
  triggered over SWD; there's no remaining synthetic-repro path on either
  side. Don't re-attempt an SWD/NRST-based repro — this needs the user to
  watch a real reinsertion happen live, which is also the only way to learn
  whether Windows keeps the same COM port number across the reset.
- **Library tab** — a top-level tab beside "Slots" in the main content area
  (`Views/LibraryPanel.xaml`, `ViewModels/LibraryViewModel.cs`), showing the
  whole ProtoMod catalog rather than just what's plugged in. Read-only over
  the wire: it sends nothing and touches no module control logic; its one live
  input is `MainViewModel` calling `Library.UpdateInstalled(...)` after a
  `PresenceReport` (and `ClearInstalled()` on disconnect).
  **Each card tracks two independent facts, which must not be conflated:**
  whether the board has been *plugged in* (observed automatically from
  `PresenceReport` — `ModuleConnectionState`) and whether the user *says it's
  theirs* (`KitStatus`, only ever set by clicking). Seeing a board proves it
  was plugged in, not that anyone owns it — a borrowed or classroom board is
  the obvious case — so a newly-seen card asks "Is this ProtoMod part of your
  kit?" and the answer is always one click from being flipped. Never make a
  sighting silently imply ownership.
  **Tracking is per profile** (`Services/AccountStore.cs`, persisted to
  `%AppData%\ProtoVerse\accounts.json`), with a sign in / switch / sign out
  control in the window's top right and a picker dialog
  (`Views/SignInWindow.xaml`). **These profiles are explicitly not a security
  feature** (user decision, 2026-08-31: "no actual security, just for board
  tracking") — no password, no encryption, one readable file for everyone on
  the machine; the point is separating two people's kits on a shared PC. Don't
  add a password field later without making it mean something, since a fake
  login that looks real is worse than an obviously informal one.
  **Also settled and not to be relitigated:** this data is stored app-side,
  explicitly *not* on ProtoCore (it belongs to a person, not a board: a shared
  classroom ProtoCore would give every student the same wrong answer, a
  reflash would delete it, a second ProtoCore wouldn't know about the first),
  and **no wire-protocol or firmware change is involved at all** — the app
  already receives every `PresenceReport` and just records what it sees. The
  document shape is deliberately close to what a real server-backed account
  would send, so that swap is a change inside `AccountStore`, not to its
  callers. All its file I/O degrades to "no accounts" rather than throwing.
  **The catalog content
  (`Models/ProtoModLibraryCatalog.cs`) is hardcoded for v1 on purpose and is
  held to a strict no-fabrication rule**: every description, schematic
  summary, project idea, and "leads into" link is quoted from a document that
  actually exists (the manuals in `PROTOVERSE/Manuals/`, the KiCad exports in
  `PROTOVERSE/Finished Modules/`, or this repo's own docs), each carries a
  `*Source` field that the UI displays, and anything with no source material
  is left `null` and rendered as "coming soon". Do not fill one of those
  nulls with something that sounds right — write the manual first, then quote
  it. Progression links are likewise never inferred from circuit-code order
  or series; exactly one exists today (F01 → F02) because exactly one manual
  sentence establishes one. **The one exception to "quote, don't derive" is
  the family**: every ProtoMod belongs to Fundamentals, Explorers, or
  Advanced, keyed by the first letter of its circuit code (F/E/A) — confirmed
  as a product rule by the user 2026-08-31, so a board with no manual still
  gets a correct family. The Library's filter row is built on exactly that
  rule. Should eventually move to a JSON/manifest source shared with the
  manual docs — the records are already flat and JSON-friendly, and
  `Entries` is the only thing the rest of the app reads.
- **Help tab** — lives in the same collapsed-by-default bottom `Expander` as the
  Traffic Log (they're now two `TabItem`s of one `TabControl` there, not two
  separate Expanders). `HelpViewModel.RevisionNotes` is a hand-maintained,
  newest-first list of short, single-sentence, **end-user-facing** summaries —
  deliberately not a mirror of `CHANGELOG.md` (that file is for whoever develops
  this app; routine internal/refactor entries there don't belong here). Add a line
  to `RevisionNotes` whenever a change is worth telling an end user about.
  `HelpViewModel.SupportedModules` reads straight from `ModuleCatalog.SupportedModules`
  — don't hand-maintain a second "what's supported" list anywhere; if a new ProtoMod
  panel gets registered in `ModuleCatalog`, this list picks it up automatically.
- **App icon.** `Assets/AppIcon.ico` (multi-res, 16/32/48/256px) is built from the
  real ProtoVerse logo (`PROTOVERSE/logo.jpg`, background chroma-keyed out) —
  set via `<ApplicationIcon>` in the `.csproj` (the `.exe`/taskbar/Explorer
  icon) and `Window.Icon` in `MainWindow.xaml` (the title bar/Alt-Tab icon).
  `Assets/logo_transparent_master.png` is the cropped/centered transparent
  source, kept for re-exporting at a different size later without redoing the
  background removal. If the logo ever changes, regenerate both from a fresh
  export of the real logo, not by hand-editing the `.ico`.
  **Settled sizing/weight, after live-testing several alternatives against
  the real taskbar (2026-08-30, CHANGELOG entry 35):** original (undilated)
  stroke weight - a thickened version closes the visual-weight gap against
  solid-fill icons like VS Code/Excel but collapses into an unrecognizable
  blob at 16/32px once pushed far enough to actually look "bold," not worth
  it. Zero-margin "contain" fit - the mark's longer dimension (width, source
  is a wide/short 1.6:1 shape) fills the canvas edge to edge, the shorter
  dimension is letterboxed - is the largest this specific mark can be
  without clipping the orbit ring's curled tail tips; a version that also
  filled the shorter dimension necessarily clipped those tails and was
  rejected once shown. Don't re-litigate either without a reason to.
- **A direct small-region `Graphics.CopyFromScreen` of the taskbar
  intermittently returns a blank white image on this machine** (Windows 11) -
  likely a DWM/hardware-overlay quirk with partial `BitBlt` capture of the
  modern taskbar. Capturing the full desktop and cropping the target region
  out of that bitmap in memory worked reliably every time. Prefer that
  approach for any future taskbar screenshot verification rather than a
  direct small-region capture.

## Gotchas already hit (save yourself the loop)

- **After reflashing ProtoCore, the app's "Refresh" may not show a new COM
  port — this is a Windows driver issue, not an app bug.** Confirmed
  2026-08-30 (first real-hardware session): `SerialPort.GetPortNames()`
  (what Refresh calls) accurately reports what Windows itself sees — if it's
  not listing the board, Windows genuinely doesn't have an enumerated COM
  port for it yet. Diagnosed via `Get-WinEvent`/`Get-PnpDevice`: Windows saw
  the board's USB CDC interface (`VID_0483&PID_5740`, the standard STM32
  Virtual COM Port ID) and logged "requires further installation," but
  driver install never completed, leaving a phantom (`CM_PROB_PHANTOM`,
  i.e. not currently present) device node. A firmware reflash via ST-Link
  typically only resets the MCU — it doesn't force a real USB
  disconnect/reconnect, which Windows needs to retry driver installation
  cleanly. Fix: unplug/replug the USB cable, then check Device Manager's
  Ports (COM & LPT) for a warning icon on "STMicroelectronics Virtual COM
  Port" and update the driver if present. If that doesn't resolve it, worth
  checking the firmware's CDC ACM descriptor setup
  (`usbd_cdc_if.c`/`usbd_desc.c` in the `Protocore` codebase) — a malformed
  descriptor can produce this exact "identified but won't finish installing"
  symptom.
- Any `Button` (or similar) with a **fixed pixel `Width`** risks clipped/missing
  text if the branded `Button` style's font weight, padding, or content ever
  changes — happened for real (`Disconnect` clipped after entry 12's theming
  added `FontWeight="SemiBold"` + `Padding="12,6"` to a style written after that
  button's `Width="80"` was already set). Fixed by using `MinWidth` instead of
  `Width` everywhere a button's content should never be allowed to clip — prefer
  `MinWidth` over `Width` for any future button too, unless there's a specific
  reason several buttons must render at *exactly* the same width.
- A custom `TabControl` `ControlTemplate`'s content-hosting row **must not** be
  `Height="*"` if the `TabControl` sits inside something that doesn't impose a
  bounded height on it (e.g. an `Expander`, as the Traffic Log/Help tabs do) — a
  star row measures to zero under an unconstrained parent, so the selected tab's
  content silently renders at zero height (nothing visible, no error). Use
  `Height="Auto"` and let the tab content's own sizing (e.g. a fixed-height
  `DockPanel`) drive it instead. **The converse is also true, which is why
  `App.xaml` now has two TabControl styles:** the implicit (`TargetType`-only)
  one keeps `Height="Auto"` for the Expander case above, and a keyed
  `MainTabControlStyle` uses `Height="*"` for the top-level Slots/Library tabs,
  which sit in a bounded `Height="*"` grid row where `Auto` would instead let
  a long list overflow past the row rather than scrolling inside it. Don't
  "unify" these two — they're different because their parents are.
- The usual `<ContentPresenter ContentSource="SelectedContent"/>` shortcut for a
  custom `TabControl` template (used in Microsoft's own default template) did not
  render any content in this app, for reasons not tracked down further. Use an
  explicit `Content="{TemplateBinding SelectedContent}"` /
  `ContentTemplate="{TemplateBinding SelectedContentTemplate}"` binding instead
  (plus `x:Name="PART_SelectedContentHost"` for convention) — confirmed working.
  If retemplating `TabControl` again, don't reach for `ContentSource` first;
  start with the explicit binding.
- **Windows caches the taskbar icon per file path and doesn't reliably
  invalidate it on rebuild.** After adding `Assets/AppIcon.ico`, a taskbar
  screenshot of the running app still showed the old generic icon even
  though the `.exe` was freshly rebuilt with the icon correctly embedded
  (confirmed via `Icon.ExtractAssociatedIcon` reading the file directly,
  bypassing the shell cache). This is expected after rebuilding the same
  `.exe` path repeatedly during a dev session — not a bug, and not something
  the app can fix. If an icon change doesn't show up in the taskbar, check
  the title bar first (a live render, not cached) or extract the icon
  directly from the file before assuming the embed failed. **Confirmed fix**
  (2026-08-30, this exact scenario): delete
  `%LocalAppData%\IconCache.db` and
  `%LocalAppData%\Microsoft\Windows\Explorer\iconcache_*.db`/
  `thumbcache_*.db`, then restart `explorer.exe` (`Stop-Process -Name
  explorer -Force` followed by `Start-Process explorer.exe`) — relaunching
  the app afterward showed the correct icon in the taskbar. A full reboot
  isn't actually necessary, just an Explorer restart.
- `System.IO.Ports` (for `SerialPort`) needs an **explicit** `PackageReference`
  in the `.csproj` even on `net8.0-windows` — it's not included implicitly.
  Already added; don't remove it.
- Converters (`IValueConverter` implementations) used by `StaticResource` from
  more than one XAML file **must** be registered in `App.xaml`'s
  `Application.Resources`, not a single window's `Window.Resources` — a
  window-scoped resource isn't visible from a separate `UserControl` file.
  This caused a startup `XamlParseException` once already.
- If `dotnet build` fails with the `.exe` "locked by another process," a
  previous run is still alive — check the taskbar/Task Manager for
  `ProtoVerseApp.exe` and close it before rebuilding. **This bites hardest
  when driving the app from a script:** a leftover instance from an earlier
  automated run silently kept the build from replacing the `.exe`, so several
  rounds of "build succeeded, but the fix didn't work" were actually testing
  stale code (2026-08-31, chasing a sign-in button that appeared to do
  nothing). `dotnet build` reports the copy failure as an error, but it's easy
  to miss under warnings — grep the output for `error MSB` too, not just
  "Build succeeded", and kill stray `ProtoVerseApp` processes before every
  scripted run.
- **UI Automation can't see this app's modal dialogs in this environment.**
  A `ShowDialog()` window (e.g. `SignInWindow`) opens correctly but never
  appears in the UIA tree, and while it's open the *whole* process's UIA tree
  goes unreachable — which reads exactly like "the button did nothing." Win32
  `EnumWindows` filtered by process id does see it (correct size and
  position), so use that to confirm a dialog opened, and drive the rest of a
  test through the main window. Related to, and probably the same underlying
  cause as, the blank-white screenshot capture noted above.
- The dev environment is VS Code + `dotnet build` / `dotnet run` from the
  integrated terminal, not full Visual Studio — no XAML visual designer in
  play, so XAML errors only surface at build/run time, not while typing.
- When copying this repo to a new machine/location, copy its *contents* into
  the destination folder — don't drag the `ProtoVerseApp` folder itself into an
  existing folder of the same name, which nests a redundant `ProtoVerseApp\`
  wrapper one level deeper (this has happened repeatedly; see CHANGELOG entries
  5 and 6). Also: a Claude Code session's working directory (whatever folder
  VS Code had open) is a fixed path for the life of that session — the sandbox
  keeps a live shell process anchored there, so on Windows that exact directory
  can't be renamed or `rmdir`'d mid-session even after everything is moved out
  of it (fails with "device or resource busy" from both the Bash and PowerShell
  tools). If a restructure needs to remove that directory, move its *contents*
  elsewhere and leave the empty shell for the user to delete after closing/
  reopening VS Code, rather than fighting the lock.
