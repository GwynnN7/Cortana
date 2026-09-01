# Cortana — developer notes

State of the implementation, the boundaries that matter, and what is left. Written so a new session
can pick this up with nothing but the repository.

---

## 1. Layering

```
Clients (Web, Telegram, Discord, Desktop)     separate processes, no business rules
        │  HTTP + two SSE streams
CortanaKernel/Api                             translates requests into commands and queries
CortanaKernel/Application                     commands, queries, snapshot, AI capabilities
CortanaKernel/Domain                          the rules
CortanaKernel/Infrastructure                  GPIO, sockets, JSON/CSV files, Gemini, web push, systemd
```

`Domain/` depends on nothing but `CortanaLib`. That is now a convention rather than a compiler-checked
boundary — keep it honest: nothing under `Domain/` should reference ASP.NET, GPIO, sockets, a provider
SDK or a client type.

`CortanaLib` is shared by every process: the contracts, `CortanaClient`, and small runtime helpers
(environment, JSON store, shell, machine metrics, media). It holds no rules.

### Dependency notes

- `AutomationEffects` takes a `Lazy<DeviceService>`. Automation switches devices, and switching a
  device raises a fact automation listens to; the laziness breaks that one cycle at composition time.
- `WebPushSink` takes a `Lazy<PushService>` for the same reason.
- Everything else is plain constructor injection from `Program.cs`, which is the only composition root.

---

## 2. Commands, queries, events

- **`AutomationStatus` (Active | Holding | Off) is derived, never stored.** It is projected in
  `AutomationEngine.View()` from the persisted `AutomationEnabled` setting plus whether any device
  hold is live. Keeping it derived is deliberate: a stored `Hold` would survive a restart (holds are
  transient by design), would be global where holds are per-device, and would have to grow a
  sub-state to describe a hold begun during sleep. The dashboard switch reflects the setting; the
  subtitle reflects the status.
- **Holds are facts.** `DeviceHoldChanged` fires when a hold opens and when it expires, and both
  raise a notification, so the clients show state changes through the event system rather than
  through explanatory text in the UI. `LastDecision` still exists but lives only in
  `/automation/diagnostics`, because the AI's "why didn't the lamp turn on?" is built on it.
- **Event schedules** are one-shot by default when the AI creates them (`RunOnce`), because chaining
  one action onto another — "turn the pc on and then reboot into windows" — must not repeat on every
  later power-on. A repeating hook is opt-in and can carry a `MinimumIntervalSeconds` cooldown, which
  is what stops a flapping agent connection from firing `ComputerTurnedOn` twice. The decision lives
  in `ScheduleTiming.ShouldFireOnEvent` so it is testable, and firing claims the schedule under the
  lock so a burst cannot double-fire it.
- **Commands** carry a `CommandOrigin` (`Actor` + `Surface` + `ViaAi`). Clients declare their surface
  with the `X-Cortana-Surface` header; the Kernel decides what that is allowed to do. An action a
  user asked for through the AI stays `Actor = User`, which is what the sleep-wake rules key off.
- **Events** are typed facts on an in-process `EventBus` (`CortanaKernel/Domain/Common/EventBus.cs`).
  Handlers subscribe to a type, never to a string. They are ephemeral: a subscriber that is not
  listening simply misses one.
- **State** reaches other processes as snapshots. `StateBroadcaster` subscribes to *every* event and
  turns it into "there is a newer snapshot"; `/events` then pushes a fresh `CortanaSnapshot`. Missing
  an event is harmless because the snapshot is authoritative.
- `/events/notifications?channel=Telegram|Discord` is the second stream. It replaced the Redis IPC
  the old build used; **there is no Redis anywhere any more.**

---

## 3. The automation engine

`CortanaKernel/Domain/Automation/AutomationEngine.cs` owns every runtime concept that decides whether
Cortana acts. They are deliberately separate values, never one enum:

| Concept | Lives where | Persisted? |
| :--- | :--- | :--- |
| `AutomationEnabled` | a setting | yes |
| `TimeContext` (Day/Night) | derived from `NightHour`/`MorningHour` | no |
| `SleepMode` | engine field | **no**, transient by design |
| Device overrides | engine field | no |
| `SleepHold` | engine field | no |

The engine reads the world through `IAutomationWorld` and acts through `IAutomationEffects`. Those
two interfaces are what keep the domain from depending on the application: without them the engine
would have to call `DeviceService` and `NotificationService` directly, which is also a hard DI cycle.

### Why a tick instead of timers

`Tick()` runs once a second and expires everything time-based: the day/night boundary, the sleep
entry delay, a daytime sleep, the sleep hold, device overrides and the motion window. One cheap tick
is far easier to reason about than the six interacting timers the old build had, and it makes every
expiry reachable from one place.

### Deliberate behaviour changes from the old build

- **Night alone no longer suppresses automatic lighting.** Previously `Night` and `Sleep` were the
  same state, so motion did not light the lamp after `NightHour`. They are now separate: at night,
  before sleep mode is entered, motion still lights the lamp. Nighttime sleep entry is what stops it.
- **The motion timeout follows the computer only**, not "night or computer off" as before.
- **Sleep mode is a real toggle** with its own lifetime, hold and entry delay, instead of a one-way
  "enter sleep" button.
- `ComputerCommand.System` became `BootIntoOtherOperatingSystem`, and the rest of the enum names were
  spelled out, because the AI picks tools by reading them.

---

## 4. Devices

GPIO outputs cannot be read back, so `DeviceRegistry` holds *Cortana's belief*. A failed write leaves
the belief alone; only a successful one moves it. On boot every device is believed OFF and **nothing
is written to the pins** — one of those relays is the desktop's mains supply.

`GpioDeviceController` owns the pin map, and with it the location difference: in Pisa there is no
dedicated lamp line, so Lamp and Generic are the same relay. `Linked()` tells the application which
beliefs move together, so no location knowledge leaks into the domain.

Two sequences worth knowing:

- **Power off with the desktop alive** asks it to shut down, waits for the socket *and* a ping to go
  quiet, then waits `ComputerShutdownGraceSeconds` before cutting the relay. The agent process dies
  before the machine does, so the socket closing alone is not proof.
- **PC ON, for every user-facing purpose, means the desktop agent is connected.** `ComputerPresenceService`
  is what turns a connection into device state, and it also marks Power as ON, since a machine that is
  talking to us obviously has power.

---

## 5. AI

`CapabilityRegistry` is the whole surface the model can reach: ~25 capabilities tagged
`Query`, `Analysis`, `Action` or `Management`. Each one calls the same application command a human
client would. There is no path from a tool to infrastructure.

- Untrusted conversations (`trusted: false`, which Discord sets for anyone but the Chief) get the
  read-only subset, and `AiService.Invoke` refuses a mutating tool even if the model names one.
- `chat` persists to `CortanaKernel/Conversations/<id>.json` and survives a restart. `ask` runs the
  identical pipeline, tools included, and neither loads nor writes history.
- Arithmetic is never left to the model: `AnalyseHistory` runs one of the deterministic reductions in
  `HistoryAnalysis` (average, extremes, value-at, trend, transitions, duration-in-state, worst period,
  compare).
- `ExplainAutomation` returns the engine's own decision ring buffer, so "why didn't the lamp turn on"
  is answered from recorded facts rather than invented.
- `GeminiProvider` is the only file that knows about Gemini. `IAiProvider` speaks in plain messages
  and `AiToolDescriptor`s; swapping providers means one new adapter.

---

## 6. Persistence

Everything lives under `~/.config/cortana`, written through `JsonStore` (atomic: temp file + move).

| File | Contents |
| :--- | :--- |
| `CortanaKernel/Settings.json` | `SettingKey` → string. Enum names are the dictionary keys |
| `CortanaKernel/Ai.json` | `{ values: { AiSettingKey: number }, model }` |
| `CortanaKernel/Network.json` | array of location profiles, selected by matching the live gateway |
| `CortanaKernel/Schedules.json` | persistent schedules |
| `CortanaKernel/Conversations/*.json` | one file per conversation |
| `CortanaKernel/History/YYYY-MM-DD.csv` | one row per sample, pruned by retention |
| `CortanaKernel/Vapid.json`, `PushDevices.json` | web push keys and subscriptions |

Transient by design and never written: sleep mode, sleep hold, device overrides, device states.

**The config format changed from the old build.** `CortanaKernel/Scripts/migrate-config` converts a
legacy tree in place (backing each file up as `*.legacy`) and is idempotent; `--dry-run` shows what
it would do. `Vapid.json` and the history CSVs were already compatible and are left alone.

---

## 7. Clients

- **Web** — Blazor Server. The visual design, `app.css`, the charts and the service worker are carried
  over unchanged; only the data layer was rewritten. `CortanaState` holds the newest snapshot, keeps
  the SSE subscription open, and falls back to polling for a few seconds when the stream drops.
- **Push status notification** — no title (Android renders one badly here), tag `cortana-status`,
  `ongoing`. `PushService` rebuilds it from live state on every event with a 300 ms coalesce, and a
  five-minute timer exists only as a safeguard. An accepted event replaces the body for
  `PushEventSeconds`, then the newest status is rebuilt — never restored from cached text. The status
  line is `Online · 💡🖥️🔌 · 🔆|💠|💤 💨 21.4°`, with the emoji taken from the old build.
- **Telegram** — one updating menu per topic (`Menu` + `LiveMenu`). One pending input per topic at a
  time. Notifications arrive on the notification stream.
- **Discord** — slash commands only. **Voice is deliberately absent**: no `VoiceService`, no
  `libdave.so`, no meme playback. `/home` carries the house, and a mention opens a short conversation
  window during which Cortana keeps answering untagged.
- **Desktop** — `Agent` (resident socket, JSON-line protocol, metrics push) plus `Cli`. Three
  conversational modes are distinct: `cortana chat` (interactive persistent), `cortana chat "msg"`
  (one-shot into the same persistent conversation) and `cortana ask "..."` (stateless).
  `DesktopOs.BestMatch` resolves misspelled application names before launching or closing anything.

---

## 8. Protocols

- **ESP32 → Kernel**: unchanged from the old build, so the firmware does not need reflashing. It sends
  `esp32` then bare JSON objects with no delimiter; frames are cut by counting braces.
- **Desktop agent ↔ Kernel**: one JSON object per line in both directions. Agent sends `computer`,
  then `{"type":"ping"|"reply","id","text"}`; the Kernel sends `{"id","command","argument"}`.
  Replies are correlated by id, so several commands can be in flight.
- **API**: every route answers plain text when `Accept: text/plain`, JSON otherwise. That is what lets
  the bots and the CLI stay thin.

---

## 9. Testing

There is no automated test project: the dashboard is the intended way to exercise the system. The
logic that was covered by tests during the rewrite — the sleep transition table, hold lifetimes and
wake sources, schedule timing, settings validation and the deterministic analyses — is written as
pure functions (`AutomationRules`, `ScheduleTiming`, `HistoryAnalysis`, `SettingsStore`) so it can be
covered again cheaply if that changes.

The Kernel does check one thing at boot: it materialises the whole route graph and refuses to start
if any endpoint signature is invalid, because that failure otherwise turns every request into a 500.

---

## 10. Known limitations and remaining work

- Notification text is written short because it becomes the push overlay body verbatim; the overlay
  additionally trims anything past 80 characters.
- The station has two temperature sensors with distinct jobs: the SHT4x is the reported room
  measurement, and the AHT20 exists only to compensate the ENS160. The AHT20 reading is forwarded as
  `airQualityTemperature` purely so the two can be compared while calibrating the offset; it is never
  presented as a room reading.
- The temperature offset is applied at ingest, so it corrects live readings and everything recorded
  from that moment on; history written before it was set keeps the old values.
- **Not yet run against real hardware.** Everything compiles and the rules are tested, but the GPIO
  writes, the ESP32 socket, the desktop agent handshake and the deploy have not been exercised on the
  Pi. `migrate-config` has been tested against copies of the live config but not run on the Pi.
- Device overrides are per-device with one shared duration, as specified. If automation ever controls
  more than the lamp, per-device durations become worth having.
- `HistoryAnalysis.WorstPeriod` is O(n²) over the window. Fine for a few thousand samples, worth an
  index if retention grows a lot.
- The Discord `/games` module needs `CORTANA_IGDB_*`; without them the command answers that it is not
  configured rather than failing.
- `yt-dlp` is required for anything YouTube. There is no fallback, on purpose.
