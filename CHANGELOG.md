# Changelog

Chronological record of changes made to this app by Claude Code, in the order they
were requested. Each entry has the prompt that drove it (paraphrased where long),
why it was done, and what actually changed. Later entries sometimes revise or fully
replace the approach from an earlier one — noted inline where that happened, so the
history stays honest rather than implying a straight line.

Each entry is timestamped **date and time** (not just date) as of 2026-08-30 entry
9 onward. Entries 1–8 predate that convention and only have a date on record — their
time of day was never captured, so it's marked "(time not recorded)" rather than
guessed.

### 1. Traffic log panel + serial simulator mode
**2026-08-29, time not recorded**

**Prompt:** "Add a raw traffic log panel. A collapsible debug panel showing every
frame sent/received (hex + decoded fields)... Build a serial mock/simulator mode. A
fake SerialService that generates plausible responses without real hardware
attached."

**Purpose:** Give development and debugging a way to see exactly what's on the wire,
and a way to exercise the whole app (UI, panels, presence detection) without a
ProtoCore board plugged in.

**Changes:**
- Added `Services/ISerialService.cs` — abstraction over "something that sends/
  receives frames," so `FrameDispatcher` can point at real hardware or a fake
  transport interchangeably.
- `Services/SerialService.cs` now implements `ISerialService` (no behavior change).
- Added `Services/MockSerialService.cs` — fakes ProtoCore: answers
  `PresenceRequest` with all three demo modules present, echoes Blinky LED state,
  and streams synthetic Accel+Temp / Electronic Load telemetry once a second.
- `Services/FrameDispatcher.cs` — added `FrameSent` event; added `SetTransport(...)`
  so the app can swap between real serial and the mock at runtime without
  disturbing already-subscribed panels.
- `Models/TrafficLogEntry.cs` (new) — formats a sent/received frame or a framing
  error into TX/RX/ERR + module + msg type + payload hex + full raw-frame hex.
- `ViewModels/TrafficLogViewModel.cs` (new) — subscribes to
  `FrameSent`/`FrameReceived`/`FrameError`, keeps the last 500 entries.
- `ViewModels/MainViewModel.cs` — added `SimulatorMode` toggle; `Connect()` talks to
  the mock instead of a COM port when it's on.
- `Views/MainWindow.xaml` — added the "Simulator mode" checkbox (disables the real
  port dropdown/refresh while on), and a collapsed-by-default "Traffic Log"
  `Expander` with a `DataGrid` + Clear button. Widened the window 720→900 so the
  log's columns aren't clipped.

### 2. Disable panel controls when not present; detect unplug/disconnect
**2026-08-29, time not recorded**

**Prompt:** "Disable each panel's controls when IsPresent is false. Right now
IsPresent just drives a status dot — the buttons are still clickable even when a
module isn't detected... Handle unplug/disconnect gracefully. Detect when the
serial port drops (cable pulled, device reset) and reflect it in the UI instead of
silently failing on the next Send."

**Purpose:** Stop the UI from letting you click "Toggle" on an LED panel that isn't
actually plugged in, and stop a dropped cable from silently going unnoticed (or
crashing on the next command) instead of being shown to the user.

**Changes:**
- `Views/MainWindow.xaml` — bound the shared panel `ContentPresenter`'s `IsEnabled`
  to `IsPresent`, disabling every control inside whichever panel is templated in.
  *(Superseded in entry 4 — enabling/disabling is now structural instead of a
  flag.)*
- `Services/ISerialService.cs` — added a `Disconnected` event, distinct from
  `FrameError` (framing/checksum problems): this one means the transport itself is
  gone.
- `Services/SerialService.cs` — wrapped the background read loop and the write call
  in a catch for `IOException` / `UnauthorizedAccessException` /
  `InvalidOperationException` (what `SerialPort` throws when the device
  disappears); tears itself down and raises `Disconnected` with the reason.
- `Services/MockSerialService.cs` — added the `Disconnected` event member to satisfy
  the interface (never raised — no real cable to pull on a simulator).
- `Services/FrameDispatcher.cs` — forwards `Disconnected` to the UI thread; wrapped
  `Send()` so a mid-command cable pull can't throw an unhandled exception out of a
  button click.
- `ViewModels/MainViewModel.cs` — added `OnTransportDisconnected`, wired through the
  same `SetDisconnectedState` path the manual Disconnect button already used.
- `ViewModels/TrafficLogViewModel.cs` — also logs the disconnect reason as a Traffic
  Log entry.

### 3. Presence should start unknown, not "not present"
**2026-08-29, time not recorded**

**Prompt:** "All slots should start as unknown (show as empty) until a device is
connected to and 'identify slots' is done."

**Purpose:** A never-connected (or just-disconnected) panel was showing the same
"not present" indicator as a panel ProtoCore had actually checked for and confirmed
absent — those are different facts and shouldn't look identical.

**Changes:**
- `ViewModels/ModulePanelViewModelBase.cs` — changed `IsPresent` from `bool` to
  `bool?` (`null` = unknown); added `IsConfirmedPresent` (`IsPresent == true`) to
  drive control enablement. *(Removed in entry 4.)*
- `ViewModels/MainViewModel.cs` — disconnect (manual or unexpected) now resets
  presence to `null` instead of `false`.
- `Converters/PresenceToBrushConverter.cs` (new) — tri-state dot: null → hollow,
  true → green, false → gray. *(Deleted in entry 4, replaced by
  `SlotStateToBrushConverter`.)*
- `Views/MainWindow.xaml` — status dot gained a gray stroke so "unknown" reads as a
  visible hollow ring rather than disappearing.

### 4. Stop assuming which modules exist at all
**2026-08-29, time not recorded**

**Prompt:** "There will be a huge amount of different boards that could be installed
(but never more than three) - the gui should not assume what those three are.
Assume at boot they are empty and say such. Don't start with led, current, etc."

**Purpose:** The app was still hardcoding Blinky LED / Accel+Temp / Electronic Load
into the panel list at startup — true "don't assume" requires the slot list to be
built entirely from what `PresenceReport` says, since a real deployment could have
any three of a much larger, growing catalog of ProtoMods plugged in, and the app
should degrade gracefully when it encounters one it doesn't recognize yet.

**Changes:**
- `ViewModels/ModulePanelViewModelBase.cs` — removed `IsPresent`/`IsConfirmedPresent`
  entirely (a panel VM only ever exists in a slot while its module is actually
  present, so there's nothing left to flag); added a constant `SlotState` property
  and a `Detach()` method so a discarded panel unsubscribes from the dispatcher.
- `ViewModels/SlotState.cs` (new) — `Empty` / `Occupied` / `Unsupported` enum
  driving the status dot.
- `ViewModels/EmptySlotViewModel.cs` (new) — placeholder shown for a slot with
  nothing detected; used for all three slots at boot and after any disconnect.
- `ViewModels/UnknownModuleViewModel.cs` (new) — placeholder for a module ProtoCore
  reports that this build has no panel for, e.g. `"Unsupported module (9, 0x09)"` —
  shown instead of crashing or silently dropping the slot.
- `ViewModels/ModuleCatalog.cs` (new) — the one place mapping `ProtoModId → panel
  view model factory`; adding support for a new ProtoMod means adding one line
  here, nothing in `MainViewModel`.
- `ViewModels/MainViewModel.cs` — `Panels` is now `ObservableCollection<object>`;
  starts (and, on disconnect, reverts to) three `EmptySlotViewModel`s; a
  `PresenceReport` fully rebuilds the slot list via `ModuleCatalog`, detaching any
  discarded module panels first to avoid leaking dispatcher subscriptions.
- `Converters/SlotStateToBrushConverter.cs` (new, replacing
  `PresenceToBrushConverter`) — Empty → transparent, Occupied → green, Unsupported →
  orange.
- `App.xaml` — converter resource key updated to match.
- `Views/MainWindow.xaml` — added `DataTemplate`s for `EmptySlotViewModel` and
  `UnknownModuleViewModel`; status dot now binds to `SlotState`; removed the
  now-unneeded `IsEnabled` binding on the panel `ContentPresenter` (an empty/
  unsupported slot's template has no interactive controls to begin with, so there's
  nothing to disable).
- `CLAUDE.md` — "Current state" section rewritten to describe dynamic slot
  population instead of the old hardcoded-three-panel design.
- `README.md` — architecture diagram, "Identify slots," and the former "Moving to
  dynamic panels later" section (now "Adding a new ProtoMod's panel") updated to
  match the implemented design.

### 5. This changelog
**2026-08-29, time not recorded**

**Prompt:** "create a change log file that captures all the changes you make (and
have already made) with a listed purpose and summary of my prompt."

**Purpose:** Keep a durable, human-readable record of what changed and why, tied
back to the request that drove it.

**Changes:**
- Added `CHANGELOG.md` (this file).

### 6. Backfill CLAUDE.md; make logging changes a standing rule
**2026-08-29, time not recorded**

**Prompt:** "update CLAUDE.md with any changes you've made in this session where
appropriate so we can hit the ground running in the future. Moving forward all
changes to the code must be updated in the CHANGELOG.md and if appropriate
CLAUDE.md."

**Purpose:** `CLAUDE.md` (read automatically every session) was still missing
several things built this session — simulator mode, the traffic log, and
disconnect detection weren't documented anywhere in it. Also codify going forward
that every code change gets a `CHANGELOG.md` entry, and `CLAUDE.md` gets updated
too whenever a change touches something it's meant to document (a platform
decision, the wire protocol, "Current state," or a new gotcha).

**Changes:**
- `CLAUDE.md` — added a "Keeping this file and CHANGELOG.md current" section
  stating the standing rule above; added "Current state" bullets for simulator
  mode (`ISerialService`/`MockSerialService`/`FrameDispatcher.SetTransport`), the
  traffic log panel, and disconnect detection
  (`SerialService`'s exception handling → `Disconnected` event →
  `FrameDispatcher` → UI reset to empty slots) — none of which were previously
  documented there.

### 7. Make CLAUDE.md actually auto-load
**2026-08-29, time not recorded**

**Prompt:** "do you actually check CLAUDE.md upon initiating this extension" →
confirmed no, then: "You do what you need to so it's automatically read."

**Purpose:** This repo's `CLAUDE.md` lives at `ProtoVerseApp/CLAUDE.md`, but a
session's actual working directory is the parent folder
(`c:\Users\Zach\Downloads\ProtoVerseApp`) one level up. Claude Code only auto-loads
`CLAUDE.md`/`CLAUDE.local.md` by walking *upward* from the working directory at
session start — it does not scan subdirectories proactively, so this file was never
actually being auto-loaded despite its own header claiming it is. (A subdirectory
`CLAUDE.md` does get pulled in, but only lazily, when a file inside that
subdirectory is read — not at launch.) Confirmed via `claude-code-guide` before
acting, rather than assuming.

**Changes:**
- Added `c:\Users\Zach\Downloads\ProtoVerseApp\CLAUDE.md` (new, one level up from
  this file) containing a single `@ProtoVerseApp/CLAUDE.md` import line. Claude
  Code's `@import` syntax pulls the real `CLAUDE.md` in from a subdirectory at
  session start without moving or duplicating its content — the working
  `CLAUDE.md` stays where it already was, alongside `README.md`, `CHANGELOG.md`,
  and the `.sln`.

### 8. Collapsed redundant nested `ProtoVerseApp` folders
**2026-08-30, time not recorded**

**Prompt:** "reduce the ridiculous number of redundant ProtoverseApp folders
first" (said after "copied this over" while looking at the `.csproj`).

**Purpose:** Each time this repo was copied to a new machine/location (this repo
has moved at least twice — see entry 5's `c:\Users\Zach\Downloads\...` path vs.
today's `...\PROTOVERSE\Software\...` path), the copy landed one level *inside*
the existing folder of the same name instead of replacing it, so the nesting
grew by one `ProtoVerseApp\` every time. By this session the real repo root
(`.sln`, `README.md`, `CHANGELOG.md`, real `CLAUDE.md`) was sitting three empty
`ProtoVerseApp\` wrapper folders deep, and the project folder one further still
— five `ProtoVerseApp` path segments before reaching `ProtoVerseApp.csproj`.

**Changes:**
- Flattened the tree so `...\Software\ProtoVerseApp\` is now the true repo root
  (`.sln`, `README.md`, `CHANGELOG.md`, `CLAUDE.md` live directly in it) and
  `...\Software\ProtoVerseApp\ProtoVerseApp\` is the project folder
  (`ProtoVerseApp.csproj`, `App.xaml`, `Converters/`, `Models/`, `Services/`,
  `ViewModels/`, `Views/`) — the normal, non-redundant solution/project
  convention the `.sln`'s relative project reference already expected.
- Deleted the stale `bin/`/`obj/` build output from the old location rather than
  moving it; it regenerates on next build.
- Discarded the old `@ProtoVerseApp/CLAUDE.md` stub file from entry 5 — it was
  only an `@import` pointer with no content of its own, and it lived at one of
  the wrapper levels being removed. It's no longer needed for auto-loading:
  Claude Code walks upward from the working directory looking for `CLAUDE.md` at
  every ancestor level, and the real one is still found a couple of levels up
  the (now much shorter) chain.
- Left one empty leftover `ProtoVerseApp\ProtoVerseApp` pair behind inside the
  new project folder — see the new gotcha below.

### 9. Align wire protocol with the firmware session
**2026-08-30, 01:19 CDT**

**Prompt:** "I am going to reach out to you from the downstream stm32 hardware
agent. You two must align on a protocol for communication between the pc app
(you) and the downstream hardware (other agent)."

**Purpose:** The PC app's v1 frame format was fully implemented on this side but
had no firmware counterpart to actually talk to — needed to agree cross-session
with the Claude Code session working on the ProtoCore firmware (`Protocore`
project, STM32F401RE, separate codebase) on transport and framing before either
side builds more on top of an assumption the other side hasn't confirmed.

**Changes:**
- No code changes. Coordinated with the firmware session via cross-session
  messaging: agreed to mirror this app's existing frame format
  (`Models/ProtocolFrame.cs`) as the shared spec, transport over USB CDC (not
  UART1, which is TX-only in firmware today), little-endian for future
  multi-byte payload fields, and no ack/retry/sequence-number scheme for now.
- Learned from the firmware session that ProtoCore's EEPROM-based slot
  identification (`identify_slots()` in `program.c`) is implemented but never
  invoked from firmware's `main()`, so it has never run against real hardware
  — corrects this file's prior "done/working" claim (see `CLAUDE.md`'s wire
  protocol section). Also learned firmware has no binary frame parser/encoder
  yet at all (`CDC_Receive_FS()` doesn't dispatch incoming bytes today) — the
  firmware session is implementing both plus wiring `identify_slots()` in for
  real, pending their own user's confirmation since it changes what runs on
  boot.
- `CLAUDE.md` — corrected the "done/working" presence-detection claim and
  added the agreed transport/framing details to the wire protocol section.

### 10. Timestamp changelog entries with date and time
**2026-08-30, 01:19 CDT**

**Prompt:** "adjust change log updates so that each change is associated with a
date and time. next time you speak to the other agent advise that they do the
same. the same goes for telling them how, when, and where to use a CLAUDE.md
file."

**Purpose:** Date-only entries (or, worse, one date header shared by several
entries) don't say when in a session's timeline a change landed relative to
others on the same day — a timestamp per entry does. Also fixed a pre-existing
numbering bug (two entries were each numbered "6," two were each numbered "7")
introduced when entry 9 was appended without checking that the prior day's
entries had already reused those numbers.

**Changes:**
- `CHANGELOG.md` — restructured from per-day `##` date headers with numbered
  entries underneath, to one `###` heading per entry with its own `**date,
  time**` line directly below. Renumbered entries 1–10 sequentially (no more
  duplicate numbers). Entries 1–8 predate this convention and have no recorded
  time of day, so they're marked "(time not recorded)" rather than guessed;
  entries 9–10 have real timestamps since they happened this session.

### 11. Firmware implements the wire protocol (compile-verified, unflashed)
**2026-08-30, 01:26 CDT**

**Prompt:** none — the firmware session reported back unprompted on its
progress implementing the protocol agreed in entry 9.

**Purpose:** Record what the firmware side actually built so this app's docs
don't keep describing ProtoCore as having no frame parser — and flag the open
items the firmware session raised that need this project's/the user's input
before either side should trust presence detection end-to-end.

**Changes:**
- No code changes on this side. `CLAUDE.md`'s wire protocol section updated to
  describe what the firmware session implemented: `protocol.h`/`protocol.c`
  mirroring `ProtocolFrame.cs` byte-for-byte, wired into `CDC_Receive_FS()`
  (RX) and `CDC_Transmit_FS()` (TX) with the agreed resync-on-error behavior,
  `main()` now calling `start_program()` for the first time, and
  `poll_protocol()` answering `PresenceRequest` with a real `PresenceReport`
  built from `active_slots[]`. `Protocore.elf` builds and links clean via
  CMake/Ninja (11% RAM / 9% Flash) but **nothing has been flashed to real
  hardware** — slot detection over I2C/EEPROM remains unverified in practice.
- Flagged two things needing follow-up, documented in `CLAUDE.md`:
  1. Firmware's `BOARD_BASIC_LED` enum maps to this app's `AccelTemp`
     (`0x02`) by lookup-table position, not by matching name — neither side
     has confirmed that's actually the same physical board.
  2. Two pre-existing firmware bugs the firmware session hit while wiring
     this in (missing `init.h` include in `program.c`; `init.c` duplicating
     globals also defined in `main.c`, breaking the link if both are built).
     Firmware session excluded `init.c` from the CMake build rather than
     fixing it, leaving that decision to their user; this likely means the
     CubeIDE/Eclipse build has the same problem, unconfirmed.
- Firmware session declined to add its own `CLAUDE.md` unilaterally off a
  peer's suggestion, correctly treating that as a decision for their user —
  surfacing it to their user instead.

### 12. Brand the UI with the ProtoVerse color scheme
**2026-08-30, 01:38 CDT**

**Prompt:** "make the app visually appealing with some colors and professional
looking. Use these colors for the scheme" (with the ProtoVerse/ProtoCore/
ProtoMods logo lockup image attached: deep navy field, teal-to-green orbit
mark, blue/green/orange ProtoMod cube accents, off-white type).

**Purpose:** The app had no visual identity — plain default WPF Aero chrome
(white background, system gray buttons) unrelated to the ProtoVerse brand
shown in the reference logo. Wanted a professional, on-brand dark theme
instead.

**Changes:**
- `App.xaml` — added a centralized brand palette (`BgColor`/`SurfaceColor`/
  `SurfaceAltColor`/`BorderColor`, accent teal/green/blue/orange, primary/
  secondary text colors, all sampled from the reference logo) as named
  `Color`/`SolidColorBrush` resources, plus a teal→green `AccentGradientBrush`.
  Added implicit (`TargetType`-only, no `x:Key`) `Style`s for `Button`,
  `TextBox`, `CheckBox`, `ComboBox`/`ComboBoxItem`, `Expander`, `DataGrid`/
  `DataGridColumnHeader`/`DataGridCell` so every control across the app picks
  up the dark theme automatically without touching each view — `Button` and
  `TextBox` get full `ControlTemplate` overrides for rounded corners (default
  WPF chrome doesn't support `CornerRadius` directly); `ComboBox` gets a
  simplified dark popup/list template.
- `Views/MainWindow.xaml` — set `Window.Background`/`Foreground` to the new
  brand brushes; added a header row with a small vector "orbit" mark (two
  `Ellipse`s approximating the logo's ring-and-nodes icon, stroked with
  `AccentGradientBrush`) next to the "ProtoVerse" wordmark and "Build your
  universe of electronics." tagline; restyled the per-slot panel `Border`s to
  use the surface/border brushes with rounded corners; status-dot `Ellipse`
  stroke now uses `BorderBrush` instead of a literal `"Gray"`; status message
  and empty/unsupported-slot placeholder text now use `TextSecondaryBrush`
  instead of a literal `"Gray"`. Grid grew one more `Auto` row for the header;
  window height 640→700 to fit it.
- `Converters/BoolToBrushConverter.cs` (Blinky LED on/off dot) and
  `Converters/SlotStateToBrushConverter.cs` (slot status dot) — swapped
  `Brushes.LimeGreen`/`Brushes.Orange`/`Brushes.LightGray` for brushes built
  from the same brand hex values used in `App.xaml`, so the dots match the new
  palette. (A converter is a plain C# class, not part of the visual tree, so
  it can't bind to a `StaticResource` — the hex values are duplicated
  literally there rather than shared, noted in each file's doc comment.)
- Verified by actually building (`dotnet build`, no XAML parse errors — that's
  where a bad `StaticResource` key or malformed `ControlTemplate` would
  surface) and launching the real `.exe`; this session doesn't have a way to
  screenshot the user's desktop, so final visual confirmation is pending the
  user actually looking at the running window.
- This machine had no .NET SDK at all before this session (only .NET 6
  runtime) — installed .NET 8 SDK via `winget install Microsoft.DotNet.SDK.8`
  earlier in this session (user confirmed) so `dotnet build`/`dotnet run`
  actually work here; not itself a code change, noted since it's what made
  building/running/verifying this entry possible.

### 13. Fix: simulator mode's slots stayed empty despite visible traffic
**2026-08-30, 01:44 CDT**

**Prompt:** "simulator mode isn't working. When there is clear transactions
with identified protomods in the traffic the three slots remain empty."

**Purpose:** Reproduced live (this session drove the real running app via
Windows UI Automation — `System.Windows.Automation`, finding controls by
name and invoking `TogglePattern`/`InvokePattern` — rather than guessing from
code alone) to find the actual mechanism: `MockSerialService.Connect()`
starts streaming Accel+Temp/Electronic Load telemetry immediately, before
presence is ever requested, so the Traffic Log fills with frames clearly
labeled `AccelTemp`/`ElectronicLoad` right away — which reads as "these
modules are identified." But nothing populates the slot panels until
`PresenceRequest`/`PresenceReport` actually round-trips, which only happened
on a separate, easy-to-miss "Identify slots" button click. Clicking that
button did work correctly when tested directly — this was a discoverability/
sequencing gap, not a broken presence-detection path.

**Changes:**
- `ViewModels/MainViewModel.cs` — `Connect()` now calls
  `_dispatcher.RequestPresence()` automatically right after a successful
  connect (real port or simulator), instead of requiring a separate manual
  "Identify slots" click before slots populate. The button remains, for a
  manual re-query (e.g. after a hot-swap while already connected) — this
  only removes the *required* extra step on initial connect.
- Verified the fix the same way the bug was reproduced: drove the rebuilt
  app via UI Automation, toggled Simulator mode, clicked only Connect (no
  Identify-slots click), and confirmed all three panels populated
  ("3 ProtoMod(s) detected") from that single action.

### 14. Firmware implements unsolicited PresenceReport, BlinkyLed command, error codes
**2026-08-30, 01:54 CDT**

**Prompt:** none — the firmware session reported back unprompted on the three
proposals from its previous message (unsolicited PresenceReport on hot-swap,
BlinkyLed Command payload, error-code semantics), and asked two follow-up
questions.

**Purpose:** Record what firmware built, and answer the two open questions by
actually checking what the current C# code assumes rather than guessing —
both answers turned out to be grounded in real fragility/behavior already in
this codebase, not arbitrary preference.

**Changes:**
- No code changes on this side yet (pending firmware's response). Confirmed
  cross-session:
  - Firmware's blinky pattern-stepper now stores rate as milliseconds-per-step
    internally (previously steps-per-second) — matches the wire value
    `BlinkyLedViewModel` already sends with zero conversion needed either
    direction. Default 125ms (was 8 steps/sec).
  - Firmware resolved an ambiguity I hadn't raised: `ProtoModId` identifies a
    board *type*, not a specific physical slot, so a `Command` addressed to
    `BlinkyLed` is ambiguous if two slots both hold that type. Firmware made
    it apply to all matching slots (broadcast-by-type). Also fixed a
    pre-existing firmware bug found along the way: blinky pattern state was
    accidentally shared/global rather than per-slot (two Blinky boards would
    have blinked in lockstep).
  - Answered firmware's two open questions: (1) told them to keep SetState's
    ack echoing `payload[0]` = current on/off state (not make it empty) since
    `BlinkyLedViewModel.OnFrameReceived` has no request/response correlation
    and blindly reads *any* Response's `payload[0]` as on/off state — but
    SetBlinkRate's ack must stay empty, since a nonempty payload there would
    get misread the same way and corrupt the toggle indicator. (2)
    Recommended adding the "not implemented for this ProtoMod type" error
    code they were on the fence about, since `ElectronicLoadViewModel`'s
    "Apply Current Limit" button already sends a real Command today (not
    hypothetical) — a silent no-op there would repeat the exact "looks
    broken, actually just silent" confusion from entry 13's simulator bug.
- Still compile/link-verified only on firmware's side, no hardware run yet.

### 15. Firmware closes out the Response-ack and not-implemented-error follow-ups
**2026-08-30, 01:55 CDT**

**Prompt:** none — firmware session confirmed both items from entry 14 are
done and build/link-verified, then indicated it's wrapping this coordination
round (will reach out again once real hardware testing starts).

**Purpose:** Record that both open items are resolved so `CLAUDE.md` doesn't
keep describing them as pending.

**Changes:**
- No code changes on this side. Confirmed cross-session: BlinkyLed SetState's
  Response now echoes `payload[0]` = resulting on/off state (not empty);
  SetBlinkRate's Response stays empty, unchanged. Firmware added
  `PROTOCOL_ERR_NOT_IMPLEMENTED` (`0x04`) in `protocol.h`, sent for any
  Command addressed to AccelTemp or ElectronicLoad (neither has firmware-side
  handling yet) — covers `ElectronicLoadViewModel`'s SetCurrentLimitMa case.
- `CLAUDE.md` — updated the wire protocol section to reflect both as
  confirmed built rather than "agreed but not yet confirmed."
- Still compile/link-verified only, no hardware run yet — that caveat hasn't
  changed and won't until firmware session reports back on an actual flash+
  test.

### 16. BlinkyLed gets Pattern/Direction/Manual-LEDs controls, Response becomes a full-state snapshot
**2026-08-30, 02:06 CDT**

**Prompt:** relayed from the firmware session, on the user's instruction: add
more controllable BlinkyLed features firmware-side and update the PC app to
match. Three new commands plus a breaking change to how BlinkyLed's Response
payload works.

**Purpose:** Firmware added SetPattern/SetDirection/SetManualLeds, and
switched every BlinkyLed Command's Response from a per-command ad-hoc payload
to one fixed 7-byte full-state snapshot (regardless of which of the 5
sub-commands triggered it) — this is exactly the fix for the correlation
fragility flagged in entry 14, generalized: now there's one parse path
instead of a per-command special case, and it also gives real hardware state
feedback (device-authoritative, not locally-guessed) as more controls get
added.

**Changes:**
- `Models/BlinkyLedState.cs` (new) — `BlinkyLedMode` (Animated=0/Manual=1) and
  `BlinkyLedPattern` (Bounce=0/Chase=1/All=2/Random=3) enums, matching the
  wire values firmware defined exactly.
- `ViewModels/BlinkyLedViewModel.cs` — rewritten around the 7-byte snapshot:
  `payload[0]`=enabled, `[1]`=mode, `[2]`=pattern, `[3]`=reverse,
  `[4..5]`=period_ms (uint16 LE), `[6]`=manual LED mask (bits 0-3). Added
  `Mode`, `Pattern`, `Reverse`, `Led0On..Led3On` properties, all populated
  only from the device echo (no local optimism — `Toggle()` no longer flips
  `IsOn` itself, it just sends the command and waits for the response).
  Property-changed partial methods (`OnPatternChanged`, `OnReverseChanged`,
  the four `OnLedNOnChanged`) send the corresponding Command automatically,
  same pattern `MainViewModel.OnSimulatorModeChanged` already uses elsewhere
  in this codebase. Added an `_applyingDeviceState` re-entrancy guard so
  writing the incoming snapshot back onto these same properties doesn't
  bounce a command right back at the device for state it just reported.
- `Views/BlinkyLedPanel.xaml` — added a Pattern `ComboBox` (bound to a
  `Patterns` array on the view model), a "Reverse direction" `CheckBox`, a
  read-only "Mode:" readout, and four LED `CheckBox`es. The manual-LED
  checkboxes stay enabled regardless of Mode, since sending SetManualLeds at
  all is how the device switches into Manual mode - there's no separate
  "enter manual mode" step to gate on.
- `Services/MockSerialService.cs` — reimplemented BlinkyLed's simulated
  behavior to match: tracks the same 6 pieces of state internally and
  replies to all 5 sub-commands with the same 7-byte snapshot shape, so
  Simulator mode keeps working end-to-end rather than silently falling back
  to the old per-command format (which would have reintroduced exactly the
  "simulator mode isn't working" confusion from entry 13, this time for
  BlinkyLed instead of presence).
- Deliberately kept scope pragmatic rather than building a generic/dynamic
  command-UI framework: each new capability is one bound control plus one
  partial-changed handler sending the matching command, same shape as the
  existing Toggle/ApplyBlinkRate pair. Extending BlinkyLed further (or
  adding a 6th ProtoMod feature) follows the same repeatable pattern without
  needing a redesign, which covers the "flexible enough to extend" ask
  without a premature abstraction.
- Verified live: rebuilt, launched, drove it via Windows UI Automation
  (`System.Windows.Automation`) — connected in Simulator mode (auto
  presence-request from entry 13's fix populated all three panels),
  clicked Toggle and confirmed Mode read "Animated" from the device echo,
  toggled the LED0 checkbox and confirmed Mode switched to "Manual",
  then changed the Pattern selection (via keyboard, since the custom
  `ComboBox`/`ComboBoxItem` `ControlTemplate` added for the dark theme in
  entry 12 doesn't expose `SelectionItemPattern` on its items for UI
  Automation clients — a testing-tool quirk, not a functional bug; real
  mouse/keyboard interaction works normally) and confirmed Mode reverted to
  "Animated" as specified.

### 17. Visual LED indicators that replicate board-level animation
**2026-08-30, 02:13 CDT**

**Prompt:** "show the four LEDs if BlinkyLED is a chosen slot. Replicate
whatever behavior should be occuring at the board level."

**Purpose:** The 4 manual-LED controls added in entry 16 were plain
checkboxes with no visual "this is what the board's LEDs look like right
now" feedback, and Animated-mode patterns (Bounce/Chase/All/Random) had no
representation in the UI at all - the protocol only reports a state
*snapshot* per command, it doesn't stream a frame per animation step (same
as a real board wouldn't report over serial every time its own LED-stepper
timer fires), so nothing was locally reconstructing what the board should be
visibly doing between snapshots.

**Changes:**
- `ViewModels/BlinkyLedViewModel.cs` — added a local `DispatcherTimer` that
  runs only while `Mode == Animated` and `IsOn`, ticking at the last known
  `BlinkRateMs` and computing which LED(s) should be lit for the current
  `Pattern`/`Reverse` each step (Bounce: ping-pongs across the 4 positions on
  a period-6 cycle; Chase: rotates through 0-3 in one direction; All: all
  four blink together; Random: one random LED per tick). `UpdateAnimationState()`
  (called after every device state snapshot, so any of enabled/mode/pattern/
  reverse/rate changing takes effect immediately) stops the timer and holds
  all 4 LEDs dark when disabled, holds them at the device-echoed manual mask
  when in Manual mode, and (re)starts it otherwise. Added an
  `_animatingLocally` guard, same shape as the existing `_applyingDeviceState`
  one, so the timer's own writes to `Led0On..Led3On` don't loop back into
  `SendManualLeds()` and spam the device with commands for motion it's
  already producing on its own.
- `ViewModels/ModulePanelViewModelBase.cs` — made `Detach()` `virtual` (was
  sealed to the base implementation) so `BlinkyLedViewModel` can stop its
  timer when the slot is vacated; without this a removed/hot-swapped Blinky
  panel's timer would keep ticking indefinitely with nothing displaying it.
- `Views/BlinkyLedPanel.xaml` — replaced the 4 plain `CheckBox`es with round
  `ToggleButton`s styled (via a `ControlTemplate` scoped to this file's own
  `UserControl.Resources`, so it can't collide with the unrelated internal
  `ToggleButton` inside `App.xaml`'s `ComboBox` template) as actual LED
  indicators: dark/`TextSecondaryBrush` when off, lit/`AccentGreenBrush` when
  on. They remain the manual-control click target (clicking one still sends
  `SetManualLeds`, unchanged) so one set of 4 controls serves as both display
  and input, matching how a real board's LEDs work.
- Verified live via the same UI Automation approach used earlier this
  session (not just build-succeeded): connected in Simulator mode, enabled
  the module, set a 150ms rate, and sampled all 4 LED toggle states every
  150ms across several ticks — confirmed Bounce visibly shifts a single lit
  LED back and forth across positions, and switching the Pattern selector to
  "All" made all 4 toggle on/off in sync. Then clicked one LED directly and
  confirmed Mode switched to "Manual" and all 4 states held static
  (unchanging) across further samples, rather than continuing to animate.

### 18. Firmware confirms LED animation sequence is an exact match
**2026-08-30, 02:15 CDT**

**Prompt:** none — firmware session checked the open question from entry 17's
FYI against the real `protomod.c` source rather than leaving it unconfirmed.

**Purpose:** Record that the local animation reconstruction isn't just a
reasonable guess.

**Changes:**
- No code changes. Confirmed cross-session: firmware's actual sequences are
  Bounce `{0,1,2,3,2,1}` (period 6, ping-pong) and Chase `{0,1,2,3}` (period
  4, plain wrap) — exactly what `BlinkyLedViewModel`'s `BounceIndex()`/
  `ChaseIndex()` already assumed. Only expected divergence from real hardware
  is timing jitter (blocking I2C calls elsewhere in firmware's main loop
  occasionally delaying a step by a few ms), not the sequence itself.
- `CLAUDE.md` — updated to say "confirmed exact match" instead of leaving it
  as an open/unverified assumption.

### 19. Flag ProtoModId's byte-width ceiling as a blocker for the 1,000+-ProtoMod goal
**2026-08-30, 02:22 CDT**

**Prompt:** "both you and the other agent need to be fully aware that the
structure of both programs should prioritize modularity as there could be
1000+ different protomods installed on the hardware side. let the other
agent know this and both of you need to act to prioritize this moving
forward."

**Purpose:** Make this a standing, documented priority rather than something
that could get silently deprioritized in a future session, and find whatever
concrete conflicts already exist with it rather than just writing a vague
principle down. The rest of the architecture (dynamic slot population,
`ModuleCatalog`'s one-line-per-type registration, graceful degradation for
an unrecognized type) was already built with this in mind — the one real,
provable conflict is `ProtoModId`'s wire width.

**Changes:**
- `Models/ProtoModId.cs` — added a doc comment flagging that `ProtoModId` is
  a single byte on the wire (`ProtocolFrame.Encode()`/`ProtocolFrameReader`
  both treat it as one byte), so only ~253 values are usable after reserved
  IDs (`None`/`Core`/`Broadcast`) — nowhere near 1,000+. Not fixed yet since
  it's a wire-format change requiring firmware agreement first.
- `CLAUDE.md` — added a "Platform decisions" entry recording the
  1,000+-ProtoMod modularity priority as user-directed and standing, plus
  the `ProtoModId` conflict and proposed fix (widen to 2 bytes, pending
  firmware agreement).
- Messaged the firmware session with this finding and a concrete proposal
  (widen `ProtoModId` to a 2-byte little-endian field) rather than a vague
  "keep modularity in mind" — asked for their read before either side
  implements anything, since it's a breaking wire-format change and both
  sides have already built and validated parsers around the 1-byte version.
  No code changes made to the wire format yet on either side.

### 20. Widen ProtoModId to 2 bytes on the PC side, matching firmware's implementation
**2026-08-30, 02:30 CDT**

**Prompt:** none — firmware session confirmed the 1,000+ priority directly
with the user, agreed with the flat-16-bit-ID proposal from entry 19, and
implemented it firmware-side; this entry is the matching PC-side
implementation firmware asked for.

**Purpose:** Match firmware's now-implemented wire format bit-for-bit before
either side is disconnected/out of sync - the frame header grew from 3 to 4
bytes and `PresenceReport`'s payload shape changed from 1 byte per ID to 2.

**Changes:**
- `Models/ProtoModId.cs` — widened from `byte` to `ushort`. Kept
  BlinkyLed/AccelTemp/ElectronicLoad at their existing low values (`0x0001`-
  `0x0003`) per firmware's lead; moved `Core` `0xF0`→`0xFFF0` and `Broadcast`
  `0xFF`→`0xFFFF` to the top of the range, matching firmware's convention of
  keeping reserved/system addresses visually distinct from catalog IDs.
- `Models/ProtocolFrame.cs` — `Encode()`/`ComputeChecksum()` now write/XOR 2
  ID bytes (little-endian) instead of 1; buffer size 6→7 plus payload.
  `ProtocolFrameReader`'s header state now collects 4 bytes instead of 3
  before transitioning to payload, and reconstructs `_moduleId` as a
  little-endian `ushort` from the first two.
- `ViewModels/MainViewModel.cs` — `PresenceReport` parsing now reads 2-byte
  little-endian pairs from the payload instead of one `ProtoModId` per byte.
- `Services/MockSerialService.cs` — `PresenceReport` reply now emits 2 bytes
  per `ProtoModId` instead of 1, so Simulator mode keeps working against the
  new format.
- `ViewModels/UnknownModuleViewModel.cs` — fixed a display bug this change
  would otherwise have introduced: its hex readout cast to `(byte)`, which
  would've silently truncated any ID above `0xFF` to its low byte and shown
  the wrong value. Now casts to `(ushort)` and formats as 4 hex digits.
- `README.md` — updated the frame diagram, ProtoModId/checksum description,
  and the two other mentions of "ProtoModId bytes" to reflect the 2-byte
  little-endian format.
- Verified live via UI Automation (not just build-succeeded): connected in
  Simulator mode, confirmed "3 ProtoMod(s) detected" and all three panels
  populated via the new 4-byte-header `PresenceReport`, then clicked Blinky's
  Toggle and confirmed Mode updated to "Animated" from the device's `Response`
  - proving both the presence path and the Command/Response path encode,
  checksum, and decode correctly under the new format. No checksum/framing
  errors appeared anywhere in the traffic log during testing.
- Reported back to the firmware session that the PC side now matches.

### 21. Live trend-line charts for Accel+Temp and Electronic Load, decoupled from firmware
**2026-08-30, 12:50 CDT**

**Prompt:** "Use Simulator mode to design the Accel+Temp and Electronic Load
UX now, decoupled from firmware. The placeholder payload formats are
blocking real data parsing, but they're not blocking UI design. Simulator
mode already fakes telemetry — extend it to fake plausible Accel+Temp and
Electronic Load values, then build the actual charts/interaction (OxyPlot or
LiveCharts2, live trend lines) against that fake data now. When firmware
defines the real payload later, it's a parsing swap, not a UI build."

**Purpose:** Both telemetry panels only showed a single live numeric
readout each - no sense of a trend over time, and no reason that had to wait
on firmware's still-undefined payload formats, since Simulator mode already
generates plausible telemetry values today.

**Changes:**
- Added `OxyPlot.Wpf` (2.2.0) as a NuGet dependency.
- `Charts/ChartTheme.cs` (new) — shared helper building dark-themed
  `PlotModel`s matching the ProtoVerse palette (OxyPlot's `PlotModel` is a
  plain C# object, not a WPF `DependencyObject`, so it can't bind to a
  `StaticResource` brush - hex values are duplicated here the same way
  `BoolToBrushConverter`/`SlotStateToBrushConverter` already do, per
  `CLAUDE.md`'s "Branded dark theme" note). Provides a single-Y-axis model
  factory, a dual-Y-axis factory (for series on very different scales, e.g.
  volts vs. milliamps), a themed line-series helper, an opt-in legend helper
  (only enabled for multi-series charts - a single-series chart's axis title
  already says what it is), and a rolling-window point-append helper that
  trims the oldest point once a chart exceeds its history cap, so a chart
  left running for a long session doesn't grow unbounded.
- `ViewModels/AccelTempViewModel.cs` — added `TempPlotModel` (1 line) and
  `AccelPlotModel` (X/Y/Z, 3 lines + legend), ~120-point (~2 min at ~1
  sample/sec) rolling history on an elapsed-seconds X axis. `AppendToCharts()`
  is a separate step from the byte-parsing in `OnFrameReceived` - it just
  plots whatever `TemperatureC`/`AccelXg/Yg/Zg` currently hold, so swapping
  in the real payload format later only touches the parsing, not this.
- `ViewModels/ElectronicLoadViewModel.cs` — added a dual-axis `PlotModel`
  (Voltage on the left axis, Current on the right - sharing one axis would
  flatten one of them given how different their scales are), same
  decoupled-`AppendToChart()` pattern.
- `Views/AccelTempPanel.xaml` / `Views/ElectronicLoadPanel.xaml` — added
  `oxy:PlotView` controls below the existing numeric readouts.
- `Views/MainWindow.xaml` — wrapped the panel `ItemsControl` in a
  `ScrollViewer` (`VerticalScrollBarVisibility="Auto"`), since telemetry
  panels are now tall enough with charts to exceed the window on smaller
  screens; the Traffic Log expander stays outside the scroll region.
- Did not change `MockSerialService`'s telemetry generation - it already
  produces plausible values (sine/cosine-based temp/accel, a load response
  curve for voltage/current) at a real streaming cadence, which is exactly
  what the charts needed; no changes were necessary there.
- Verified live via UI Automation (not just build-succeeded): connected in
  Simulator mode, confirmed both panels' charts rendered with correct axis
  titles/legends, then waited ~5s for telemetry to stream and confirmed the
  numeric readouts *and* every chart axis auto-ranged to real values (e.g.
  Temperature axis showing 22-24°C instead of a 0-100 placeholder range,
  Electronic Load's dual axes showing ~4.8V and ~100mA independently) -
  proving the whole live-charting path actually works end-to-end against
  Simulator mode's fake telemetry, not just that it compiles.

### 22. Replace the accel X/Y/Z line chart with a bubble-level XY plot + Z fill gauge
**2026-08-30, 12:55 CDT**

**Prompt:** "I want the IMU graph to be two parts. A 2D plot that plots x and
y (when both x and y are near 0 then the dot is in the center). Z should be
a vertical bar type appearance with -1g being the middle point - increased z
accel would fill the bar. decreased (less than minus 1 g) would cause the
bar to go lower."

**Purpose:** The 3-line X/Y/Z trend chart from entry 21 didn't read
intuitively as "which way is the board tilted right now" - a 2D position and
a fill gauge map much more directly onto what those two axis pairs
physically mean (X/Y = tilt off level, Z = how much the board is being
pushed against/away from gravity).

**Changes:**
- `Charts/ChartTheme.cs` — added `CreateXyPlotModel(range)` (fixed
  `[-range, range]` axes on both X and Y, not auto-ranging, so the origin is
  always the exact visual center; a distinct crosshair gridline through zero
  on each axis via `ExtraGridlines`), `AddXyMarker`/`SetXyPoint` (a single
  `ScatterSeries` point, replaced wholesale each update rather than
  accumulated like a line series - a level indicator only has one current
  position). Added `CreateVerticalGaugeModel(title, minY, maxY)` (a value-only
  Y axis, X axis hidden since there's nothing to categorize) and
  `AddVerticalGaugeBar`/`SetGaugeValue`, built on OxyPlot's `LinearBarSeries`
  with its `BaseValue` set to the gauge's center - that's what makes the bar
  fill *from* that baseline rather than from zero, growing up above it and
  down below it.
- `ViewModels/AccelTempViewModel.cs` — replaced `AccelPlotModel` (the 3-line
  chart) with `XyPlotModel` (range ±1.5g, comfortably framing the
  simulator's ±1g sine/cosine values) and `ZGaugeModel` (centered on -1g per
  spec, ±1g half-range, so the gauge's fixed axis is exactly -2g to 0g).
  `AppendToCharts()` now calls `SetXyPoint`/`SetGaugeValue` instead of
  appending to a rolling history for these two - matches what they actually
  are (an instantaneous reading, not a trend).
- `Views/AccelTempPanel.xaml` — 3-column layout now: Temperature trend line,
  XY bubble-level plot, Z gauge (relative widths 2:1.3:0.7, reflecting that
  the gauge only ever needs to be narrow).
- Verified live via UI Automation: connected in Simulator mode, waited for
  telemetry, and confirmed the X/Y axes both span exactly [-1.5, 1.5]
  (rendering major ticks at -1/0/1) and the Z axis spans exactly [-2, 0]
  with -1 sitting at the middle tick, matching the spec precisely - not just
  that the panel renders without error.

### 23. Fix clipped button text; add a Help tab (revision notes + supported ProtoMods)
**2026-08-30, 13:13 CDT**

**Prompt:** "some of the text on the top bars is not fully visible - i.e.
missing letters. Also add a help tab that documents single sentance
revisions and that sort of thing like a change log. Also indicates currently
supported protomods."

**Purpose:** Two separate items - a real rendering bug from entry 12's dark
theme, and a new end-user-facing Help surface distinct from CHANGELOG.md
(which is aimed at whoever develops this app, not whoever uses it).

**Changes - clipped text:**
- Root cause: entry 12's branded `Button` style added `FontWeight="SemiBold"`
  and `Padding="12,6"` inside a custom `ControlTemplate`, but several buttons
  still had their original fixed pixel `Width` values chosen before that
  style existed (e.g. `Disconnect` at `Width="80"` only leaves 56px for text
  after padding) - content wider than the fixed width gets silently clipped
  during Arrange rather than the button growing to fit, so trailing letters
  disappeared. Confirmed live: `Disconnect`'s rendered width was exactly
  `80.0` before the fix and grew to `84.0` after, matching the diagnosis.
- Changed every fixed-width `Button` (`Refresh`, `Connect`, `Disconnect`,
  `Identify slots` in `MainWindow.xaml`; `Toggle`/blink-rate `Apply` in
  `BlinkyLedPanel.xaml`; `Apply` in `ElectronicLoadPanel.xaml`; `Clear` in
  the traffic log) from `Width` to `MinWidth` - buttons now grow to fit
  their content instead of clipping it, while still lining up consistently
  when the old fixed value already had enough room.

**Changes - Help tab:**
- `ViewModels/ModuleCatalog.cs` — restructured its factory dictionary into
  `(DisplayName, Factory)` registrations and added `SupportedModules`, so
  "what's supported" has exactly one source of truth shared by both
  `TryCreate` and the new Help tab - it can't drift out of sync with what's
  actually registered.
- `ViewModels/HelpViewModel.cs` (new) — `RevisionNotes` (a hand-maintained,
  newest-first list of short, single-sentence, end-user-facing summaries -
  deliberately not a 1:1 mirror of every CHANGELOG.md entry, since routine
  internal/refactor entries don't mean anything to someone just using the
  app) and `SupportedModules` (read straight from `ModuleCatalog`).
- `Views/HelpPanel.xaml`/`.xaml.cs` (new) — lists both, matching the app's
  existing dark theme.
- `Views/MainWindow.xaml` — the bottom Expander (previously "Traffic Log"
  only) now hosts a `TabControl` with "Traffic Log" (existing content,
  unchanged) and "Help" tabs, still collapsed by default same as before.
- `App.xaml` — added dark-themed `TabControl`/`TabItem` styles (unselected
  tabs muted, selected tab visually fused to the content panel below it).
  Hit and fixed two real bugs building this, both worth remembering for any
  future custom `TabControl` template: (1) the content-hosting row was
  initially `Height="*"` - inside an `Expander` with no bounded height above
  it, a star row measures to zero, so nothing rendered at all; changed to
  `Auto` so it sizes to the selected tab's own content instead. (2) the
  usual `ContentPresenter ContentSource="SelectedContent"` shortcut
  (Microsoft's own default `TabControl` template uses exactly this) did not
  render any content in this app despite no XAML errors and a correctly
  `Expanded`/tab-`Selected` state - switched to an explicit
  `Content="{TemplateBinding SelectedContent}"` /
  `ContentTemplate="{TemplateBinding SelectedContentTemplate}"` binding
  (plus `x:Name="PART_SelectedContentHost"`, matching the conventional part
  name), which resolved it immediately. Root cause of that second one wasn't
  tracked down further since the explicit binding is equally correct and
  unambiguous either way.
- Verified live via UI Automation at each step (not just build-succeeded) -
  this was essential here, since the first two fix attempts (the `Height`
  change alone, and before that nothing) both looked plausible but the tab
  content silently failed to render both times; only re-checking after each
  change caught it. Final state confirmed: `Disconnect` button no longer
  clipped, Traffic Log tab shows its `DataGrid` and `Clear` button, Help tab
  shows all 3 supported ProtoMods (Blinky LED, Accelerometer + Temperature,
  Electronic Load) and all 9 revision-note lines.

### 24. Fix: a module in any slot but the first always rendered in the first panel
**2026-08-30, 13:38 CDT**

**Prompt:** relayed from the firmware session (a new session,
`workspace-1-19-0-0d` - the previous firmware session that coordinated
entries 9-22 had ended by this point). The user had reported a real
hardware/UI mismatch: a Blinky board physically in ProtoCore's middle slot
was rendering in this app's *first* panel.

**Purpose:** Root cause, found by the firmware session and verified against
this app's actual code before touching anything: `PresenceReport`'s payload
only ever listed IDs for *occupied* slots, with no slot index anywhere in
the payload - so "BlinkyLed in slot 0" and "BlinkyLed in slot 1 (0/2 empty)"
produced an identical single-entry payload on the wire, indistinguishable
from each other. `MainViewModel.OnFrameReceived` then just appended arrivals
in list order starting at `Panels[0]`, so a module always landed in the
first panel regardless of which physical slot it actually occupied.

**Changes:**
- Firmware (separate `Protocore` codebase/session, already done before this
  entry): `PresenceReport`'s payload is now a **fixed** `SlotCount*2`-byte
  array - exactly one `ProtoModId` per physical slot, always in slot order,
  with an empty slot reporting `ProtoModId.None` instead of being omitted.
- `ViewModels/MainViewModel.cs` — `OnFrameReceived` rewritten to match:
  reads exactly `SlotCount` slots directly off the payload by index and
  assigns each straight to `Panels[slot]` (`None` → `EmptySlotViewModel`,
  otherwise `ModuleCatalog.TryCreate` or `UnknownModuleViewModel`, same as
  before). Removed the old `.Distinct().Take(SlotCount)` filtering entirely
  - `None` can legitimately repeat across multiple empty slots now, and
  filtering it out is exactly the bug. A payload that isn't exactly
  `SlotCount*2` bytes is now rejected with a status message rather than
  partially interpreted, since the format is fixed-size, not "at least."
- Verified live via UI Automation, not just build-succeeded, and not just
  the easy "everything populated" case: first a regression check (all 3
  slots occupied, unchanged `MockSerialService` default) confirmed panels
  still render in the correct order. Then, since that alone wouldn't catch
  this specific bug, temporarily reconfigured `MockSerialService`'s
  `InstalledMods` to the *exact* reported scenario - Blinky in slot 1 only,
  slots 0 and 2 empty - rebuilt, and confirmed Blinky now renders as the
  **second** panel with "No module detected" correctly above and below it
  (previously would have rendered first). Reverted the simulator back to
  its normal full-population default afterward and rebuilt again to confirm
  that still works too.
- Reported back to the firmware session that the PC side now matches and is
  verified against the exact scenario the user hit.

### 25. Diagnose: Refresh can't see the board's COM port after a reflash
**2026-08-30, 13:49 CDT**

**Prompt:** "If I reflash the HW with updated FW it seems that the app can
no longer see that com port after hitting refresh"

**Purpose:** First real-hardware session (everything prior was Simulator
mode only). Diagnosed against the actual machine state rather than guessing
- checked what Windows itself currently sees before assuming the app's
Refresh logic was broken.

**Changes:**
- No code changes - `SerialService.GetAvailablePorts()`/Refresh already just
  calls `SerialPort.GetPortNames()`, and that was confirmed to be accurately
  reporting what Windows sees (only COM1, the built-in port). Not an app bug.
- Diagnosed via `Get-WinEvent`/`Get-PnpDevice`: Windows's PnP event log
  showed `USB\VID_0483&PID_5740\... requires further installation` at the
  time of the reflash (`0483:5740` = STM32's standard CDC-ACM Virtual COM
  Port ID) - so the board's USB CDC interface *did* start enumerating and
  Windows correctly identified it, but driver installation never completed,
  leaving a phantom (`CM_PROB_PHANTOM`) device node rather than a live COM
  port. Likely cause: an ST-Link reflash only resets the MCU, it doesn't
  force a real USB disconnect/reconnect, which Windows needs to retry driver
  install cleanly.
- Told the user to unplug/replug the USB cable, then check Device Manager's
  Ports (COM & LPT) for a warning icon on the STM32 Virtual COM Port entry
  and update the driver if present.
- `CLAUDE.md` — added this as a gotcha, including the diagnostic commands
  used, so a future hardware bring-up session doesn't have to rediscover it.
- Messaged the firmware session with the finding (FYI, not a blocker) in
  case it's actually a CDC ACM descriptor issue rather than a one-time
  Windows driver prompt, since this is the first time their USB stack has
  been tested against a real Windows host.

### 26. Mirror firmware's ProtoMod EEPROM identity catalog; flag the AccelTemp dispute
**2026-08-30, 14:14 CDT**

**Prompt:** the user first reported (unprompted, mid-session) "board two is
not IMU and Temp" - a real-hardware contradiction of this app's `AccelTemp`
labeling for `0x0002`. Separately, the user shared
`ProtoMod_Programmer/ProtoMod_Programmer.ino` (a third codebase - an Arduino
wizard that programs each ProtoMod's onboard AT24C02 EEPROM with an 11-byte
identity record: circuit code, PCB rev, PCBA rev, WW/YY date, 2 misc bytes).
The firmware session then relayed that it had consolidated the same identity
data firmware-side into `Core/{Inc,Src}/protomod_catalog.{h,c}` and asked
this app to mirror it.

**Purpose:** Firmware's new catalog is worth mirroring (matches this app's
existing `ProtoModId` mirrors `protocol.h` pattern), but its `AccelTemp`
entry (`circuit_code "F02"`) is *exactly* the assumption the user's "board
two" report contradicts - the firmware session's message introducing the
catalog didn't independently re-verify that entry, it just restated the same
slot-position-based guess in a new file. Mirroring it faithfully without
flagging that would have made a disputed, unconfirmed value look settled.

**Changes:**
- `Models/ProtoModBoardCatalog.cs` (new) — `ProtoModBoardIdentity` record +
  the 3 entries firmware provided (BlinkyLed/F01, AccelTemp/F02,
  ElectronicLoad/E05), doc-commented the same way `ProtoModId.cs` is
  ("mirror `protomod_catalog.c`, update both sides together") **plus** a
  prominent unresolved-dispute note on the class itself: don't treat
  `AccelTemp="F02"` as settled, and the real fix is a non-destructive I2C
  read of whatever's actually in that slot (ProtoCore's own EEPROM read) -
  not the Arduino programmer sketch, which has no read-only mode (it always
  ends in a write, and even its pre-write "planned payload" hexdump shows
  the newly-typed values, not what's currently on the chip, so it can't
  answer "what's there now" without risking overwriting it).
- `ViewModels/HelpViewModel.cs` — `SupportedModuleInfo` gained a nullable
  `CircuitCode`, joined from `ProtoModBoardCatalog.Entries` by `Id` (falls
  back to `null`/displays `?` rather than throwing if a future type is in
  `ModuleCatalog` but not yet cataloged). Added a matching `RevisionNotes`
  line.
- `Views/HelpPanel.xaml` — each supported-module row now shows its circuit
  code (e.g. "Blinky LED — circuit code F01 (BlinkyLed)"), plus a short
  explanatory footnote. Deliberately did *not* put the AccelTemp-specific
  dispute into this end-user-facing UI text, since it's a transient,
  soon-to-be-resolved engineering concern - that context lives in
  `ProtoModBoardCatalog.cs`'s doc comment and here instead, where it won't
  go stale in front of a user once resolved.
- Verified live via UI Automation: rebuilt, launched, expanded the Help tab,
  and confirmed all three rows render their correct circuit codes.
- Replied to the firmware session: confirmed the mirror is in, but pushed
  back on treating the catalog as resolving the AccelTemp question - asked
  them to have ProtoCore do an actual non-destructive I2C read of the real
  board in that slot and report the circuit code it actually finds, rather
  than the PC and firmware sides just agreeing with each other's copy of
  the same unverified assumption.

### 27. Correct AccelTemp's circuit code to E03, confirmed against the actual product manuals
**2026-08-30, 14:19 CDT**

**Prompt:** none - the firmware session followed up unprompted, reporting it
had settled the entry-26 dispute by finding and reading the project's own
module manuals (`Documents/.../PROTOVERSE/Manuals/`), which neither session
had pulled up before.

**Purpose:** Apply the correction, but only after independently verifying it
myself rather than trusting the relay - same standard as every other
cross-session claim this project has acted on. The firmware session's quotes
turned out to be accurate.

**Changes:**
- Independently extracted and read the actual text of both `.docx` files
  (they're binary, not directly readable - unzipped as the OOXML archives
  they are and pulled `word/document.xml`) rather than trusting the quoted
  excerpts. Confirmed verbatim: `E03_Sensors1.docx` - "Module Name: Sensors
  1 (E03) ... This ProtoMod introduces two types of sensors: STM LIS3DH
  accelerometer ... Analog Devices TMP36 temperature sensor" - is
  `AccelTemp`, precisely. `F02_Simple_LED.docx` - "Module Name: Simple LED
  (F02) ... two LED paths (one red, one green) plus two switches per path"
  - a resistor/voltage demo board, zero sensors, not `AccelTemp`.
- `Models/ProtoModBoardCatalog.cs` — `AccelTemp`'s circuit code corrected
  `"F02"` → `"E03"`. Rewrote the class doc comment from "unresolved
  dispute" to "resolved (doc-confirmed), hardware-read still pending" -
  kept the same honesty standard as the rest of this project: this is
  documentation-confirmed, not yet verified against a live EEPROM read of
  real Sensors-1 hardware.
- `ViewModels/HelpViewModel.cs` — added a `RevisionNotes` line about the
  correction, since users would have seen the wrong code before this.
- `CLAUDE.md` — updated the wire protocol section's AccelTemp entry from
  "open question, not resolved" to "resolved (documentation-confirmed),
  hardware-read still pending," with the manual citations.
- Verified live via UI Automation: rebuilt, launched, expanded the Help tab,
  confirmed AccelTemp now shows circuit code `E03` (not `F02`) and the new
  revision note appears.

### 28. Make hot-swap presence-report handling fault-isolated
**2026-08-30, 14:46 CDT**

**Prompt:** "what about making the app safe against hot swapping modules on
the board? I don't want that to result in a bricked app" (after being asked
to set aesthetics aside and focus on functionality).

**Purpose:** `App.xaml.cs` already has a global `DispatcherUnhandledException`
handler (logs, shows a dialog, marks handled so the process itself survives)
- a reasonable last resort, but not a good hot-swap experience: it exists to
catch truly unexpected bugs, not to be the normal response to something as
routine as pulling a module. The real gap was underneath it -
`MainViewModel.OnFrameReceived` rebuilt `Panels` destructively (`Clear()`
then re-add in a loop with no error handling), so a single module's panel
constructor throwing mid-rebuild would leave the collection half-populated
and surface as a scary global error dialog for a routine hot-swap.

**Changes:**
- `ViewModels/MainViewModel.cs` — `OnFrameReceived` now builds the new slot
  lineup in a local list first and only swaps it into the live `Panels`
  collection once fully built, so the currently-displayed (working) panels
  are never disturbed by a failure elsewhere in the same rebuild. Each
  slot's panel construction is individually wrapped in try/catch - a
  misbehaving module type degrades just that one slot to
  `UnknownModuleViewModel` (with the specific slot/type/exception recorded
  in `StatusMessage`) instead of losing the whole report. `DetachModulePanels`
  similarly wraps each panel's `Detach()` call so one panel's cleanup
  misbehaving can't block detaching the rest or stall a hot-swap rebuild.
- Verified with genuine fault injection, not just reasoning about it:
  temporarily made `ModuleCatalog`'s `BlinkyLed` factory throw
  unconditionally, rebuilt, connected in Simulator mode, and confirmed the
  app stayed alive and responsive with no crash dialog - slot 0 showed
  "Unsupported module (BlinkyLed, 0x0001)" and `StatusMessage` read
  `3 ProtoMod(s) detected - panel error in slot 0 (BlinkyLed):
  InvalidOperationException`, while slots 1 and 2 (Accel+Temp, Electronic
  Load) populated completely normally, unaffected. Reverted the fault
  injection afterward and rebuilt/re-verified normal 3-module operation was
  fully restored.

### 29. Add BasicLed + Unknown ProtoModIds; distinguish the two "unknown" meanings
**2026-08-30, 15:24 CDT**

**Prompt:** none - the firmware session reported two compile/link-verified
changes unprompted: the "board two" mystery from entries 26-27 is now fully
closed with real hardware evidence (a raw-serial capture), not just
documentation inference.

**Purpose:** The physical board that started the whole AccelTemp/"F02"
saga was never AccelTemp at all - it's real hardware with a valid EEPROM
that simply wasn't in firmware's catalog, so it reported as `ProtoModId.None`
(indistinguishable from an empty slot) the entire time. Firmware fixed the
root cause (a genuinely missing catalog entry, not a guess-the-mapping
problem) and added a new sentinel so "present but unrecognized" and "empty"
are no longer conflated on the wire.

**Changes:**
- `Models/ProtoModId.cs` — added `BasicLed = 0x0004` (Simple LED,
  Fundamentals series, circuit code "F02" - confirmed to be exactly the
  board the manuals described, it just needed its own catalog entry instead
  of being confused with AccelTemp) and `Unknown = 0xFFE0` (a slot ProtoCore
  itself can't identify - valid EEPROM read, no catalog match - reported
  instead of `None` so it's distinguishable from an empty slot).
- `Models/ProtoModBoardCatalog.cs` — added the `BasicLed`/"F02" entry;
  updated the class doc comment to record the full resolution.
- `ViewModels/UnknownModuleViewModel.cs` — this view model covers two
  conceptually different situations that happen to share one appearance
  (both render as the orange/Unsupported status): a real, firmware-known
  `ProtoModId` this app build just has no panel for yet (routine, fix is
  "update this app" - `BasicLed` currently falls here, since firmware's
  stub has no behavior yet either), versus `ProtoModId.Unknown` (fix is
  "update firmware's catalog," a completely different situation). Previously
  both showed the identical "Unsupported module (...)" text; now
  `ProtoModId.Unknown` gets its own message: "Something's plugged in here,
  but ProtoCore doesn't recognize its EEPROM identity." This distinction is
  exactly what was missing when this same ambiguity caused real
  troubleshooting confusion earlier today.
- Verified live via UI Automation: temporarily reconfigured
  `MockSerialService`'s `InstalledMods` to report `BasicLed`, `Unknown`, and
  `ElectronicLoad`; rebuilt; confirmed all three render distinctly and
  correctly ("Unsupported module (BasicLed, 0x0004)" vs. the new Unknown
  message vs. a normal Electronic Load panel). Reverted the simulator back
  to its normal default afterward and rebuilt/re-verified.
- Not added to `ModuleCatalog.Registrations`/`SupportedModules` - `BasicLed`
  has no real panel or firmware behavior yet, so it correctly stays out of
  the Help tab's "supported" list until one exists.

### 30. Show circuit code instead of raw hex ID for an unsupported ProtoMod
**2026-08-30, 15:28 CDT**

**Prompt:** "Isntead of showing 0x0004 (which I don't know where that is
coming from) show the circuit code for the unsupported module (F01, F02,
etc. etc.)" (after being shown `ProtoMod_Programmer.ino`'s EEPROM layout
again, which is where circuit codes like "F02" actually come from - what's
printed/programmed onto the physical board).

**Purpose:** `ProtoModId`'s raw hex value (`0x0004`) is an internal wire
protocol detail with no meaning to a person looking at the app - the circuit
code is what's actually on the board and in the manuals, so it's the right
thing to show for "what is this unsupported thing."

**Changes:**
- `ViewModels/UnknownModuleViewModel.cs` — the "recognized but unsupported"
  message now looks up the circuit code from `ProtoModBoardCatalog.Entries`
  and leads with it (e.g. "Unsupported module: BasicLed (circuit code F02)")
  instead of the raw hex ID. Falls back to the hex ID only when a type is
  genuinely uncataloged on this side too (not yet mirrored into
  `ProtoModBoardCatalog.cs`) - still better than showing nothing.
  `ProtoModId.Unknown`'s separate message (entry 29) is unaffected - it
  already didn't show a hex ID.
- Verified live via UI Automation, both paths: temporarily reconfigured
  `MockSerialService` to report `BasicLed` (cataloged - confirmed it now
  shows "Unsupported module: BasicLed (circuit code F02)") and a genuinely
  uncataloged raw ID `0x1234` (confirmed the hex fallback still works:
  "Unsupported module: 4660 (0x1234)"). Reverted the simulator back to
  normal afterward and rebuilt/re-verified.

### 31. Fix: a failed real-port Connect() could surface as an unhandled exception
**2026-08-30, 16:42 CDT**

**Prompt:** the user pasted a live crash: `System.IO.IOException: A device
attached to the system is not functioning.` thrown from
`SerialPort.Open()`, with a stack trace matching `App.xaml.cs`'s crash
reporter format exactly (the same one that logs to `crash_log.txt`, copies
to clipboard, and shows a dialog) - almost certainly hit while trying to
connect to the real board's COM port.

**Purpose:** `SerialService.Connect()` called `_port.Open()` with zero
exception handling, and neither `FrameDispatcher.Connect()` nor
`MainViewModel.Connect()` caught it either - so any real, expected failure
mode (a flaky USB CDC driver, the board resetting mid-open, another program
already holding the port, a bad port name) fell all the way through to the
global unhandled-exception dialog instead of a normal "failed to connect"
status message. The global handler is a last resort for genuine bugs, not
the intended response to a routine connect failure - same principle as
entry 28's hot-swap fault isolation, just a different code path.

**Changes:**
- `Services/SerialService.cs` — `Connect()` now wraps `port.Open()` in
  try/catch: on failure, unsubscribes `DataReceived`, disposes the failed
  `SerialPort`, and rethrows, rather than leaving a half-opened port
  assigned to `_port` for a later call to trip over. `_port` is now only
  ever assigned after a successful `Open()`.
- `ViewModels/MainViewModel.cs` — `Connect()` now catches
  `IOException`/`UnauthorizedAccessException`/`InvalidOperationException`
  around `_dispatcher.Connect(...)` (same exception-type set
  `SerialService.IsDisconnectException` already uses elsewhere in this
  codebase, for consistency) and turns it into
  `StatusMessage = "Failed to connect to {port}: {reason}"` instead of
  letting it propagate.
- Verified with genuine fault injection, not just reasoning: temporarily
  added a nonexistent `"COM250"` to `AvailablePorts`, rebuilt, selected it
  with Simulator mode off, and clicked Connect. Confirmed the app stayed
  alive and responsive (no crash dialog) and `StatusMessage` read
  `Failed to connect to COM250: Could not find file 'COM250'.` — the exact
  clean failure this fix was meant to produce. Reverted the fault injection
  afterward, rebuilt, and re-confirmed normal Simulator-mode connect still
  works.

### 32. Bound SerialPort's read/write timeouts to stop a dead handle from freezing the UI
**2026-08-30, 17:04 CDT**

**Prompt:** relayed from the firmware session, diagnosing a real user report:
after fixing an earlier unplug-freeze (I2C bus recovery, firmware-side),
re-inserting a ProtoMod board now causes ProtoCore's own supply rail to sag
enough to trigger a real MCU brownout reset (`POR/PDR`, confirmed via a live
RCC_CSR capture) - a genuine hardware/connector issue, not fixable in
software on either side. But afterward, "the app becomes unresponsive,"
which *is* a software question: does this app's serial handling actually
recover from an abrupt, real (not simulated) USB disconnect+reconnect?

**Purpose:** Checked the actual code rather than guessing. Confirmed a real
bug matching the symptom exactly: `SerialService`'s `SerialPort` was
constructed with no `ReadTimeout`/`WriteTimeout` (both default to
`SerialPort.InfiniteTimeout`), and `Send()` is called **synchronously on the
UI thread** (traced through `FrameDispatcher.Send()`, invoked directly by
every button-click `RelayCommand` - Toggle, Apply, Identify Slots, etc.). If
a disconnected/reset device's OS-level handle blocks on `Write()`/`Read()`
instead of erroring promptly - plausible for the timing/abruptness of a
brownout-triggered reset, per the firmware session's question - that hang
would freeze the entire app, not just fail one command. `IsDisconnectException`
also didn't include `TimeoutException`, so even a bounded timeout alone
wouldn't have been caught and treated as a disconnect.

**Changes:**
- `Services/SerialService.cs` — `Connect()` now sets `port.ReadTimeout` and
  `port.WriteTimeout` to 1000ms (generous for a real frame - max 250 bytes
  at 115200 baud takes low tens of ms - while still recovering promptly from
  a genuinely dead port) before opening. `IsDisconnectException` now
  includes `TimeoutException` alongside `IOException`/
  `UnauthorizedAccessException`/`InvalidOperationException`, so a timed-out
  Read/Write tears the port down and raises `Disconnected` exactly like the
  other failure modes already did.
- `Services/FrameDispatcher.cs` — `Send()`'s catch filter also gained
  `TimeoutException`, so a timeout gets swallowed the same way the other
  disconnect-related exceptions already are (the transport already raised
  `Disconnected` to tell the UI; this just stops a button click from
  crashing the UI thread on top of that).
- **Verification caveat, stated plainly:** rebuilt and regression-tested
  that normal Simulator-mode connect/command flow is unaffected (it is -
  `MockSerialService` doesn't touch `SerialPort` at all, so this change
  can't be exercised through the simulator). The actual failure mode this
  fixes - a real OS-level handle hanging instead of erroring on a genuinely
  dead port - can't be honestly verified without a real disconnect/reset
  event, which needs real hardware. The firmware session offered to
  reproduce the brownout reset on demand via their ST-Link access
  specifically so this can be tested against a real instance; accepted that
  offer rather than claim this is confirmed fixed on reasoning alone.
- Two open questions from the firmware session not yet answered, both need
  real hardware to observe rather than being answerable in code: whether
  Windows keeps the same COM port number across this kind of reset or
  assigns a new one, and confirming this exact fix actually prevents the
  freeze against a real reproduction (not just a plausible mechanism).

### 33. Firmware's attempted repro: SWD/NRST reset does not reproduce the freeze
**2026-08-30, 17:06 CDT**

**Prompt:** none - the firmware session tried the repro it offered in entry
32 (an ST-Link-triggered NRST reset) and reported back a negative result
unprompted.

**Purpose:** Record a genuinely useful negative result so a future session
doesn't waste time trying the same SWD-based repro approach again.

**Changes:**
- No code changes. Firmware polled `Get-PnpDevice` at 500ms resolution
  through an ST-Link-triggered NRST reset and found the COM port stayed
  continuously `Present=True`/`Status=OK` with zero flicker, and nothing
  appeared in the System event log around that timestamp either - an NRST
  reset disturbs the MCU core but not VBUS/the USB link enough for Windows
  to notice at all, let alone the disconnect chime + re-enumeration the user
  actually sees. Confirms the real event (`POR/PDR`, a supply-level
  brownout from physical board insertion) can't be triggered remotely via
  SWD - firmware has no way to induce an actual VDD sag without physical
  access to the connector.
- Practical consequence: entry 32's timeout fix remains unverified against
  the real failure mode, and can only be closed out by the user watching a
  real board reinsertion happen after retesting with the fix in place -
  there's no remaining path to a synthetic repro on either side. Also still
  open: whether Windows keeps the same COM port number across the real
  reset or assigns a new one - same answer, only observable live.
- `CLAUDE.md` — noted this negative result in the disconnect-detection entry
  so a future session doesn't re-attempt an SWD/NRST-based repro.

### 34. Add a real app/window icon, built from the ProtoVerse logo
**2026-08-30, 17:16 CDT**

**Prompt:** "can you make the tool bar icon for the app nice looking if I
give you an image to use" → user pasted the ProtoVerse orbit-mark logo
directly into chat. This app had no icon set at all before this (flagged as
a gap in an earlier modernization discussion) - it showed WPF's generic
default icon everywhere (taskbar, title bar, Alt-Tab, Explorer).

**Purpose:** A pasted chat image isn't automatically a file this session can
process - no filesystem path is exposed for it, and a session-wide search
turned up nothing (one candidate, `%TEMP%\zburm.bmp`, turned out to be an
unrelated stale mountain-landscape image once actually opened). The user
then saved the real file to `PROTOVERSE\logo.jpg` and pointed at it directly,
which resolved the blocker.

**Changes:**
- Background removal: sampled the JPEG's background color (very consistent
  navy, RGB 28/18/68, closely matching the app's own `BgColor` brand token)
  from all four corners, then chroma-keyed every pixel against it with a
  soft-edged alpha ramp (not a hard cutoff) so the mark's own anti-aliased
  edges against JPEG compression noise didn't come out jagged. Verified the
  result pixel-by-pixel in the one region that looked like it might be a
  stray artifact in a downscaled preview - confirmed via a raw alpha scan
  that it was genuinely part of the orbit ring's curling tail, not noise.
- Computed the non-transparent content's bounding box, cropped to it plus a
  small margin, and centered it on a square transparent canvas (source was
  255×208, non-square) so later resizing to square icon sizes doesn't
  stretch it.
- `ProtoVerseApp/Assets/AppIcon.ico` (new) — hand-assembled a real
  multi-resolution `.ico` (16/32/48/256px, PNG-compressed entries per size,
  valid since Windows Vista) via `System.Drawing`, since .NET has no
  built-in multi-size ICO writer. Also kept
  `Assets/logo_transparent_master.png` (the cropped/centered transparent
  source) for any future re-export at a different size.
- `ProtoVerseApp.csproj` — added `<ApplicationIcon>Assets\AppIcon.ico</ApplicationIcon>`
  (controls the `.exe` itself - taskbar/Explorer) and a `<Resource Include="Assets\AppIcon.ico" />`
  item (so `Window.Icon` can locate it at runtime via pack URI).
- `Views/MainWindow.xaml` — added `Icon="/Assets/AppIcon.ico"` to the
  `Window` (controls the title bar/Alt-Tab icon specifically).
- Verified live with real screenshots, not just a successful build: captured
  the actual running window's title bar via `GetWindowRect` +
  `Graphics.CopyFromScreen` and confirmed the orbit mark renders correctly
  next to "ProtoVerse". The taskbar screenshot initially looked like it
  still showed a generic icon - extracted the icon directly from the built
  `.exe` via `Icon.ExtractAssociatedIcon` (bypasses Explorer's shell icon
  cache entirely) and confirmed the correct icon *is* embedded; the taskbar
  view was Explorer's icon cache showing a stale image for that exact file
  path after dozens of rebuilds this session, not an actual defect - not
  something the app can fix, resolves itself after an Explorer
  restart/reboot.
- **Confirmed live, same session:** the user reported the taskbar still
  showed a generic icon after the above. Cleared the icon cache directly
  (`%LocalAppData%\IconCache.db` and `%LocalAppData%\Microsoft\Windows\
  Explorer\iconcache_*.db`/`thumbcache_*.db`) and restarted `explorer.exe`,
  then relaunched the app. Located the exact taskbar button via UI
  Automation (`Shell_TrayWnd` → the "Running applications" pane's
  `BoundingRectangle` - Windows 11's modern taskbar doesn't expose
  individual per-app buttons through `UIAutomation` the way Windows 10's
  did, so the pane-level rect was the only reliable anchor) and screenshotted
  that exact region. Confirmed the real orbit-mark icon now renders
  correctly in the taskbar - the fix was correct all along, it just needed
  the stale cache cleared.

### 35. Icon sizing/weight iteration: bolder strokes rejected, tight no-clip crop kept
**2026-08-30, 17:50 CDT**

**Prompt:** a sequence of live-tested adjustments - "make it bigger and the
lines thicker... visual strength of the other icons... next to VS Code" →
(shown a real taskbar comparison) "doesn't look much different - go much
bigger and more bold!!" → (shown a much bolder version) "that's pretty bad,
go back to the very first iteration" → "just zoom in... minimal margins" →
"bigger! I want the rings... near the edge of the icon max size" → (shown a
version with the orbit tails clipped) "too big! it's clipped haha... just
need all of it to fit."

**Purpose:** Converge on a final icon after directly testing several
distinct approaches against the real taskbar rather than guessing at one.

**Changes:**
- Tried morphological dilation (a two-pass separable max-filter over the
  alpha channel, since .NET has no built-in "grow" filter and a naive
  per-pixel circular-kernel version was too slow to run) to thicken the
  extracted line art. Confirmed live against the real taskbar, next to
  VS Code/Excel/PowerPoint: even a moderate dilation radius closed some of
  the visual-weight gap, but pushing it enough to really match solid-fill
  icons collapsed the design into an unrecognizable blob at 32px/16px -
  rejected outright once shown, not worth the loss of the orbit/circuit
  shape. Reverted to the un-dilated original stroke weight.
- Cropping/scaling iteration on the *undilated* art, each version verified
  by regenerating and visually checking both 256px and 32px renders:
  a small margin (original) → zero-margin "contain" fit (fills the mark's
  longer dimension - width, 225px - edge to edge, letterboxes the shorter
  dimension - height, 141px - since the source is a wide/short 1.6:1 shape)
  → scaling further to fill *both* dimensions, which necessarily clips the
  orbit ring's curled tail tips left/right since the shape isn't square →
  rejected once shown clipped, back to the zero-margin contain fit as the
  final answer. That's the mathematically largest the mark can be at this
  aspect ratio without cutting anything off.
- `Assets/AppIcon.ico` / `Assets/logo_transparent_master.png` — final state:
  zero-margin chroma-keyed extraction, original (undilated) stroke weight,
  contain-fit centered on the square canvas. Verified live against the
  taskbar (full-screen capture cropped in memory, not a direct small-region
  `CopyFromScreen` - see the note below) after each change, with the
  standard icon-cache-clear-and-Explorer-restart already documented in
  entry 34's CLAUDE.md gotcha.
- **New tooling note:** a direct small-region `Graphics.CopyFromScreen` of
  just the taskbar strip intermittently returned a blank white image, while
  capturing the full 1920×1080 desktop and cropping the same region out of
  that bitmap in memory worked reliably every time - likely a DWM/hardware-
  overlay quirk with partial `BitBlt` capture of Windows 11's modern
  taskbar. Prefer full-screen-capture-then-crop for any future taskbar
  screenshot verification.

### 36. Electronic Load: real wire format lands, "measured" chart dropped (open-loop hardware)
**2026-08-30, 21:43 CDT**

**Prompt:** a cross-session message from the firmware session reporting that
`protomod_electronic_load.c` now has real `SetCurrentLimitMa` command
handling, with a Response shape that differs from this app's placeholder,
and flagging (not deciding unilaterally) that the resulting UI mismatch
needed a call from the user - answered via `AskUserQuestion`: "Drop the
chart entirely."

**Purpose:** Stop building against a placeholder payload now that firmware
has a real one, and resolve the UI implication that real hardware here
cannot produce the "measured" telemetry the original panel design assumed.

**Changes:**
- Confirmed via the firmware session: this ProtoMod's current revision is
  genuinely open-loop (bit-banged PWM low-passed into an op-amp forcing
  current through a 10-ohm sense resistor) with no ADC feedback path at
  all - there is no measured voltage/current this hardware can ever report,
  not a gap that will close once parsing is fixed.
- `ViewModels/ElectronicLoadViewModel.cs` - replaced the placeholder 4-byte
  `[voltage_mV, current_mA]` parse and `MeasuredVoltage`/`MeasuredCurrentMa`
  properties with the real 3-byte `[current_ma_lo, current_ma_hi,
  duty_percent]` Response format (current is an echo of what was
  commanded, not a measurement). Removed the `PlotModel`/`LineSeries`/
  OxyPlot dual-axis chart and its `AppendToChart` method entirely, per the
  user's answer - added `CommandedCurrentMa` and `DutyPercent` as plain
  readout properties instead.
- `Views/ElectronicLoadPanel.xaml` - removed the `oxy:PlotView` and the
  "measured" V/I readouts; added "Commanded"/"Duty" readouts bound to the
  new properties, plus an explicit "values above are commanded, not
  measured" note so the panel doesn't imply real sensing exists.
- `Services/MockSerialService.cs` - `BuildLoadTelemetry` now returns the
  real 3-byte format, computing `duty_percent` from the same first-pass
  calibration the firmware session described (I·R=V, V/VDD=duty, R=10Ω,
  VDD=3.3V nominal - explicitly not finalized on their side, real hardware
  verification still pending there). Removed the periodic re-push of load
  telemetry from `EmitTelemetry`: with no ADC feedback, nothing changes
  between commands, so the old 1×/sec fake jitter was itself part of the
  misleading-measurement problem, not something worth simulating anymore.
- Verified live, not just build-clean: ran the app in Simulator mode,
  applied a 100 mA current limit through the real UI, and confirmed the
  panel showed `Commanded: 100 mA` / `Duty: 30%` - matching the calibration
  math by hand (100 mA × 10 Ω = 1 V, 1 V / 3.3 V ≈ 30%) - via UI
  Automation driving the actual controls and a full-screen-capture-and-crop
  screenshot (per entry 35's tooling note), not just reasoning about the
  code.
- `CLAUDE.md`'s "Current state" section split the old combined Accel+Temp/
  Electronic Load placeholder bullet in two: Accel+Temp's payload is still
  an explicit TODO placeholder, Electronic Load's wire format is now
  settled and its chart-free UI is a deliberate design choice, not an
  unfinished panel.

### 37. Diagnosed real-hardware "Commanded stays 0" report: stale firmware, not an app bug
**2026-08-30, 21:59 CDT**

**Prompt:** "when I hit 100mA and then apply for the load - nothing changes
in the commanded portions? address and fix if necessary with the other
agent."

**Purpose:** Root-cause a real-hardware report that entry 36's Electronic
Load Apply flow appeared to do nothing, and fix it if the problem was on
this app's side.

**Changes:**
- No code changed in this app - the root cause was on the firmware side.
  Reproduced the same Apply flow in Simulator mode via UI Automation first
  (real click on the Electronic Load panel's own Apply button, not
  BlinkyLed's - the panel has two buttons named "Apply" and an early
  automation attempt clicked the wrong one before this was caught) and
  confirmed this app's code path was correct: `Commanded: 100 mA` /
  `Duty: 30%` updated exactly as expected.
- Since the user's report was against real hardware, read the Traffic Log
  from their actual session (still present in the running app instance)
  instead of guessing: it showed `TX ElectronicLoad Command 01 64 00`
  (`SetCurrentLimitMa(100mA)`) answered by `RX ElectronicLoad Error 04`
  (`PROTOCOL_ERR_NOT_IMPLEMENTED`) - repeated for both the 100mA and a
  follow-up 30mA attempt. The app correctly received and logged this Error
  frame; it just has no UI path (in any panel, not just this one) to
  surface a `MSG_ERROR` response to the user, only to the Traffic Log -
  which is why nothing appeared to happen instead of showing a visible
  failure. Noted as a pre-existing gap, not fixed in this entry - the user
  hasn't asked for it yet.
- Messaged the firmware session with the exact frame capture. Confirmed
  cross-session: entry 36's real `SetCurrentLimitMa` handler had compiled
  and linked but had not actually been flashed to the bench board yet (the
  firmware session was waiting on a go-ahead before writing to physical
  hardware) - that board was still running pre-entry-36 firmware, which is
  exactly why it fell through to `PROTOCOL_ERR_NOT_IMPLEMENTED`. The
  firmware session then flashed and verified the current build via
  STM32CubeProgrammer (download verified, MCU reset performed) and
  committed the change locally on their side.
- Net result: this app's Electronic Load code has been correct since entry
  36 in both Simulator mode and against a real frame capture; the "nothing
  changes" symptom was a stale bench-board flash, now resolved. Retest
  against real hardware is the next step, not yet confirmed as of this
  entry.

### 38. Diagnosed a real firmware main-loop freeze: unbounded CDC_Transmit_FS retry
**2026-08-30, 23:08 CDT**

**Prompt:** "check the ongoing logs and ID why the FW may have become
unresponsive."

**Purpose:** Root-cause a second real-hardware symptom, seen right after
entry 37's reflash: the Electronic Load panel would work for a while, then
stop getting any reply at all to further commands - no `Response`, no
`Error` either, unlike entry 37's case.

**Changes:**
- No code changed in this app - root cause was firmware-side, but the
  diagnosis itself was done entirely from this app's own Traffic Log,
  which is what actually made it findable. Read the live running
  instance's Traffic Log (not a guess) and found: `SetCurrentLimitMa(10mA)`
  and `SetCurrentLimitMa(250mA)` each got a correct, promptly-returned
  `Response` (`0A 00 04` and `FA 00 4C` respectively - both match the
  calibration math). Then `SetCurrentLimitMa(100mA)`, sent six times in a
  row as the user kept clicking Apply, got zero reply to any attempt over
  ~4 seconds - not a rejection, just silence. This app's own connection
  never dropped (no `Disconnected` event, no read/write timeout exception,
  slots stayed populated) and every TX write completed normally, ruling
  out a cable/COM-port problem on this side.
- Scrolling further back in the same log surfaced a second, separate
  silent burst - `SetCurrentLimitMa(1mA)` sent five times with zero replies
  - and an *earlier*, successful `100mA` exchange from before either silent
  window. That combination (100mA worked once, then didn't; a completely
  different value also went silent) was the key detail: it ruled out a
  value-specific bug in the duty-cycle math and pointed at something
  timing/state-related instead. Reported both data points to the firmware
  session.
- Firmware session found and fixed the real cause: `protocol.c`'s
  `Protocol_SendFrame()` retried `CDC_Transmit_FS()` in an **unbounded**
  loop whenever it returned `USBD_BUSY` - a flag that only clears once the
  host actually drains the previous IN transfer, which firmware doesn't
  control. A brief slowdown on this app's read side (very plausible with
  several rapid Apply clicks queuing up) could leave that loop spinning
  forever, freezing ProtoCore's entire main superloop - `poll_protocol()`
  never runs again, so *every* subsequent Command of any type is silently
  dropped for good, regardless of module or payload, until a physical
  reset. Nothing to do with current-limit values at all, which explains
  why the symptom looked value-independent once enough data points came
  in. Fixed firmware-side by bounding the retry to a 100ms timeout
  (`PROTOCOL_TX_TIMEOUT_MS`) - past that it drops the one frame and
  returns, rather than hanging the system. Built, flashed to the bench
  board, verified via download-verify, and pushed on the firmware side
  (commit `9fce051`).
- **Confirmed fixed against real hardware, same day, 23:10 CDT:** user
  repeated the original trigger - clicking Apply on the Electronic Load
  panel rapidly, several times in a row - and the board stayed responsive
  throughout, no repeat of the freeze. Fully closed.
- Worth remembering for future diagnosis on this side: a silent freeze
  (zero bytes back, connection otherwise looks healthy) versus an explicit
  `Error` response are different failure modes with different causes here
  - a `MSG_ERROR` means firmware is alive and rejecting the frame, silence
  after previously-working exchanges means the MCU's main loop itself may
  have stopped running. The Traffic Log is the tool that distinguishes
  them; this app has no other way to tell the two apart today.

### 39. Documentation-only: Electronic Load hardware confirmed, firmware bumped PWM frequency
**2026-08-30, 23:12 CDT**

**Prompt:** two cross-session updates from the firmware session - first the
user's confirmation that entry 38's rapid-Apply retest holds up on real
hardware, then an unrelated FYI that ElectronicLoad's PWM frequency moved
1kHz→5kHz to cut ripple the user saw on the bench.

**Purpose:** Keep CLAUDE.md accurate as of the last real information from
either side; no app code needed to change for either update.

**Changes:**
- CLAUDE.md: marked entry 38's freeze fix as confirmed against real
  hardware (user repeated the rapid-Apply trigger, board stayed
  responsive) rather than "not yet re-confirmed."
- CLAUDE.md: also caught and fixed a second, unrelated stale note in the
  same section - entry 37's "still not user-retested against real
  hardware" line was already outdated by the time entry 38 was written;
  this session's own Traffic Log captures from earlier the same evening
  (10mA/100mA/250mA all getting correct Responses) were themselves the
  retest, just not yet reflected in the doc. Marked confirmed.
- CLAUDE.md: noted the firmware-side PWM frequency change (1kHz→5kHz,
  firmware commit `64d6351`) - wire format is unchanged, `duty_percent` is
  still a single 0-100 byte, but real hardware now only reports it in
  multiples of 10 (was 2) since the frequency bump traded duty-step
  resolution for a fixed ISR rate. Not a bug if a future duty readout
  looks coarser than the calibration formula alone would suggest.

### 40. Near-black theme; Traffic Log's Info column decoded, Length/Checksum columns added
**2026-08-30, 23:45 CDT**

**Prompt:** "I'm sick of the purple - can you do a dark mode - mostly black
background with accents driven off of the logo/business scheme? also add
in the following - populate the info tab in the help tab - indicate the
meaning of the blocks using additional columns if necessary." Clarified via
`AskUserQuestion` that "the info tab" actually meant the Traffic Log's
existing, never-populated Info column (not a new tab), and "the blocks"
meant the Raw Frame column's hex byte groups - not to be removed, just made
readable alongside.

**Purpose:** Replace the original deep-navy/purple background with a
genuinely dark, near-black theme while keeping the brand accent colors
recognizable, and make the Traffic Log actually explain what a captured
frame means instead of requiring hand-decoding hex against the wire spec.

**Changes:**
- `App.xaml` - swapped `BgColor`/`SurfaceColor`/`SurfaceAltColor`/
  `BorderColor` from the deep-navy/purple ramp (`#1B1642`→`#3D3475`) to a
  near-black neutral-gray ramp (`#0A0A0C`→`#35353C`), and
  `TextSecondaryColor` from a lavender tint (`#A79FD1`) to a neutral gray
  (`#9A9AA4`). Left `AccentTeal`/`AccentGreen`/`AccentBlue`/`AccentOrange`
  and `TextPrimaryColor` unchanged - those are the actual logo-derived
  colors and read cleanly against either background. Updated the same
  three hand-synced literal-color spots CLAUDE.md already flags as unable
  to bind to a `StaticResource`: `Converters/BoolToBrushConverter.cs`'s
  off-state gray, and `Charts/ChartTheme.cs`'s `Background`/`GridLines`/
  `TextMuted`. `Converters/SlotStateToBrushConverter.cs` needed no change -
  it only ever uses the unchanged green/orange accents.
- `Models/FrameInterpreter.cs` (new) - a pure decode function,
  `Describe(ProtocolFrame)`, that turns a frame's `MsgType`/`ModuleId`/
  payload into a short human-readable summary (`"SetCurrentLimitMa: 100
  mA"`, `"Commanded: 100 mA, Duty: 30%"`, `"Temp=23°C, X=0.91g, Y=-0.42g,
  Z=-0.98g"`, `"Slot 0: BlinkyLed, Slot 1: AccelTemp, Slot 2:
  ElectronicLoad"`, `"Error: PROTOCOL_ERR_NOT_IMPLEMENTED (0x04)"`, etc.),
  mirroring the payload layouts already implemented separately in each
  panel's own parsing code. Guards every case with an explicit payload-
  length check (falls back to a generic description rather than indexing
  past a too-short payload) since the frame reader only guarantees a
  checksum-valid frame, not one shaped the way this app expects - a real
  concern given this session's own firmware bugs that produced exactly
  that. Asked the firmware session for the authoritative `protocol_err_t`
  values rather than guessing at the three not yet documented on this
  side; got back the complete set (`protocol.h`, 0x01-0x05, no gaps):
  `PROTOCOL_ERR_NOT_PRESENT`, `_UNKNOWN_MSGTYPE`, `_BAD_PAYLOAD_LEN`,
  `_NOT_IMPLEMENTED`, `_BAD_VALUE`. Any error code outside that set falls
  back to a generic "unrecognized code 0xNN" rather than risk mislabeling
  a future addition.
- `Models/TrafficLogEntry.cs` - `Info` is now populated via
  `FrameInterpreter.Describe` for every sent/received frame (previously
  only ever set for local framing/checksum errors, so it showed "-" for
  literally every normal frame). Added `LengthLabel` and `ChecksumHex`
  properties, extracted from the frame's own `Encode()` output rather than
  recomputed by hand.
- `Views/MainWindow.xaml` - added "Length" and "Checksum" DataGrid columns
  to the Traffic Log (the two Raw Frame "blocks" not already broken out
  into the existing Module/MsgType/Payload columns) and widened Info to
  fill the remaining space. Left STX/ETX (the other two framing bytes)
  without their own columns, since they're constant `0x02`/`0x03` markers
  with no per-frame information - instead added a tooltip on the "Raw
  Frame" column header spelling out the complete byte layout for anyone
  who hovers. The raw hex itself is untouched and still fully visible,
  per the user's explicit "do not remove the raw data" instruction.
- Verified live in Simulator mode via UI Automation and a full-screen
  capture-and-crop screenshot (this session runs on a second monitor with
  a non-zero virtual-screen origin - the crop script needed a fix to
  capture the full virtual desktop and offset by its origin, not just
  `PrimaryScreen.Bounds`, or the earlier single-monitor-only version
  produced a blank sliver): confirmed the new near-black theme renders
  correctly across every panel and the Traffic Log, and confirmed real
  decoded Info text for `PresenceRequest`, `PresenceReport`, `StreamData`
  (AccelTemp), `Command`/`Response` (BlinkyLed and ElectronicLoad) all
  matched the actual values shown in each panel's own UI.

### 41. Rewrote README.md against current reality, added real GUI screenshots
**2026-08-31, 00:17 CDT**

**Prompt:** "update git and specifically update the readme - feel free to
use actual images from the GUI."

**Purpose:** `README.md` still read like the original scaffold handoff
document from before the app was ever built or run - "This was written as
source, not built/tested here," a `PresenceReport` description that
predated the fixed-array format fix, only 3 of the now-5 cataloged
`ProtoModId`s, no mention of Simulator mode, the Traffic Log's new decode,
or the theme - and had no images at all. Brought it in line with what the
app actually is today, for a human landing on the repo cold.

**Changes:**
- `docs/screenshots/main-window.png` and `docs/screenshots/traffic-log.png`
  (new) - real screenshots, not mockups, captured from the app's live
  session actually connected to real ProtoCore hardware over COM4 (an
  Electronic Load and a Blinky LED installed) rather than Simulator mode,
  since that session happened to be open and idle at the time - full-screen
  capture-and-crop per the established tooling note, then cropped further
  in-memory to isolate the module-panels region and the Traffic Log region
  as two separate images. Did not touch the live session's Connect/
  Disconnect state or any control, since it was an active hardware session,
  not this session's own test instance.
- `README.md` - full rewrite: leads with the main-window screenshot and a
  plain "what it does" section aimed at someone landing on the repo cold,
  a module-support-status table (Blinky LED done, Electronic Load done,
  AccelTemp UI-done-payload-placeholder, everything else shown as
  "Unsupported" rather than hidden), real build/run instructions (`dotnet
  build`/`dotnet run`, replacing the old "open in Visual Studio, wasn't
  actually built here yet" scaffold-era instructions), the Traffic Log
  screenshot alongside an explanation of what it actually diagnosed in
  practice, and an updated wire-protocol section (all 5 current
  `ProtoModId`s including `BasicLed`/`Unknown`, the corrected fixed-array
  `PresenceReport` format and why it's fixed-size, and the confirmed 5-value
  error code table). Kept the architecture diagram and "adding a new
  ProtoMod" walkthrough from the original, since both were still accurate.
  Points to `CLAUDE.md`/`CHANGELOG.md` for anything more detailed rather
  than duplicating their content.

### 42. Ran a real-hardware SetCurrentLimitMa sweep for the firmware session: confirmed their fix, then found and fixed a bug in my own diagnostic script, not firmware
**2026-08-31, 00:36 CDT**

**Prompt:** a cross-session request from the firmware session - after adding
a calibration correction to `handle_command()` for a user-reported "10mA and
20mA produce the same duty" bug, they wanted real empirical data rather than
reasoning about the fix by hand: sweep `SetCurrentLimitMa` across the full
1-300mA range against the real bench board and log each Response's echoed
current/duty. Confirmed with the user first (`AskUserQuestion`) before
running ~300 commands against real hardware and before disconnecting the
GUI's live connection to free the port for a standalone script.

**Purpose:** Get precise, real-hardware data to either confirm the
firmware session's calibration fix or pinpoint exactly where duty_percent
collides, rather than continuing to reason about the fix from firmware
source alone.

**Changes:**
- No app code changed - this was a diagnostic run using a new standalone
  PowerShell script (not committed to the repo - a one-off diagnostic
  tool, kept in the session scratchpad) that talks to the serial port
  directly, implementing the same wire protocol as `Models/ProtocolFrame.cs`
  byte-for-byte, bypassing the GUI entirely for speed and reliability
  across ~300 sequential commands.
- Disconnected the GUI first to free the COM port (with the user's
  explicit go-ahead), ran the sweep, then reconnected the GUI afterward -
  in the process, noticed the board had re-enumerated from `COM4` to
  `COM3` partway through this session (a real, if minor, instance of the
  "COM port renumbers on replug/reset" behavior already documented
  elsewhere in this file).
- **Result 1, confirmed correct: the originally-reported duty collision is
  gone.** 10mA now produces duty 2%, 20mA produces duty 5% - cleanly
  distinct - and duty climbs smoothly and monotonically from 1mA up through
  300mA (about one duty step per 3.16mA, consistent with the documented
  10Ω/3.3V calibration formula). The firmware session confirmed this
  matches their new calibration constants exactly
  (`CAL_SLOPE_MV_PER_MA=9.3828`, `CAL_OFFSET_MV=32.906`). The 95% (not
  100%) duty cap at 300mA is also expected, not a bug - the correction has
  to overshoot the naive formula's 90.9% to compensate for real VDD
  running below the nominal 3.3V assumption.
- **Result 2, initially reported as a firmware bug, was actually a bug in
  this session's own diagnostic script - corrected, not firmware's fault.**
  The sweep's logged `EchoedMa` wrapped to 0 at `CommandedMa=256` and
  counted up from there, which looked exactly like an 8-bit truncation
  server-side. Reported it to the firmware session as a probable firmware
  bug. They checked their actual `send_state()` source and showed it
  correctly encodes both bytes (`payload[0] = current_ma & 0xFF`,
  `payload[1] = current_ma >> 8`) - and pointed out that a decoder which
  only reads `payload[0]` would produce exactly this symptom. That was
  the real cause, confirmed by testing the decode line in isolation:
  PowerShell's `-shl` operator **preserves the type of its left operand**,
  so `[byte]$x -shl 8` computes the shift in an 8-bit container and
  silently truncates the result back to 0 instead of promoting to a wider
  integer type first - `$payload[0] -bor ($payload[1] -shl 8)` therefore
  always evaluated the high-byte term as 0 for any value >= 256, even
  though `$payload[1]` itself held the correct byte. Fixed in the
  scratchpad script by casting to `[int]` before shifting
  (`[int]$payload[1] -shl 8`); confirmed there is no firmware bug here at
  all. Worth remembering for any future one-off PowerShell diagnostic
  script that reconstructs a multi-byte value: cast to a wider integer
  type *before* shifting, not after.
- Net result: no firmware or app changes needed from this sweep - both
  sides are working correctly. The value was in getting real data to
  confirm the calibration fix, and in catching a tooling bug in the
  process of investigating what turned out to be a second tooling bug
  rather than a second firmware one.

---

### 43. New Library tab: the full ProtoMod catalog as a discovery surface, built only from real source material

**2026-08-31, 17:15 CDT**

**Prompt:** create a new "Library" tab showing the full ProtoMod catalog
(not just what's connected), hardcoded for v1 but structured to move to a
JSON/manifest source later. Each entry needs name + code, series and
difficulty, a one-sentence description, a brief schematic, 2-3
creative-challenge-style ideas, owned vs. not-owned state driven by whatever
the app already uses to detect connected panels, and "next step" links to
related modules. Explicitly framed as a marketing/discovery surface -
not-owned modules should look inviting rather than locked. Hard constraint
repeated several times in the prompt: **no fabricated content** - only
modules and details that actually exist in this codebase or its manual docs,
with anything unbacked marked as TODO/missing in the UI rather than filled
with plausible filler, and progression links only where real evidence
supports them. Also: create and switch to branch `feature/protomod-library`
first, and don't touch the wire protocol, `FrameInterpreter`, or any module
control logic.

**Purpose:** Give the app a place that answers "what else is there?" rather
than only "what's plugged in right now" - and do it without inventing a
product catalog, which for a hardware education product would be actively
harmful (a fabricated module description is a promise the hardware can't
keep).

**Source material gathered first, before writing any content.** Extracted
the text of every module manual in `PROTOVERSE/Manuals/` by unzipping each
`.docx` and stripping the XML, and enumerated the KiCad schematic exports in
`PROTOVERSE/Finished Modules/`. That produced six real boards - F01 Blinky,
F02 Simple LED, E03 Sensors 1, E05 Electronic Load, A01 Direct Digital
Synthesis, F00 Headers - and is the entire basis for the catalog. No seventh
module was added, including the "Logic ProtoMod" that the F01 manual's own
next-step text mentions: it has no board, no manual, and no `ProtoModId`, so
inventing a card for it would be exactly the fabrication the prompt ruled
out.

**Changes:**
- `Models/ProtoModLibraryCatalog.cs` (new) - flat, JSON-friendly records
  (`ProtoModCatalogEntry`, `ProtoModIdea`, `ProtoModNextStep`) plus the
  hardcoded six-entry catalog. Every content field is paired with a
  `*Source` field naming the document and section it was quoted from, and
  every field with no source material is `null`. A long file comment records
  the sources used, the "don't fill a null with something that sounds right"
  rule, and the intended migration to a manifest shared with the manual docs.
- `ViewModels/LibraryViewModel.cs` (new) - `LibraryEntryViewModel` turns each
  null into its "...coming soon" string; `NextStepLinkViewModel` carries the
  evidence sentence through to a tooltip so a person can see *why* the app
  claims one module leads into another. `UpdateInstalled`/`ClearInstalled`
  are the only mutable state.
- `Views/LibraryPanel.xaml` / `.xaml.cs` (new) - a `ListBox` with a
  `WrapPanel` items panel (two columns of 385px cards at the default window
  width - measured, see below). Selection chrome is stripped from
  `ListBoxItem`; selection is expressed as an orange highlight ring on the
  card itself, and the code-behind's one job is `ScrollIntoView` on selection
  change, which is what makes a "leads into" link scroll to its target.
- `Converters/BoolToVisibilityConverters.cs` (new) - `BoolToVisibility` and
  its inverse, registered in `App.xaml` (not a view's own resources - see the
  converter-scope gotcha in `CLAUDE.md`).
- `App.xaml` - registered the two converters, and added a **keyed**
  `MainTabControlStyle` for the new top-level tabs. It is a copy of the
  implicit `TabControl` style with the content row changed from `Height="Auto"`
  to `Height="*"`. The implicit style must stay `Auto` because it's used
  inside an `Expander` with no bounded height, where a star row measures to
  zero and the content silently vanishes (a gotcha this project already hit);
  the new style is only applied where the parent row *is* bounded, where the
  opposite is true. Two parents, two correct answers - hence two styles
  rather than changing the shared one.
- `Views/MainWindow.xaml` - the main content row is now a two-tab
  `TabControl`: **Slots** (the existing stacked panels, moved verbatim into a
  `TabItem`) and **Library**. Chose a top-level tab over a third tab in the
  bottom Traffic Log/Help drawer because that drawer is collapsed by default
  and fixed at 220px - fine for a diagnostic log, wrong for a surface whose
  whole job is to be browsed.
- `ViewModels/MainViewModel.cs` - added a `Library` property and two calls:
  `Library.UpdateInstalled(detectedIds)` after a `PresenceReport` rebuild, and
  `Library.ClearInstalled()` in `ResetSlotsToEmpty`. Ownership is derived from
  the same per-slot `ProtoModId` values the panels are built from, so the two
  can't drift. Disconnect clears to "connect to see what's in your kit" rather
  than marking everything not-owned - "we don't know" and "you don't own it"
  are different claims. The build-then-swap and per-slot try/catch structure
  of `OnFrameReceived` is untouched.
- `ViewModels/HelpViewModel.cs`, `README.md` - a user-facing revision note,
  and a README section covering the tab, the deliberate v1 hardcoding, and
  the two inferred fields flagged below.
- **Not touched, per the prompt:** the wire protocol, `FrameInterpreter`, and
  every panel/module view model.

**What's backed by real sources vs. marked missing.** Manual-quoted:
descriptions (each module's "Core Concept" line), schematic summaries (F01's
Appendix C, F02/E03/A01's Overview and Background sections), and all ideas.
F01's three ideas come from its Gen2 manual's actual **Creative Challenge**
section; F02/E03/A01 have no such section, so their ideas are quoted from
their manuals' "Now try this" experiments and each idea's source label says
so explicitly. E05 has no manual at all - its description and schematic
summary come from this repo's own `CLAUDE.md`, and its ideas are genuinely
empty ("Project ideas... coming soon"). F00 Headers has hardware and a
schematic PDF and nothing else written about it anywhere, so every content
field is null and its card is almost entirely "coming soon" - included
because it's a real board, not excluded because its card looks sparse.
Difficulty and time estimate exist **only** for F01 (the one manual written
against the newer template that has those fields); every other card reads
"Difficulty not yet rated". No schematic *drawing* is bundled for any board -
only KiCad PDF exports exist, outside this repo - so every card shows a
placeholder tile referencing the real PDF rather than a fake thumbnail.
Exactly one "leads into" link exists in the whole catalog (F01 -> F02), quoted
from the Blinky manual's "Future ProtoMods for you" section.

**Two judgment calls worth reviewing:**
1. **Series for the two manual-less boards is inferred**, not quoted - E05 is
   listed as Explorers and F00 as Fundamentals on the strength of the
   circuit-code letter pairing that every existing manual follows (F =
   Fundamentals, E = Explorers, A = Advanced). Consistent, but never written
   down as a rule anywhere. Flagged in both the source file and README.
2. **The two F01 manuals disagree** and the newer one was used throughout.
   `Manuals/F01_Blinky.docx` titles the board "Blink", calls the series
   "Foundations", and describes F02 as "the Switch ProtoMod";
   `Manuals/Gen2/Blinky_F01_Manual.docx` titles it "Blinky", says
   "Fundamentals Series", and describes F02 as "Simple LED" - which is what
   F02's own manual calls it. Everything F01 contributes to the catalog comes
   from the Gen2 manual, on the grounds that it's the one that agrees with
   the rest of the docs. If the older manual is actually the current one,
   F01's card needs revisiting.

**Verification.** Built clean (only the two pre-existing `MockSerialService`
warnings). Drove the running app via UI Automation in Simulator mode:
connected, switched to the Library tab, and dumped every text element and
button - all six cards render with the exact quoted content, the header reads
"3 of 6 ProtoMods in this catalog are plugged into your ProtoCore right now",
and ownership badges are correct (F01/E03/E05 "In your kit"; F02, A01, F00
"Not yet in your kit" - F02 correctly *not* owned in the simulator, which
reports Blinky/AccelTemp/ElectronicLoad). Invoking the F01 card's
"Simple LED (F02)" link selected the F02 card and scrolled it to the top of
the viewport (verified via the list items' bounding rectangles before and
after). Card width was 410px initially, which measured 422px per card against
an 834px list - 10px too wide for two columns, so every card wrapped to its
own row; reduced to 385 and re-measured, confirming pairs now share a row at
x=216 and x=613.

**Not verified: the visual appearance.** Screenshot capture returned a blank
white client area for the *entire* app window - including the untouched Slots
tab and the brand header - under both a full-desktop `CopyFromScreen` crop
(the workaround `CLAUDE.md` recommends for taskbar capture) and
`PrintWindow` with `PW_RENDERFULLCONTENT`. The title bar captured correctly
in both, and other windows on the desktop captured fine, so this is a capture
problem specific to this app's WPF client area in this environment, not a
rendering failure introduced here - the UI Automation tree reports every
element with sane, non-zero bounding rectangles laid out inside the window.
Colors, spacing, and contrast on the new cards therefore have **not** been
seen by anyone yet and are the one thing worth eyeballing before this
branch merges.

---

### 44. Library family filter (F/E/A), and the series-inference caveat resolved

**2026-08-31, 17:40 CDT**

**Prompt:** "There will be three general families of protomods, Fundamentals
(F circuit codes), Explorer (E circuit codes), and Advanced (A circuit
codes) - use this information to allow for filtering of boards in the GUI."
(The same message also raised a ProtoCore-side "remember every ProtoMod ever
seen" idea and asked for thoughts - that's discussion, not implemented here,
and nothing about the wire protocol was touched.)

**Purpose:** Two things at once. Practically, give the Library a family
filter. More importantly, this **resolves judgment call #1 from entry 43**:
that entry flagged the series of the two manual-less boards (E05, F00) as an
inference, since every manual states its own series in prose but no document
had ever written down the letter-to-family rule. The user has now confirmed
the rule directly, so it is a real product fact rather than a guess, and a
board with no manual can be given a correct family.

**Changes:**
- `Models/ProtoModLibraryCatalog.cs` - rewrote the `ProtoModSeries` doc
  comment to state the confirmed F/E/A rule and to mark it explicitly as the
  *one* field in this catalog that may be derived rather than quoted; the
  no-fabrication rule still covers descriptions, ideas, schematics, and
  progressions. Removed the "this is an inference, see README" caveats from
  the E05 and F00 entries.
- `ViewModels/LibraryViewModel.cs` - added `SeriesFilterViewModel` (one filter
  button; `Series` is null for "All") and, on the library, a `SeriesFilters`
  list plus an `EntriesView` `ListCollectionView`. The grid now binds to the
  view rather than to `Entries` directly, so filtering never disturbs card
  state - the cards are the same objects either way, they're just shown or
  not. Counts are baked in at construction since the catalog is static.
- `SelectByCode` now clears the filter when the target is in a different
  family than the active one. Nothing says a Fundamentals board can't lead
  into an Explorers one, and without this a cross-family "leads into" link
  would select and highlight a card that had been filtered out of view.
  Following a link is an explicit request to go look at that module, so it
  wins over the filter.
- `Views/LibraryPanel.xaml` - filter row above the grid. A flat row of
  buttons rather than a dropdown: with only three families plus All, every
  option is visible at once and one click away. **Gotcha worth remembering:**
  the unselected/selected colors are `Style` setters, not local attributes on
  the `Button` - a locally-set `Background` outranks a `Style` trigger in
  WPF's value precedence, so setting it inline silently defeats the
  `IsSelected` `DataTrigger` (written the wrong way first, caught before
  building).
- `ViewModels/HelpViewModel.cs`, `README.md`, `CLAUDE.md` - user-facing
  revision note; README and CLAUDE.md updated to state the F/E/A rule as
  confirmed and to drop the inference caveat.

**Verification.** Built clean. Drove the app via UI Automation in Simulator
mode and checked which cards are present after each filter click: All -> all
six; Fundamentals -> Blinky (F01), Simple LED (F02), Headers (F00);
Explorers -> Sensors 1 (E03), Electronic Load (E05); Advanced -> Direct
Digital Synthesis (A01); back to All -> all six restored. Button labels carry
the right counts (All 6, Fundamentals 3, Explorers 2, Advanced 1). Same
screenshot-capture limitation as entry 43 applies - the filter row's selected
vs. unselected styling has been reasoned about but not seen.

---

### 45. Library remembers previously-connected ProtoMods - app-side only, and "seen" is not "owned"

**2026-08-31, 18:05 CDT**

**Prompt:** in response to a proposal to have ProtoCore firmware track every
ProtoMod it has ever interacted with, plus my own pushback that such history
belongs to a person rather than a board: "no seeing it doesn't necessarily
mean 'owns' - as you said it should track with an account or at the very
least linked to user's installation of the app. Why would you need to convey
anything to the firmware for this. I agree that nothing should be stored on
the firmware side."

**Purpose:** Fix a real defect in the Library as shipped in entry 43 - a
module vanished from the user's library the instant it was unplugged, because
ownership was a live snapshot of three slots. Do it without the firmware-side
registry that was originally floated, and without overstating what the data
actually means.

**Two decisions this settles (don't relitigate):**
1. **History is app-side, never on ProtoCore, and no wire-protocol or
   firmware change is involved.** The user's point was sharper than my own
   suggested firmware/app split: there is nothing to convey to firmware at
   all. The app already receives every `PresenceReport`, so it can simply
   record what it already sees. The earlier reasoning for why the board is the
   wrong home still stands (a shared classroom ProtoCore gives every student
   the same wrong answer, a reflash silently deletes it, a second ProtoCore
   knows nothing about the first), but the implementation is now purely local
   - no new `MsgType`, no firmware work, no cross-session agreement needed.
2. **Seeing a module is not owning it.** Confirmed explicitly by the user. A
   borrowed board plugged in once is recorded forever and there is no un-see,
   so nothing in this tab may be phrased as an ownership or purchase claim.
   Every label is about connection history instead. Real ownership, if it is
   ever needed, is account data layered on top of this rather than read out
   of it.

**Changes:**
- `Services/ModuleHistoryStore.cs` (new) - persists every `ProtoModId` this
  installation has seen to `%AppData%\ProtoVerse\module-history.json`, with
  first/last-seen timestamps and a schema version. Scoped to the app
  installation for now; the document shape is deliberately flat and
  account-syncable so moving it behind a user account later is a change to
  where this class reads and writes, not a change to its callers. Skips
  `ProtoModId.Unknown` (that means "ProtoCore saw something it can't
  identify" - not a type, and recording it would leave a permanent
  meaningless entry). Saves only on a genuinely new type or a new
  last-seen day, not on every `PresenceReport`, since a hot-swap fires those
  in bursts. Every file operation degrades to "no history" and a surfaced
  message rather than throwing - this is a cosmetic distinction and losing it
  must never take down a hot-swap rebuild. A corrupt file is deliberately
  *not* deleted on read failure.
- `ViewModels/LibraryViewModel.cs` - replaced the `IsInKit` bool with a
  three-state `ModuleConnectionState` (NeverConnected / PreviouslyConnected /
  ConnectedNow). `UpdateInstalled` now records to the store then recomputes;
  `ClearInstalled` (disconnect) drops back to history-only rather than
  resetting everything to never-seen - unplugging a board isn't the same as
  never having had it. A board with no assigned `ProtoModId` (A01, F00) can
  never be reported, so it stays NeverConnected and says so in its own
  footnote. The store is constructor-injectable for testing or for
  account-backed storage later.
- `Views/LibraryPanel.xaml` - badge and stripe now have three colors: green
  "Connected now", teal "Connected before", blue "Not yet connected". All
  three cards are otherwise visually identical, so a never-connected module
  still never reads as locked or second-class. Header subtitle rewritten off
  "the ones in your kit" onto connection wording.
- `ViewModels/HelpViewModel.cs`, `README.md`, `CLAUDE.md` - user-facing
  revision note; both docs now carry the app-side-only decision, the
  no-firmware-involvement point, and the seen-is-not-owned distinction.

**Verification.** Built clean. Drove the app via UI Automation across two
launches with the real `%AppData%` file (which did not exist beforehand):
- Fresh launch, before connecting: 0 connected now, 0 before, 6 not yet;
  summary "Connect your ProtoCore to start filling in your library".
- After connecting in Simulator mode: 3 now, 0 before, 3 not yet; summary
  "3 of 6 ProtoMods in this catalog are plugged into your ProtoCore right
  now."
- After clicking Disconnect: 0 now, **3 before**, 3 not yet; summary
  "Nothing plugged in right now. You've connected 3 of 6 ProtoMods in this
  catalog before." - this is the defect from entry 43, now fixed.
- History file written correctly with `schemaVersion: 1` and three sightings
  (moduleId 1/2/3, circuit codes F01/E03/E05, matching timestamps).
- Relaunched the app from scratch: 0 now, 3 before, 3 not yet *before
  connecting to anything*, confirming the history survives a restart.

Same screenshot-capture limitation as entries 43-44 - the three badge colors
have been reasoned about but not seen.

---

### 46. Local profiles and an explicit "is this yours?" question - resolving the one-way kit problem

**2026-08-31, 18:20 CDT**

**Prompt:** "Resolve #2. Can we create accounts with log ins (no actual
security) just for board tracking? Login/logout/etc options on the top
right... then you can confirm that board is or isn't in your kit." (#2 was
the open flag from entry 45: a board plugged in once was marked forever with
no way to un-see it.)

**Purpose:** Fix the one-way-door problem, and give the tracking an owner.
Entry 45 deliberately refused to treat a sighting as ownership but had no way
for the user to *say* either way - so a borrowed board sat in the library
permanently, and two people sharing a PC shared one merged history.

**Two things now tracked per module, deliberately not conflated:**
- **Has it been plugged in?** Observed automatically from `PresenceReport`.
- **Does the user say it's theirs?** `KitStatus`, only ever set by clicking.
A newly-seen card asks "Is this ProtoMod part of your kit?" with
**Yes, it's mine** / **No, just borrowed**, and once answered collapses to a
one-click flip ("Not mine after all" / "Actually, this is mine"). Nothing is
ever claimed on the user's behalf, and no answer is permanent.

**Changes:**
- `Models/UserAccount.cs` (new) - `KitStatus` (Unanswered/InKit/NotMine),
  `ModuleRecord` (per-module sighting timestamps + kit answer), `UserAccount`,
  and the versioned `AccountsDocument`.
- `Services/AccountStore.cs` (new) - profiles and their tracking, persisted to
  `%AppData%\ProtoVerse\accounts.json`. Replaces `ModuleHistoryStore.cs`
  (deleted), whose per-installation history is now per profile. Imports a
  pre-accounts `module-history.json` into the first profile created, renaming
  the old file to `.migrated` rather than deleting it; imported sightings come
  in as `Unanswered`, since they're observations and not ownership claims.
  Keeps the earlier design decisions: skips `ProtoModId.Unknown`, saves only
  on a new type or new day rather than every `PresenceReport`, degrades to
  "no accounts" on any file error, and never deletes a corrupt file it failed
  to read.
- `ViewModels/AccountViewModel.cs` (new) - backs the top-right control; raises
  `SignInRequested` rather than constructing a Window, since showing a dialog
  is the view's job.
- `Views/SignInWindow.xaml`/`.cs` (new) - profile picker: create, pick,
  delete (with a confirm, since deleting throws away everything that profile
  tracked). No password field, deliberately.
- `ViewModels/LibraryViewModel.cs` - cards now carry `KitStatus` and an
  `IsSignedIn` flag alongside connection state; `StatusLabel` picks one of
  five badges. Kit answers route through one `SetKitStatus` method. Signed
  out, the ownership question is hidden entirely rather than asked with
  nowhere to store the answer. Summary reports "plugged in right now" and
  "confirmed in <name>'s kit" as two separate numbers.
- `Views/LibraryPanel.xaml` - the kit prompt card, the flip button, and a
  fifth badge state. Trigger order matters: NotMine after InKit (so a
  disowned but previously-connected board reads blue), ConnectedNow last so
  it always wins.
- `Views/MainWindow.xaml`/`.cs`, `ViewModels/MainViewModel.cs` - header split
  into a Grid with the sign-in control pinned right; `AccountStore` is owned
  by MainViewModel and shared with the Library so both see the same active
  profile.
- `README.md`, `CLAUDE.md`, `HelpViewModel.cs` - documented, including that
  the profiles are explicitly not security and shouldn't grow a password
  field without becoming real.

**Two bugs found and fixed while testing, both worth remembering:**
1. **Subscribing to the view model's event in the `MainWindow` constructor
   silently didn't attach**, so clicking "Sign in" did nothing at all - no
   dialog, no error, because the view model just raises an event nobody was
   listening to. Moved to `Loaded` (plus `DataContextChanged`), where the
   XAML-declared DataContext is guaranteed to be in place.
2. **A stale `ProtoVerseApp.exe` from an earlier scripted run held the output
   file, so several rebuilds never replaced it** and multiple rounds of
   debugging were spent on stale code. `dotnet build` does report this
   (`error MSB3027`), but it's easy to miss under the usual warnings - grep
   for `error MSB`, not just "Build succeeded", and kill stray processes
   before every scripted run. Added to `CLAUDE.md`'s gotchas.

**Verification.** Built clean. Because UIA can't see this app's modal dialogs
in this environment (see below), the flow was verified in two halves:
- *Dialog opens:* confirmed via Win32 `EnumWindows` filtered by process id -
  clicking "Sign in" produces a new visible 380x420 top-level window at the
  expected position, matching `SignInWindow`'s declared size.
- *Everything else, through the main window:* seeded `accounts.json` with a
  profile and known kit answers, then drove the app. Header showed
  "Zach / Signed in". Cards identified by their schematic-tile circuit code
  (not their title - a card's title also appears on *other* cards as a "leads
  into" link, which produced a bogus reading in a first pass). Confirmed:
  a seeded `Unanswered` board shows the prompt; `InKit` shows "In your kit"
  with a "Not mine after all" flip; `NotMine` shows "Not in your kit" with an
  "Actually, this is mine" flip. Clicking the prompt's "Yes, it's mine" moved
  F01 to "In your kit" and the summary from 1 to 2 confirmed. Flipping E05
  NotMine->InKit and E03 InKit->NotMine both worked and moved the count.
  Connecting in Simulator mode moved F01/E03/E05 to "Connected now", left F02
  (not reported by the simulator) at "Not yet connected", and wrote new
  `Unanswered` records for the newly-seen boards without touching F01's
  existing answer. Disconnecting reverted them to "In your kit" /
  "Connected before" as appropriate. Signing out hid all tracking and
  restored "Sign in (top right) to track which of these are in your kit."
  A01 and F00 (no assigned `ProtoModId`) correctly stayed untracked
  throughout, with their existing "ProtoCore can't identify this board yet"
  footnote.
- `accounts.json` verified on disk after each stage: kit answers persisted,
  sighting timestamps updated, `activeAccountId` cleared on sign-out.

**Still not verified: the visual appearance**, including the sign-in dialog's
entire contents. Screenshot capture returns a blank client area for this
app's windows in this environment, and UIA can't enumerate its modal dialogs
at all (both now recorded in `CLAUDE.md`). The dialog is confirmed to open at
the right size, but nobody has actually looked at it.

---

### 47. Evaluation spike: in-app ProtoMod manuals, demoed on Electronic Load (E05)

**2026-08-31, 19:10 CDT**

**Prompt:** an evaluation brief (`in-app-manuals-eval-prompt.md`) asking for a
spike - not a production feature - rendering ProtoMod manuals natively inside
the app instead of a separate Word/PDF window, built for one module, on a
throwaway branch, with a written recommendation and effort estimate. Followed
by three decisions from the user when asked: (1) the left column should show
the three slots with a presence dot, and selecting one opens that module for
learning/work - accepting for now that this doesn't model two ProtoMods
working together; (2) demo on the Electronic Load rather than Blinky; (3)
placeholders are fine, the focus is UI not appearance. Plus one architectural
note: learner status/progress should eventually be retained per user.

**Purpose:** Find out whether native in-app manuals are worth building across
the whole library, and what it would actually cost - rather than committing to
it.

**Three things the brief assumed that turned out not to hold**, all confirmed
before any code was written:
- It assumed manuals are reachable in-app today and asked which entry point to
  replace. There is none - the app has never linked to a manual. The `.docx`
  files aren't even in this repo.
- It named Blinky F01 as the demo module, partly to evaluate the fillable
  value table. F01 has no adjustable values and no such table anywhere in its
  manual. E05 - the module the user independently chose - is the one board
  with a dial to turn and a number to read back, so it genuinely exercises
  that pattern.
- It asked whether placeholder content would need drafting. For F01 it
  wouldn't have: `Manuals/Gen2/Blinky_F01_Manual.docx` is already written
  against the exact template the brief cites. For E05, nothing exists at all.

**Changes** (branch `eval/in-app-manuals-e05`, three commits):
- `Models/Manual/ManualBlocks.cs` - content model mapping 1:1 to the manual
  template. Callouts, figures, checklists, step lists with inline Observe
  prompts, fillable value tables and question lists are first-class block
  types, not styled paragraphs, because they're the patterns that repeat.
- `Models/Manual/ManualBoilerplate.cs` - assembly steps and the "no single
  correct answer" reassurance, shared rather than repeated per manual.
- `Models/Manual/ElectronicLoadManual.cs` - E05's content. Real material from
  this repo's own docs where it exists (the calibration, the open-loop
  constraint, the 300mA limit), explicit placeholders everywhere it doesn't.
  Six of twelve sections are placeholders, which the UI states plainly.
- `Models/Manual/ManualProgress.cs` - the shape for retaining learner work.
  Designed, deliberately not yet persisted.
- `ViewModels/ManualViewModel.cs`, `Views/ManualView.xaml` - the renderer: one
  set of DataTemplates keyed by block type.
- `ViewModels/SlotViewModel.cs`, `MainViewModel`, `MainWindow.xaml` - the
  navigator/workspace layout. `Panels` became `Slots`; panel view models are
  wrapped, not changed.
- `EVALUATION.md` - the deliverable.

**Recommendation, in short: proceed, with two changes to the plan.** Build a
`.docx` -> JSON converter *before* transcribing any more manuals (~1 day, pays
for itself around manual #5, and avoids drift from the Word source that is and
will remain the authoring surface), and pull learner-progress persistence into
v1 rather than leaving it as a v2 stretch. The second is because this demo
already has the defect that argues for it: `SlotViewModel` rebuilds its
`ManualViewModel` on every `PresenceReport`, so hot-swapping a module silently
discards anything typed into the value table. An unsaved fillable field is
worse than paper. Persistence is roughly half a day, because
`Services/AccountStore` already stores per-account per-module records.

**Two honest findings that cut against the feature:**
- **A partially-transcribed manual is worse than no in-app manual.** The
  learner finds half the sections missing, opens Word anyway, and now has two
  sources that can disagree. The E05 demo shows exactly this state on purpose.
- **Print-to-PDF genuinely goes away**, and for a classroom product that may
  matter more than anything the in-app version adds - the fillable table is
  precisely the thing a facilitator would want to hand out on paper. Text
  scaling for low-vision users is a second real regression: font sizes here
  are fixed px.

**Verification.** Driven through UI Automation in Simulator mode: navigator
shows three slots with correct presence dots and "Manual available" only on
E05; the manual renders its header, metadata row, provenance banner and all 11
TOC entries; 3 Tech notes, 4 Observe prompts, 2 figure placeholders and 5 of 6
placeholder callouts render (the sixth is inside the gated answer key,
correctly hidden until revealed); 14 checkboxes and 22 text fields present;
typing into a table cell and ticking a step both work; "Reveal answers"
removes the gate and shows Appendix A; a TOC click scrolls the target section
from y=658 to y=126; a slot with no manual shows the explanatory placeholder.

**Not verified: appearance** - same blank-capture limitation as entries 43-46.
The layout is verified structurally and behaviourally; nobody has looked at
it. Whether the manual is genuinely readable at this width is unassessed.

---

### 48. Real Electronic Load manual transcribed - and it describes a different board than the one the app talks to

**2026-08-31, 19:45 CDT**

**Prompt:** just a path - `C:\Users\zburm\Desktop\manual` - containing
`Electronic_Load_E02_Manual.docx`, supplied while `ElectronicLoadManual.cs`
was open in the editor. Read as: here is the real manual, use it instead of
the placeholders.

**Purpose:** Replace the six-of-twelve placeholder sections in the manuals
spike with the actual authored manual, which also turns the demo from "what a
half-written manual looks like" into "what a finished one looks like".

**Changes** (branch `eval/in-app-manuals-e05`):
- `Models/Manual/ElectronicLoadManual.cs` - rewritten as a faithful
  transcription of the supplied `.docx`. All twelve sections, every paragraph,
  callout, question and answer in the document's own wording. Placeholder count
  went from 6 to 0.
- `Models/Manual/ManualBlocks.cs` - added `CalloutKind.Discrepancy` for
  app-side notes where the manual and the hardware disagree (see below).
- `Views/ManualView.xaml` - renders that kind as the loudest thing on the page;
  also made the provenance note always visible rather than only appearing
  inside the unfinished-manual warning, since a finished manual should still be
  able to say where it came from.
- `Views/MainWindow.xaml` - fixed a XAML comment that had been placed inside
  the `<Window>` tag's attribute list.
- `EVALUATION.md` - revised with a measured transcription cost, the new
  conflict finding, and a third recommendation.

**THE MANUAL AND THE BOARD DISAGREE - needs a human decision, flagged not
resolved.** Two conflicts, both surfaced in the UI as attributed app-side
notes rather than quietly reconciled:
1. **Module code.** The manual says **E02** throughout. Everything else says
   **E05** - `ProtoModBoardCatalog`'s circuit code, the hardware folder
   `PC01_E05_ProtoMod_ElectronicLoad`, and the Library catalog.
2. **The circuit.** The manual describes a power MOSFET with an op-amp
   feedback loop, a **1 Î©** sense resistor, and ProtoCore's **DAC** setting the
   target while its **ADC reads back live voltage and current for on-screen
   monitoring**. The real board is **open-loop**: bit-banged PWM into an
   op-amp, a **10 Î©** sense resistor, no ADC feedback path - which is exactly
   why the Electronic Load panel's chart was removed rather than plotting
   numbers the hardware can't produce (CHANGELOG 36). Sections 4 and 5 ask the
   learner to watch voltage sag on screen while current holds steady; this
   revision can show neither quantity.

   The manual's own Appendix C flags its component values as provisional, so
   it may describe an intended or revised board - but that's a guess, and this
   project doesn't settle hardware questions by guessing.

**Why this became an evaluation finding rather than just a content bug:** a
manual rendered beside live slot state borrows that state's credibility. A
`.docx` in a folder makes no implicit claim about which board is plugged in;
an in-app manual does. So a wrong manual is *more* dangerous in-app than in
Word, and "only surface a manual once it agrees with the hardware" joined the
recommendations, along with having the proposed converter validate a manual's
stated module code against `ProtoModBoardCatalog` rather than just converting
it. On the other hand, transcription is what surfaced a conflict that had
presumably existed unnoticed since the manual was written - real value, but it
means per-manual rollout cost includes reconciliation time unrelated to the
app.

**Also revised in EVALUATION.md:** transcribing a complete ~2,900-word,
12-section manual measured at 20-30 minutes, cheaper than the 1-2 hour
estimate in the first draft (which had been extrapolated from a module with
almost no content). The recommendation to build a converter rather than hand
transcribe is unchanged, but the argument is now drift from the Word source
rather than per-manual time.

**Two process mistakes worth recording, both cost a test cycle:**
- A XAML comment placed between attributes of the `<Window ...>` tag is a hard
  `MC3000` parse error.
- The build failure that caused was hidden by grepping `error CS|error MSB` -
  `MC3000` matches neither - so a test run reported results from a stale
  `.exe` as though they were current. This is the second time this session
  that a stale binary produced confidently wrong test output (the first was a
  file lock, CHANGELOG 46). Both now in `CLAUDE.md`: match `error` broadly.

**Verification.** Rebuilt clean, then re-ran the UI Automation pass against
genuinely current code: header shows Explorers Series / Module E02 /
Intermediate / 45-60 min / Simple LED (F02); provenance note visible; all 11
TOC entries; 2 Tech notes, 1 Observe prompt, 2 figure placeholders, **4
discrepancy callouts**, 1 reassurance callout, 0 placeholders; 13 checkboxes
and 15 text fields; typing into a value-table cell and ticking a step both
work; "Reveal answers" reveals the real answer key; a TOC click scrolls the
target section to y=188; a slot with no manual still shows its placeholder.
Appearance remains unverified, as in entries 43-47.

**Noted for the Library branch** (not acted on here): this manual documents
two real progression links - Simple LED (F02) as a prerequisite, and
DDS/Sinusoid Generator (A01) as the next step - which is exactly the kind of
sourced evidence `ProtoModLibraryCatalog` requires for a "leads into" link. It
also states the F/E/A series rule in prose ("Fundamentals (F) is first-touch,
no prerequisites. Explorers (E) builds on at least one Fundamentals ProtoMod.
Advanced (A) assumes comfort with code, signals, or measurement"), which is
the first written confirmation of a rule that until now was only a
user-confirmed convention.

---

### 49. Electronic Load manual: E02 confirmed a typo, corrected to E05 - circuit conflict still open

**2026-08-31, 20:05 CDT**

**Prompt:** "It is indeed E05. The E02 in the manual is incorrect - a typo."

**Purpose:** Close the first of the two manual-vs-hardware conflicts raised in
entry 48, and make sure the correction is recorded rather than silently
applied - the Word document still contains the typo.

**Changes** (branch `eval/in-app-manuals-e05`):
- `Models/Manual/ElectronicLoadManual.cs` - every learner-facing "E02" is now
  "E05": the header code, `ModuleCode`, the Overview's module-name line, the
  "You'll need" board entry, and both assembly steps (including "confirm
  \"E05\" appears in the correct slot"). This is the one place the file
  knowingly departs from its source document, and it's marked as such - a
  transcription that silently edits its source is not a transcription.
- The setup-section discrepancy callout no longer mentions the module code;
  it now covers only the point that still stands, which is that the multimeter
  listed as optional isn't optional on a board that reports no measurements.
  Four discrepancy callouts remain, all about the circuit.
- `SourceNote` (shown in the UI) now states that the document says E02, that
  it's a confirmed typo, and that E05 is displayed instead - so a reader who
  opens the `.docx` and sees a different code isn't left guessing which is
  right.
- `EVALUATION.md` - the conflict table now tracks status per row; the module
  code row is Resolved and the circuit row is Open.

**The Word document still has the typo.** It should be corrected at source, or
a future `.docx` -> content converter will faithfully reintroduce E02. Noted
in the code and in EVALUATION.md.

**This strengthened one of the evaluation's recommendations.** Entry 48
suggested the proposed converter should validate a manual's stated module code
against `ProtoModBoardCatalog` rather than just convert it. This typo is proof
that pays for itself: a four-line check would have caught, automatically and
at conversion time, an error that had sat unnoticed in the document and
ultimately took a human to adjudicate.

**Still open, and still blocking this branch from going further:** the manual
describes a power MOSFET with an op-amp feedback loop, a 1 Î© sense resistor,
and ProtoCore's DAC/ADC setting the target and reading back live voltage and
current. The real board is open-loop - bit-banged PWM, 10 Î© sense resistor, no
ADC feedback path. Sections 4 and 5 ask the learner to watch voltage sag on
screen while current holds steady, and this revision can display neither.

**Verification.** Rebuilt clean and re-ran the UI Automation pass: header
reads "Explorers Series Â· MODULE E05", metadata row unchanged (Intermediate /
45-60 min / Simple LED (F02)), the source note explains the typo, 0
placeholders, 2 Tech notes, 1 Observe prompt, 4 discrepancy callouts (all now
about the circuit), the answer-key gate and reveal still work, and a TOC click
still scrolls its target into view (y=601 -> y=209). Confirmed by grep that no
"E02" remains in any rendered learner content - the only occurrences left are
the source filename and the notes explaining the typo.
