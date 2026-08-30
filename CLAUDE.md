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
[STX 0x02] [ProtoModId] [MsgType] [Length] [Payload...] [Checksum] [ETX 0x03]
```

- `ProtoModId` values are a fixed, locked vocabulary shared with the ProtoCore
  firmware (a separate codebase) — `0x01` BlinkyLed, `0x02` AccelTemp, `0x03`
  ElectronicLoad, `0xF0` Core. Adding a ProtoMod means adding an ID here *and*
  in firmware, together.
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
- **Open question, needs the user's confirmation, not a guess:** firmware maps
  its `BOARD_BASIC_LED` enum value to this app's `AccelTemp` (`0x02`) — same
  slot-2 position in firmware's lookup table, but the names don't match
  (`BOARD_BASIC_LED` vs `AccelTemp`) and neither side has independently
  verified they're the same physical board. Don't build against that mapping
  being correct until it's confirmed.
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
  layouts as settled.
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

## Gotchas already hit (save yourself the loop)

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
