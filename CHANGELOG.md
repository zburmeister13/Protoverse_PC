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
