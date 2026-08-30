# ProtoVerse PC App — scaffold

A single WPF (.NET 8) window that talks to ProtoCore over serial and shows one
stacked panel per ProtoMod. Built to match the architecture we worked through:

```
Serial port (one connection)
  -> SerialService        (owns the port + background read thread — nothing else touches it)
  -> FrameDispatcher       (marshals to the UI thread, routes by ProtoModId, exposes Send/RequestPresence)
  -> ModuleCatalog         (ProtoModId -> panel view model factory - the only place new ProtoMod types get registered)
  -> Panel view models     (built dynamically from PresenceReport, one per detected ProtoMod - each filters for its own ProtoModId)
  -> MainWindow            (ItemsControl bound to the slot collection, one DataTemplate per view model type)
```

## How to open it

This was written as source, not built/tested here (no .NET SDK or network access
to NuGet in this sandbox) — open `ProtoVerseApp.sln` in Visual Studio 2022 with the
".NET desktop development" workload installed, and it should restore
`CommunityToolkit.Mvvm` from NuGet and build. If anything doesn't compile cleanly on
first try, it'll most likely be a small XAML namespace/binding typo — flag it and
I'll fix it.

## Wire protocol (v1, defined in `Models/ProtocolFrame.cs`)

```
[STX 0x02] [ProtoModId] [MsgType] [Length] [Payload...] [Checksum] [ETX 0x03]
```

- **ProtoModId** (`Models/ProtoModId.cs`) — fixed vocabulary shared with firmware:
  `0x01` BlinkyLed, `0x02` AccelTemp, `0x03` ElectronicLoad, `0xF0` Core (ProtoCore
  itself, used for slot identification). Add new IDs here *and* in firmware together
  whenever a new ProtoMod type is introduced.
- **MsgType** (`Models/MsgType.cs`) — Command, Response, PresenceRequest,
  PresenceReport, StreamData, Error.
- **Checksum** — XOR of ProtoModId, MsgType, Length, and every payload byte. No CRC,
  no byte-stuffing/escaping yet — fine at this scale, worth revisiting if streamed
  data (e.g. a DDS sweep capture) turns out to need more robustness.
- **Max payload** — 250 bytes per frame. Anything bigger (bulk waveform data) should
  be split across multiple `StreamData` frames rather than growing this.

`ProtocolFrameReader` in the same file is a small state machine that reconstructs
frames from a raw byte stream incrementally — it doesn't assume a whole frame arrives
in one serial read, since it usually won't.

## "Identify slots"

Sends a `PresenceRequest` addressed to `Core`. ProtoCore is expected to reply with a
`PresenceReport` (also addressed to `Core`) whose payload is a list of the
`ProtoModId` bytes currently present (read from each ProtoMod's EEPROM, which you
already have working). `MainViewModel.OnFrameReceived` rebuilds the three slots from
that list: each present `ProtoModId` becomes a real panel (via `ModuleCatalog`, or an
"Unsupported module" placeholder if this build doesn't have a panel for that type),
and any slot left over becomes "Empty." The same handling also fires on an unsolicited
`PresenceReport` (ProtoCore can send one on its own if it detects a change), so a
hot-swap while connected updates the slots without needing another click.

`Connect` fires this automatically now (as of the fix logged in `CHANGELOG.md` entry
13) so slots populate right after connecting without an extra step - the "Identify
slots" button still exists for a manual re-query, e.g. re-checking presence after a
hot-swap without waiting for ProtoCore's unsolicited report.

## What's a placeholder vs. what's real

**Real / meant to stay as-is:**
- The frame format, checksum, and the reader state machine.
- `SerialService` / `FrameDispatcher` split — single connection, single writer, safe
  UI-thread marshalling.
- `ModulePanelViewModelBase` — the per-module filtering pattern.
- The stacked, `ItemsControl`-driven layout (see below for why).

**Placeholder — update once each ProtoMod's actual command set is defined:**
- Blinky LED: `CmdSetState` / `CmdSetBlinkRateMs` sub-command bytes and the response
  layout (`OnFrameReceived` in `BlinkyLedViewModel.cs`).
- Accel+Temp: the whole payload layout in `AccelTempViewModel.OnFrameReceived` —
  right now it assumes 1 byte temp + 3×int16 accel, which is a guess.
- Electronic Load: same story in `ElectronicLoadViewModel.cs`.
- `PresenceReport` payload shape (currently: flat list of ProtoModId bytes — simple,
  but confirm it matches what the firmware actually sends).

## Adding a new ProtoMod's panel

Because panels are built dynamically from `PresenceReport` (see above), supporting a
new ProtoMod type never touches `MainViewModel` or the slot-population logic. It's:

1. Add the `ProtoModId` (`Models/ProtoModId.cs`) and, in firmware, the matching ID.
2. Write the panel view model (extend `ModulePanelViewModelBase`) and its view.
3. Register `ProtoModId -> factory` in `ViewModels/ModuleCatalog.cs`.
4. Add a `DataTemplate` for the new view model type in `MainWindow.xaml`.

Until step 3 is done for a given ID, a module reporting that ID shows up as an
"Unsupported module" placeholder instead of a real panel - that's intentional
degradation, not a bug, since there will be many more ProtoMod types over time than
any single app build has panels for.
