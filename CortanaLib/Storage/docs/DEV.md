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
- **The motion timeout is one number, not one per computer state.** `MotionTimeoutComputerOnSeconds`
  and `MotionTimeoutComputerOffSeconds` were a crude proxy for "is anyone there"; real presence
  signals replaced them with a single `MotionTimeoutSeconds`.

  `AutomationRules.Present` is `deskActive || MotionActive(...)`: using the computer holds the lamp
  on, and everything else falls back to the plain PIR timeout.

  `DeskActive` requires `computer.Connected && Locked: false && IdleSeconds: 0` — a *positive* report
  of not-idle, never merely the absence of an idle signal. `IdleSeconds` is `-1` (unknown) whenever
  idle cannot be observed: no idle file, **no running idle daemon** (`hypridle`/`swayidle` — checked
  every poll, so a dead daemon degrades to plain motion instead of pinning the lamp on with a stale
  file), or **an idle inhibitor is held**. That last case matters —
  `systemd-inhibit --what=idle --mode=block` stops hypridle reporting idle at all, so treating "no
  idle signal" as "at the desk" would keep the lamp on forever after leaving with the inhibitor on.
  Unknown therefore falls back to plain motion, which is the safe direction.

  The four states: at the desk → no timeout; idle, locked, inhibited, or computer off → plain motion
  timeout. The lamp can still switch *on* from motion and lux in every one of them.
- **Sleep mode is a real toggle** with its own lifetime, hold and entry delay, instead of a one-way
  "enter sleep" button.
- `ComputerCommand.System` became `BootIntoOtherOperatingSystem`, and the rest of the enum names were
  spelled out, because the AI picks tools by reading them.

### Mood and activity

Two derived read-only values sit next to the engine, both computed fresh per snapshot and never stored.

`Domain/Automation/MoodRules.cs` reduces the whole house to one word — `Watching`, `Quiet`,
`Concerned`, `Resting`, `Alone` — with `Explain()` giving the sentence behind it. Every word describes
**Cortana**, not the user: `Quiet` means she is holding back because the desk is busy, which is why it
is not called `Busy`. `Explain()` reaches clients as `CortanaSnapshot.MoodReason`, so the word is
never unexplained: it is the status pill's tooltip and the first section of the logs page.

Mood drives the push status line, the top-right status pill (which carries the online dot and links
to the logs) and the AI's opening context.

`Domain/Activity/ActivityRegistry.cs` holds what the desktop is doing. It has **two independent axes**,
which is the whole point: `Category` is the focused window, `Playing` is whatever MPRIS is playing.
Coding with music on is `Coding` **and** a track — one field could not say that, and folding music into
`Category` would have made background music invisible.

Two things read it:

- `ActivityRules.DoNotDisturb` — a fullscreen game or film. It gates exactly one thing:
  `DeviceService.CommandComputer` drops a non-user `ComputerCommand.Notify`, so nothing pops up on the
  desktop mid-game. Push to the phone, the web log, Telegram and Discord are all untouched, and a
  notification the user explicitly asked for still goes through because `origin.IsUser` bypasses it.
  Background music never triggers it — DND requires fullscreen.
- `MoodRules` — fullscreen gaming or media reads as `Quiet` regardless of CPU load.
- `AutomationEngine` — while anything is fullscreen the engine holds: the lamp decision returns "no
  action" and `AutomationStatus` reads `Holding`. A manual lamp change during a film therefore sticks.
  This hold has no expiry, so `HoldingUntil` is null and the dashboard's Resume button is hidden —
  `FullscreenHold` on `AutomationView` is what tells the two kinds of hold apart.

The privacy boundary is the agent, not the Kernel: `CortanaDesktop/Activity.cs` maps the window class
to a category on the desktop. **Window titles never leave the machine, at any setting.** It also only
names a class that is in the map, because an unmapped class can itself be sensitive — a browser-made
web app encodes its site host in the class, which is how the first live run shipped a Tailscale
hostname to the Pi.

`~/.config/cortana/activity.conf` holds `class = category` lines plus one `detail =` line — the §11
privacy dial, `CategoryOnly` | `GameTitles` | `NowPlaying`. **It defaults to `NowPlaying`**, on the
grounds that the destination is the user's own Pi on their own LAN and track titles are the point of
the feature. `detail = CategoryOnly` turns off both the app name and the track.

The agent is also where the debouncing lives. It listens on Hyprland's `socket2` and reads
`playerctl --follow`, collapses bursts into one evaluation every 750 ms, and sends only transitions
plus a 5-minute heartbeat. That matters
because `StateBroadcaster` turns every bus event into a snapshot rebroadcast; wiring a raw compositor
event stream into the bus would melt the Blazor clients. The broadcaster's bounded(1)/`DropWrite`
channel and the SSE coalesce delay are the second line of defence, not the first.

### Baselines

`Domain/History/HistoryBaseline.cs` answers "is this normal for this hour" instead of "is this above a
number". It buckets the last few weeks of a metric by hour-of-day and reduces to a **median and a MAD**
(scaled by 1.4826 to read like a standard deviation) — both robust, so one bad afternoon does not move
the baseline the way a mean would. Below eight samples in the bucket it says so rather than guessing.

Reached as `GET /history/{metric}/usual` and as the `CompareToUsual` AI capability, which is the point:
the model gets "clearly higher than usual" as a computed fact rather than inventing the judgement.

Two caveats worth knowing: `pc_gpu` baselines are skewed until the old raw-utilisation samples age out
of the window, because effective load reads much lower; and a metric added to the CSV mid-day is
unreadable for the rest of that day, since `Read` resolves columns from the file's own header and
`Append` only writes a header for a fresh file.

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
  Replies are correlated by id, so several commands can be in flight. The agent also pushes
  `{"type":"activity","activity":{...}}` on transitions.
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
- **`Wire.begin()` must never be left to the board variant.** The sketch probes 21/22, then 13/16,
  then 13/33, and only falls back to the variant default. This is not defensive padding: flashing with
  `esp32:esp32:esp32-poe-iso` put I²C on **SDA 13 / SCL 16** while the sensors are wired for the
  generic ESP32's **21/22**, so every I²C device failed `begin()` while the GPIO PIR on pin 23 kept
  working perfectly. The tell was `light = -2`, which is BH1750's documented "sensor not configured"
  sentinel rather than a reading. Changing the FQBN silently moves the bus; the probe makes the sketch
  independent of which board definition it is compiled for.
- **The ESP32 has not been reflashed since the firmware changed.** `airQualityTemperature` and
  `WiFi.setSleep(true)` only take effect after `cortana flash` is run **on the Pi**, where
  `arduino-cli` is installed. Everything else — GPIO, the station socket, the agent handshake, the
  deploy and `migrate-config` — has been exercised against the real Pi.
- Device overrides are per-device with one shared duration, as specified. If automation ever controls
  more than the lamp, per-device durations become worth having.
- `HistoryAnalysis.WorstPeriod` is O(n²) over the window. Fine for a few thousand samples, worth an
  index if retention grows a lot.
- The Discord `/games` module needs `CORTANA_IGDB_*`; without them the command answers that it is not
  configured rather than failing.
- `yt-dlp` is required for anything YouTube. There is no fallback, on purpose.
- `Locked` comes from `qs -c caelestia ipc call lock isLocked`, polled every 10s and only when a `qs`
  process is actually running.
- Idle arrives from outside: `cortana idle on|off` writes `$XDG_RUNTIME_DIR/cortana/idle`, and the
  agent turns that into `Away` with `IdleSeconds` measured from the file's mtime. This exists because
  Wayland exposes idle only through `ext-idle-notify-v1` and logind is no help — `IdleAction=ignore`
  and nothing maintains the session `IdleHint`, so systemd holds no idle counter to read. An
  `ext-idle-notify` client such as `hypridle` calls the wrapper on timeout and resume.
- The agent is Hyprland-first, not Hyprland-only. With `HYPRLAND_INSTANCE_SIGNATURE` set it reads
  socket2 and `hyprctl`; without it (the CachyOS gamescope session) it falls back to probing for a
  `gamescope` process or a `steamapps/common` binary and reports `Gaming` fullscreen. `playerctl` and
  the lock query work in both.
- `activity` and `music` were appended to the history columns. `Read` resolves each column from the
  file's own header, so older CSVs stay readable and simply lack the two; the day the change lands
  keeps its old header until midnight rollover, so those two are blank for the rest of that day.
- Mood still has **no behavioural consumer** — it is display text and LLM context only. Activity has
  exactly one, the do-not-disturb gate above.
- The activity map is Hyprland-only. On any other compositor `Activity` stays null, which every
  consumer already treats as "unknown" rather than "idle".
