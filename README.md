# ProtoVerse PC App

A Windows desktop app (WPF, .NET 8) that talks to a ProtoCore board over USB
serial and gives you a live control panel for whichever ProtoMods are plugged
into it — no serial terminal, no hand-decoding hex, no code required to use it.

![Main window, connected to real ProtoCore hardware with an Electronic Load and a Blinky LED installed](docs/screenshots/main-window.png)

## What it does

- **Auto-detects what's plugged in.** Connect, and every populated slot turns
  into a real control panel automatically — no manual configuration.
- **Live control, not just monitoring.** Blinky LED's four LEDs, animation
  pattern, direction, and rate are all controllable from the UI and reflect
  the device's actual state (not a locally-guessed one).
- **Hot-swap safe.** Pull a ProtoMod and plug in a different one while
  connected — the slot updates itself, and one misbehaving module can't take
  down the rest of the app.
- **Works with zero hardware.** A "Simulator mode" checkbox swaps in a fake
  ProtoCore that reports modules and streams plausible telemetry, so the app
  is fully usable for development, demos, or curriculum work without a board
  on hand.
- **Explains itself when something goes wrong.** The Traffic Log records
  every frame sent and received, decoded into plain English — this is what
  actually diagnosed a real firmware freeze bug during development (see
  `CHANGELOG.md` entry 38 for the full story).

![Traffic Log, expanded, showing real hardware exchanges with the Info column decoded into plain English](docs/screenshots/traffic-log.png)

## Module support status

| ProtoMod | Status |
|---|---|
| **Blinky LED** | Fully implemented — real commands, real device-echoed state, no placeholders. |
| **Electronic Load** | Wire format finalized and confirmed against real hardware. No live chart by design — this board has no ADC feedback, so there's nothing measured to plot, only the commanded current and PWM duty it echoes back. |
| **Accelerometer + Temperature** | UI is complete (live charts: temperature trend, an X/Y tilt "bubble level," a Z fill gauge), but the wire payload is still a placeholder pending firmware defining this sensor's real command set. |
| **Basic LED**, others | Identified by firmware but not yet given a panel in this app — shown as an "Unsupported module" placeholder rather than crashing or being hidden. |

See `CLAUDE.md` for the full, currently-accurate story on what's real vs.
provisional, and `CHANGELOG.md` for the chronological history of how it got
there.

## Building and running

Requires the .NET 8 SDK and the ".NET desktop development" workload (Visual
Studio 2022, or `dotnet` from the command line). From the repo root:

```
dotnet build ProtoVerseApp.sln
dotnet run --project ProtoVerseApp/ProtoVerseApp.csproj
```

No hardware needed to try it — check **Simulator mode** in the top bar, then
**Connect**.

## Architecture

```
Serial port (one connection)
  -> SerialService        (owns the port + background read thread — nothing else touches it)
  -> FrameDispatcher       (marshals to the UI thread, routes by ProtoModId, exposes Send/RequestPresence)
  -> ModuleCatalog         (ProtoModId -> panel view model factory - the only place new ProtoMod types get registered)
  -> Panel view models     (built dynamically from PresenceReport, one per detected ProtoMod - each filters for its own ProtoModId)
  -> MainWindow            (ItemsControl bound to the slot collection, one DataTemplate per view model type)
```

`ISerialService` is the seam Simulator mode plugs into — `FrameDispatcher`
talks to the interface, not `SerialService` directly, so `MockSerialService`
can stand in without either side knowing the difference.

## Wire protocol (v1, defined in `Models/ProtocolFrame.cs`)

```
[STX 0x02] [ProtoModId_lo] [ProtoModId_hi] [MsgType] [Length] [Payload...] [Checksum] [ETX 0x03]
```

- **ProtoModId** (`Models/ProtoModId.cs`) — 2 bytes, little-endian, sized for
  a catalog expected to eventually exceed 1,000 ProtoMod types. Fixed
  vocabulary shared with firmware: `0x0001` BlinkyLed, `0x0002` AccelTemp,
  `0x0003` ElectronicLoad, `0x0004` BasicLed, `0xFFE0` Unknown (a slot with a
  valid but uncataloged EEPROM read — distinct from an empty slot), `0xFFF0`
  Core (ProtoCore itself), `0xFFFF` Broadcast (reserved). Add new IDs here
  *and* in firmware together whenever a new ProtoMod type is introduced.
- **MsgType** (`Models/MsgType.cs`) — Command, Response, PresenceRequest,
  PresenceReport, StreamData, Error.
- **Checksum** — XOR of both ProtoModId bytes, MsgType, Length, and every
  payload byte. No CRC, no byte-stuffing/escaping yet — a known, accepted gap
  at the current scale, worth revisiting before wider deployment.
- **Max payload** — 250 bytes per frame. Anything bigger (bulk waveform data)
  should be split across multiple `StreamData` frames rather than growing
  this.
- **Error codes** (payload[0] of an `Error` frame, confirmed against
  firmware's `protocol.h`) — `0x01` NotPresent, `0x02` UnknownMsgType, `0x03`
  BadPayloadLength, `0x04` NotImplemented, `0x05` BadValue.

`ProtocolFrameReader` in the same file is a small state machine that
reconstructs frames from a raw byte stream incrementally, with resync-on-error
(a bad checksum/ETX/oversized Length drops back to scanning for STX) — it
doesn't assume a whole frame arrives in one serial read, since it usually
won't.

### Presence detection

`PresenceRequest`/`PresenceReport` are addressed to `Core`. The report's
payload is a **fixed-size** array — exactly one `ProtoModId` per physical
slot, always in slot order, 2 bytes little-endian each — not a variable-length
list of only the occupied slots; an empty slot reports `ProtoModId.None`
rather than being omitted. (This replaced an earlier skip-empty-slots format
after it caused a real bug: with only one slot occupied, "module in slot 0"
and "module in slot 1" produced an identical single-entry payload.)
`MainViewModel.OnFrameReceived` rebuilds all slots from that fixed list on
every report — sent in reply to a request, and also unsolicited if ProtoCore
detects a hot-swap on its own, so slots update live without needing another
click.

## ProtoMod Library

The **Library** tab (next to **Slots** in the main content area) shows the
whole ProtoMod catalog, not just what's plugged into the ProtoCore right now.
It's a discovery surface: every module is shown at full strength — never
dimmed, locked, or hidden — with one of three connection states:

| Badge | Meaning | Accent |
|---|---|---|
| **Connected now** | In a slot in the most recent `PresenceReport` | green |
| **In your kit** | The user said it's theirs | teal |
| **Connected before** | Seen at some point, not answered for yet | teal |
| **Not in your kit** | The user said it isn't theirs | blue |
| **Not yet connected** | Never seen by this profile | blue |

Two independent facts are tracked per module, and the app is careful not to
conflate them:

- **Has it been plugged in?** Observed automatically from `PresenceReport` —
  the same report that populates the Slots tab, so the two can't disagree.
- **Does the user say it's theirs?** Only ever set by clicking. Seeing a
  board proves it was plugged in, not that it belongs to anyone — a borrowed
  or classroom board is the obvious case. So the first time a board is seen,
  its card asks "Is this ProtoMod part of your kit?" with **Yes, it's mine**
  / **No, just borrowed**, and the answer is always one click from being
  flipped afterwards. Nothing is ever silently claimed on the user's behalf.

### Profiles

Board tracking is per profile, so two people sharing a PC don't merge kits.
The control in the window's top right shows who's signed in, with
**Sign in** / **Switch** / **Sign out**; the picker lets you create a
profile, pick an existing one, or delete one.

**These profiles are not a security feature and aren't meant to be.** There
is no password and no encryption — signing in is picking a name off a list,
and every profile's data sits in one readable JSON file
(`%AppData%\ProtoVerse\accounts.json`, written by
`Services/AccountStore.cs`). Their job is separating one person's tracking
from another's, not keeping anyone out. Don't add a password field later
without making it mean something: a fake login that looks real is worse than
an obviously informal one.

While signed out, the catalog still shows in full — only the tracking is
hidden, and nothing is recorded.

**Tracking is stored app-side, never on ProtoCore.** Putting it in firmware
was considered and rejected (2026-08-31): it belongs to a person, not a
board. A shared classroom ProtoCore would give every student the same wrong
answer, a reflash would silently delete it, and a second ProtoCore would know
nothing about the first. **No wire-protocol or firmware change is involved at
all** — the app already receives every `PresenceReport`, so it simply records
what it already sees. The on-disk document is deliberately close to what a
real server-backed account would send (stable id, display name, flat list of
per-module records), so moving it behind a real account later is a change to
where `AccountStore` reads and writes, not a change to its callers.

The tab is read-only over the wire. It sends no frames and touches no module
control logic.

**Catalog content is hardcoded for v1, on purpose**
(`Models/ProtoModLibraryCatalog.cs`). It should eventually move to a
JSON/manifest source generated from — or shared with — the ProtoMod manual
documents in `PROTOVERSE/Manuals/`, so a new module's catalog entry falls out
of writing its manual instead of being transcribed by hand. The record types
are already flat and JSON-friendly, and `ProtoModLibraryCatalog.Entries` is
the only thing the rest of the app reads, so that swap should be a change to
that one file.

**Nothing in the catalog is invented.** Every description, schematic summary,
project idea, and "leads into" link is quoted from, or directly summarized
from, a document that actually exists — the module manuals in
`PROTOVERSE/Manuals/`, the KiCad schematic exports in
`PROTOVERSE/Finished Modules/`, or this repo's own `CLAUDE.md`. Each entry
carries the source it came from, and the UI displays it. Where no source
material exists (no manual written, no difficulty rated, no documented next
step), the field is left null and the card says "coming soon" rather than
being filled with plausible-sounding text. Two things to know when editing it:

- **Progression links are not inferred.** A "leads into" link only exists
  where a manual actually says one module leads into another. Today that's a
  single link — F01 → F02, from the Blinky manual's "Future ProtoMods for
  you" section. Circuit-code order, series, and difficulty are *not* treated
  as evidence of a teaching sequence.
- **Family is the one field that may be derived.** Every ProtoMod belongs to
  one of three families, keyed by the first letter of its circuit code:
  **F → Fundamentals, E → Explorers, A → Advanced** (confirmed as a product
  rule, 2026-08-31). So a board with no manual written yet still gets a
  correct family. Nothing else may be derived this way.

The filter row above the grid pins the view to one family, with the catalog
split shown in each button's count. "All" is the default — the tab's job is
showing the whole catalog, so filtering is opt-in. Following a "leads into"
link that points outside the active family resets the filter to All, so the
link can never highlight a card that's been filtered out of view.

## Diagnosing a real hardware problem

The Traffic Log (collapsed by default, bottom of the window) is the first
stop when something doesn't behave as expected against real hardware. Every
frame sent and received is shown both as raw hex (nothing hidden) and as a
plain-English decode via `Models/FrameInterpreter.cs` — e.g.
`SetCurrentLimitMa: 100 mA` or `Error: PROTOCOL_ERR_NOT_IMPLEMENTED (0x04)`
instead of a hex blob you'd have to decode by hand. Framing/checksum errors
and disconnect events show up here too, capped at the last 500 entries.

## Adding a new ProtoMod's panel

Because panels are built dynamically from `PresenceReport`, supporting a new
ProtoMod type never touches `MainViewModel` or the slot-population logic:

1. Add the `ProtoModId` (`Models/ProtoModId.cs`) and, in firmware, the
   matching ID.
2. Write the panel view model (extend `ModulePanelViewModelBase`) and its
   view.
3. Register `ProtoModId -> factory` in `ViewModels/ModuleCatalog.cs`.
4. Add a `DataTemplate` for the new view model type in `MainWindow.xaml`.

Until step 3 is done for a given ID, a module reporting that ID shows up as
an "Unsupported module" placeholder instead of a real panel — intentional
degradation, not a bug, since there will always be more cataloged ProtoMod
types than any single app build has panels for.

## More detail

- `CLAUDE.md` — the current, living state of the project: what's settled,
  what's still provisional, gotchas already hit, and platform decisions
  already made.
- `CHANGELOG.md` — a chronological, dated log of every change and why it was
  made.
