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
  AccelTemp, `0x0003` ElectronicLoad, `0xFFF0` Core, `0xFFFF` Broadcast
  (reserved IDs deliberately live at the top of the range so they read as
  system addresses, not catalog entries). Adding a ProtoMod means adding an
  ID here *and* in firmware, together. **`PresenceReport`'s payload is a
  FIXED `SlotCount*2`-byte array (as of 2026-08-30), not a variable-length
  list of only the occupied slots** — exactly one `ProtoModId` per physical
  slot, always in slot order, 2 bytes little-endian each; an empty slot
  reports `ProtoModId.None` (`0x0000`) rather than being omitted. This
  replaced an earlier skip-empty-slots format after the user hit a real bug
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
- **Two pre-existing firmware bugs found while wiring this in** (unrelated to
  the protocol itself, noted here since they affect whether ProtoCore even
  boots): `program.c` used `hi2c1` without including `init.h`, and `init.c`
  duplicates globals (`hi2c1`, `hadc1`, `SystemClock_Config`, `Error_Handler`)
  that also live in `main.c`, which fails to link if both are built together.
  The firmware session excluded `init.c` from the CMake build rather than
  fixing/deleting it (their user's call to make, not this session's). This
  likely means the CubeIDE/Eclipse build — which doesn't exclude `init.c` —
  has never linked successfully either, though that's unconfirmed.
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
- **Branded dark theme.** `App.xaml` defines the ProtoVerse brand palette
  (deep navy background, teal/green/blue/orange accents, off-white/lavender
  text — sampled from the logo lockup) as named `Color`/`SolidColorBrush`
  resources, plus implicit (`TargetType`-only) `Style`s for `Button`,
  `TextBox`, `CheckBox`, `ComboBox`, `Expander`, and `DataGrid` so every
  control picks the theme up automatically. New views should reference these
  `StaticResource` brushes rather than hardcoding colors; the two status-dot
  converters (`BoolToBrushConverter`, `SlotStateToBrushConverter`) are the one
  exception — a converter can't bind to a `StaticResource`, so their brush
  hex values are literal and must be kept in sync by hand if the palette in
  `App.xaml` ever changes.
- Accel+Temp and Electronic Load panels exist and render, but their
  command/response payload layouts in `OnFrameReceived` are **explicit
  placeholders** — marked with `TODO` comments — because those ProtoMods'
  actual firmware command sets aren't defined yet. Don't treat those byte
  layouts as settled. **Their UI is real, though, not a placeholder** — both
  panels have live OxyPlot charts (`Charts/ChartTheme.cs` builds the
  dark-themed `PlotModel`s) built deliberately decoupled from that parsing:
  `OnFrameReceived` turns bytes into properties, a separate
  `AppendToChart(s)` method just plots whatever those properties currently
  hold. Accel+Temp specifically: a temperature trend line, an X/Y
  "bubble-level" tilt plot (fixed ±1.5g axes so the origin is always the
  visual center, a single `ScatterSeries` point moved each update rather
  than a trend history), and a Z fill gauge (`LinearBarSeries` with
  `BaseValue` set to -1g, so the bar fills up above -1g and down below it,
  per the user's spec) — not three trend lines. This was built and verified
  entirely against Simulator mode's fake telemetry, on purpose, so the UI
  didn't have to wait on firmware defining the real payload — when that
  lands, only the parsing changes, not the
  charts. Follow the same split for any future panel that streams data.
- Panels are populated dynamically from `PresenceReport`, not hardcoded. There
  will eventually be many more ProtoMod types than any given ProtoCore unit has
  slots for (currently three), so the app must never assume a fixed lineup.
  `MainViewModel.Panels` starts (and, on any disconnect, reverts to) three
  `EmptySlotViewModel` placeholders; a `PresenceReport` rebuilds it via
  `ModuleCatalog.TryCreate`, which maps a `ProtoModId` to its panel view model.
  A present `ProtoModId` this build has no panel for yet becomes an
  `UnknownModuleViewModel` (shown with an orange status dot) rather than
  crashing or being silently dropped. If asked to add a new ProtoMod, the only
  new-module-specific code goes in `ModuleCatalog` plus the panel itself —
  `MainViewModel` and the XAML shouldn't need to change.
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
  stop when something doesn't behave as expected against real hardware.
- **Disconnect detection** — `SerialService` treats `IOException`,
  `UnauthorizedAccessException`, and `InvalidOperationException` from the background
  read loop or a `Send()` call as "the port just disappeared" (cable pulled, device
  reset), tears itself down, and raises `Disconnected`. `FrameDispatcher` forwards
  that to the UI thread and also swallows those same exception types out of `Send()`
  so a mid-command drop can't crash the UI thread from a button click. The UI's
  response is identical to a manual Disconnect: slots reset to empty.
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
  `DockPanel`) drive it instead.
- The usual `<ContentPresenter ContentSource="SelectedContent"/>` shortcut for a
  custom `TabControl` template (used in Microsoft's own default template) did not
  render any content in this app, for reasons not tracked down further. Use an
  explicit `Content="{TemplateBinding SelectedContent}"` /
  `ContentTemplate="{TemplateBinding SelectedContentTemplate}"` binding instead
  (plus `x:Name="PART_SelectedContentHost"` for convention) — confirmed working.
  If retemplating `TabControl` again, don't reach for `ContentSource` first;
  start with the explicit binding.
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
  `ProtoVerseApp.exe` and close it before rebuilding.
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
