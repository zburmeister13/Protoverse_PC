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
