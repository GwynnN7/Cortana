# Cortana — developer notes

How the system is built, for whoever picks it up next. `FILES.md` says what each file is;
this says how the pieces find each other

---

## 1. The shape

Seven projects, one solution:

| Project | What it is |
| :--- | :--- |
| **CortanaLib** | Shared vocabulary: contracts, primitives, the HTTP client, runtime helpers. Everything references it, it references nothing |
| **CortanaKernel** | The only process that owns state. Talks to the hardware, decides things, exposes an HTTP API |
| **CortanaWeb** | Blazor Server dashboard |
| **CortanaTelegram** | Telegram bot |
| **CortanaDiscord** | Discord bot |
| **CortanaDesktop** | Agent on the PC, and the CLI |
| **CortanaEmbedded** | ESP32 station firmware |

**The clients are thin on purpose.** None of them holds state or logic: they read a snapshot and post
commands. That is why adding a client (an Android app, say) needs no Kernel work — the API is the
whole contract. The same applies inside the Kernel: memory, history and the fabric are services, so
anything that can reach them, including the language model, gets them for free

## 2. Layering inside the Kernel

```
Api  →  Application  →  Domain  ←  Infrastructure
```

Dependencies point inward. The rule that keeps it honest:

- **Domain** knows nothing about HTTP, sockets, files or JSON. Pure rules and in-memory state
- **Application** orchestrates. It is where a command becomes several domain calls plus an event
- **Infrastructure** implements the interfaces Domain declares — GPIO, sockets, files, the model
- **Api** is transport only. No decisions

When Domain needs something from the outside it declares an interface and Infrastructure implements
it: `ILocalDeviceController`, `IComputerEndpoint`, `IServiceSupervisor`, `IAiProvider`, and every
`I*Repository`. This is also why the automation engine takes `IAutomationWorld` and
`IAutomationEffects` rather than the services themselves — without them the engine would depend on
`DeviceService`, which depends back on the engine

## 3. How things are wired: dependency injection

Nothing constructs its own collaborators. `Program.cs` registers every type once, and the container
hands each class what its constructor asks for. **Everything is a singleton** — there is one house, so
there is one of each service for the life of the process

Reading a class's constructor tells you exactly what it can touch, and nothing else is reachable

```csharp
public sealed class SensorService(
    Fabric fabric,          // where readings live
    RoomState room,         // derived room facts
    SettingsStore settings, // thresholds
    NotificationService notifications,
    IEventBus bus)
```

**Interfaces are registered to implementations**, which is how Infrastructure gets substituted without
Domain knowing: `AddSingleton<ILocalDeviceController, GpioDeviceController>()`. Where one object must
be visible under two types, it is registered once and aliased, so both names resolve to the *same*
instance:

```csharp
AddSingleton<RaspberryHost>();
AddSingleton<IHostMachine>(provider => provider.GetRequiredService<RaspberryHost>());
```

### `Lazy<T>` and why it appears

Some dependencies are genuinely circular: `AiService` needs `CapabilityRegistry` to know what it can
do, and capabilities call back into services. `DeviceService` and `AutomationEngine` need each other.
`Lazy<T>` breaks the construction cycle — the container builds the wrapper immediately and resolves
the real object on first use, by which time everything exists

`Lazy<T>` must itself be registered. **A missing `Lazy<T>` registration compiles fine and crashes at
startup**, because DI resolves at run time, not compile time. If the Kernel dies immediately with
`Unable to resolve service for type 'System.Lazy\`1[...]'`, that is what happened

### Hosted services

Anything with a loop is a `BackgroundService` registered twice — once as itself so others can inject
it, once as a hosted service so the host starts it:

```csharp
AddSingleton<PushService>();
AddHostedService(provider => provider.GetRequiredService<PushService>());
```

The loops are `MetricsService`, `AutomationService`, `ScheduleService`, `HistoryService`,
`ModelCatalogue`, `PushService`, `VolitionService` and `ConnectionServer`

## 4. How things talk

Three mechanisms, each for a different job:

**Direct calls, inward.** An `Api` endpoint calls an `Application` service, which calls `Domain`.
Synchronous, returns a `Result<T>`

**The event bus, for facts.** `IEventBus` is a typed in-process publish/subscribe. A service that has
just changed something publishes a record — `DeviceStateChanged`, `MotionDetected`,
`NotificationRaised` — and anything interested subscribes. Events are **facts that already happened**,
never requests. They are ephemeral: nothing is persisted or replayed, so a subscriber that was not
listening simply missed it

This is what stops the services knotting together. `PushService` does not need to be called by
everything that changes state; it subscribes to the bus and reacts

**Snapshots, outward.** Clients never subscribe to events — they cannot, they are separate processes.
`SnapshotService.Build` assembles the entire visible state into one `CortanaSnapshot`, and
`StateBroadcaster` turns every bus event into "there is a newer snapshot" on an SSE stream at
`/events`. Clients re-read and re-render

`StateBroadcaster` uses a bounded(1) channel with `DropWrite`, so a burst of events collapses into a
single notification rather than a flood. This matters more than it sounds: the station reports every
five seconds, and anything wired directly to the bus inherits that rate

## 5. The fabric: hardware, and what you make of it

Nothing about the hardware is hard-coded, and there are two separate ideas that used to be one.

**Hardware declares channels, and nothing else.** A source announces an id, a kind, and lists of
output and input **tags**. No names, no units, no icons — those are not hardware facts:

```
SourceDescriptor { Id, Kind, Outputs[], Inputs[] }
```

```
raspberry  outputs: pin23, pin24, pin25
station    inputs:  motion, light, temperature, humidity, co2, tvoc, air_temperature
```

**You create virtual devices and sensors on top.** A channel is only a possibility until something is
registered on it:

```
VirtualDevice { Id, Name, IconOn, IconOff, Channels[] (source + channel), Pulse, PoweredBy, InStatus }
VirtualSensor { Id, Name, IconHigh, IconLow, Source, Channel, Unit, Kind,
                Min, Max, Offset, FeedsPresence, InStatus }
```

Only a boolean sensor has two icons to draw; a number carries one and says the rest with its value.
`Min` and `Max` are what make it render as a bar instead of a tile, and `InStatus` is what puts it on
the phone's persistent notification

**Ids are internal.** Every surface resolves a device or a sensor by **id or name**, and the AI is
shown names, because a person asks for the speakers and not for `generic`

**A source also says what it *is*.** Readings are what a source measures; **facts** are what it is:

```
SourceFact { Key, Value }        name, os, uptime, memory, disk, ip, signal…
```

Nothing validates the keys — a source offers whatever it can. The Pi and the desktop give name, os,
uptime and the absolute GB; the station gives its name, chip, uptime, IP and signal. They arrive in
the `hello` and are refreshed by a `{"type":"facts","values":{…}}` message, and they are live rather
than persisted: a source that goes away stops describing itself.

That replaced `MetricsRegistry`, which was a second store of the same numbers that only two machines
could ever use. The numbers that move are sensors; the ones that describe are facts; there is no
`MetricsView` any more, and a machine card is just a source with its facts and its sensors

This is the part worth understanding: **a channel is not automatically a device.** A Pi exposing three
pins where two could drive the same lamp means you register one device on the pin you actually wired.
An unregistered channel shows as `free` and does nothing

**A device is on while any of its channels is on**, and `Partial` says when only some of them are.
Any-on is deliberate: toggling a half-lit room turns it *off*, which is the safe direction, where
all-on would read it as off and toggling would switch everything on

**A device can span several channels, and channels can be shared.** That is what removed the special
case for the room:

```
raspberry/pin23 (output): Power, Room
raspberry/pin24 (output): Generic, Room
raspberry/pin25 (output): Lamp, Room
```

`Room` is an ordinary virtual device holding all three, with no code of its own anywhere — there is no
`SwitchRoom` service, route, capability or schedule action left

**State belongs to the channel, not the device.** Two devices sharing an output can never disagree
about it, and a device reads `On` while any of its channels is on. It is persisted in `Channels.json`
and re-asserted on boot, because a GPIO output cannot be read back

**The desktop is found, not named.** `Fabric.Machine` is the device registered on a `Computer`-kind
source, and the supply to cut is its `PoweredBy`. Wake-on-LAN and the shutdown-then-cut sequence hang
off those two, not off the ids `computer` and `power`

**Channels are shared freely.** Several virtual devices may sit on the same channel, and one device
may span several — a `Room` covering every relay, or curtains driven by two motors. Switching a device
writes every channel it holds, and any other device sharing those channels has its state updated to
match

**Readings arrive keyed by channel tag**, and the fabric maps `(source, tag)` to whichever virtual
sensor claims it. A source gaining a channel therefore cannot break the frame, and an unclaimed
channel is simply ignored

**Sources declare, they are not configured.** `Announce` replaces whatever a source said before and
persists it. `Dropped` removes a source from the liveness map **only** — its channels and everything
registered on them stay, and simply read offline

The Raspberry is the exception the design allows for: it has no link to speak over, so it declares its
pins through `Pins.json`, falling back to the header pins it knows about

**Binds are how sensors reach devices:**

```
Bind { Id, Device, Triggers[], Mode (All|Any), Enabled, HoldsOnManualAction }
  Trigger { Sensor, Kind (IsTrue|IsFalse|Below|Above|Outside|Changed), Low, High, Sustains }
```

**A trigger with no reading suspends the bind.** Not "false" — suspended. The device is left exactly
as it is and the decision carries `waiting on <sensor>`. This matters because sensors can disappear:
the station drops, or a service that publishes one is disabled. Treating absence as false would make a
sustaining trigger switch the device *off* whenever a sensor went quiet, so a disabled sleep service
would turn the lamp off rather than leave it alone

`Sustains` is the subtle flag. The lamp must *stay* on while someone is present, but only needs
darkness to come *on*. So the presence trigger sustains and the light trigger does not:
`BindRules.Decide` switches off when any sustaining trigger fails, and switches on only when every
trigger is satisfied. That reproduces hand-written hysteresis for any device

**Machines are sources too.** The desktop agent publishes `cpu`, `cpu_temp`, `gpu`, `gpu_temp`,
`gpu_power`, `ram`, `disk`, plus `at_desk` and `locked`; the Kernel publishes the same machine
readings for the Pi. So "warn when the GPU passes 80 °C" is an ordinary warning, and nothing about it
is special-cased

**Presence is composed, not hardcoded.** Any sensor marked `FeedsPresence` contributes. Today that is
the PIR and the desktop's `at_desk`, which is why sitting still at the computer keeps the lamp on
without the engine knowing what a desk is. Two motion sensors can therefore be split: one feeding
presence, one driving a fan through its own bind

`AutomationRules.Present` is `live || MotionActive(lastMotionAt, now, timeout)` — a level sensor such
as `at_desk` holds presence directly, while a momentary one such as the PIR is carried by the timeout

**The Kernel is a source as well** (`kernel`), publishing the composed `presence` so binds can
reference it like any other reading

### Warnings

Nothing about "air quality" is special. A **warning** watches one or more sensors, each with its own
threshold, and is stored in `Warnings.json` like any other configuration:

```
Warning { Id, Name, Message, Triggers[], Level, CooldownMinutes, Enabled, Icon, InStatus }
```

A warning takes the **same `Trigger` as a bind**. A sustaining trigger gates it — every one has to
hold — and the rest raise it, so "only when somebody is here" is `presence IsTrue` rather than a flag
of its own, and any sensor can gate any warning

Hysteresis is fixed rather than per-warning: it fires at the threshold times `1.15` and clears only
when **every** raising trigger is back under `0.9`. Firing on any trigger but clearing on all of them
is deliberate — one sensor recovering should not silence a warning another is still raising

### The handshake

One protocol for every source, newline-delimited JSON, every message typed:

```json
{"type":"hello","magic":"cortana","version":1,"source":"station","kind":"Station",
 "outputs":[],"inputs":["motion","light","temperature"]}

{"type":"reading","values":{"motion":1,"light":21,"temperature":28.7}}
```

The Kernel replies `{"type":"welcome","accepted":true}` and thereafter treats the connection as that
source. `version` exists so the protocol can change without a flag day; a source claiming a higher
version is logged rather than rejected

The desktop agent speaks the same protocol, which is why it is an ordinary source that happens to
offer one output. A client that does not announce itself is refused: there is no legacy prefix left

Outputs are not Pi-only. `IChannelWriter` is chosen per source, so the GPIO header and a station that
announces outputs are two implementations of the same seam, the latter sending `SourceCommand` back
down the socket that carries its readings

## 6. Automation

`AutomationEngine` owns everything that decides whether Cortana acts, as separate values rather than
one enum:

| Concept | Where | Persisted |
| :--- | :--- | :--- |
| `AutomationEnabled` | a setting | yes |
| `TimeContext` (Day/Night) | derived from `NightHour`/`MorningHour` | no |
| `SleepMode` | engine field | no, transient by design |
| Device holds | engine field | no |
| `SleepHold` | engine field | no |

Sleep is no longer part of it. `SleepEngine` owns that state machine and reaches the rest of
automation through an `ISleepHost` seam — the time context, whether automation is on, clearing holds,
and asking for a re-evaluation. It never touches automation's internals. It publishes `sleep` as a
sensor, so the lamp's sleep gate is an ordinary sustaining trigger on the bind rather than a branch
inside `Evaluate`

`Evaluate` loops every bind. The gates it applies around them — automation off, sleep mode, a manual
hold, a busy desktop — are the parts that were never about one lamp

**A tick, not timers.** `Tick()` runs once a second and expires everything time-based: the day/night
boundary, the sleep entry delay, a daytime sleep, the sleep hold, device holds and the motion window.
One cheap tick is easier to reason about than six interacting timers, and every expiry is reachable
from one place

**Presence is composed, not measured.** Every sensor marked `FeedsPresence` feeds it — the PIR, being
at the desk — and it lingers for `MotionTimeoutSeconds` after the last goes quiet. `at_desk` requires a
*positive* report of not-idle, never merely the absence of an idle signal: `IdleSeconds` is `-1` when
idle cannot be observed, and a `systemd-inhibit --what=idle` lock stops hypridle reporting, so
treating silence as presence would pin the lamp on after leaving with the inhibitor set

**A dropped source loses its readings.** `Fabric.Dropped` clears them, or the last value of a source
that went away is believed for ever — `at_desk` stayed true all night with the computer off, and
carried presence with it

**Evaluate is serialised.** Readings, the tick, the computer and the endpoints all reach it from their
own threads, so without one lock around the pass two of them can both see a device off and both
switch it on

## 7. The assistant

**Capabilities.** `CapabilityRegistry` is every action the model can take, each one calling an
ordinary application command. Kinds are `Query`, `Analysis`, `Action`, `Management`; `IsReadOnly` is
`Query or Analysis`, and untrusted callers get only those. **Anything reading the owner's memory must
be `Management`**, not `Query` — a guest was once able to read stored preferences because `Recall` was
registered as a query

**Mood** reduces the house to one word, computed fresh per snapshot and never stored. Every word
describes *Cortana*, and most of them follow what the owner is doing: `Happy` at the desk, `Watching`
when something is fullscreen or a game is running, `Bored` when the desk is locked or the computer has
been off 45 minutes, `Alone` when nobody has been around for hours, `Resting` in sleep mode, `Worried`
on a warning or a service down. The nominal state is one of `Calm`/`Friendly`/`Helpful`, picked on
entering the state and held until it leaves, because rolling it per snapshot would flicker every few
seconds. `Happy` left that rotation once it started meaning something

**Memory** has two horizons. `Fact`, `Preference` and `Event` are permanent and announced when
stored. `State` is where the owner is right now: it expires, a new one replaces the old, and it is
stored without ceremony. `Prune` removes only expired entries — nothing decays for going unused

**Volition** is the one place she speaks unprompted. Deliberately almost empty: a persisted `Quiet`
period, a once-daily greeting inside a window after `MorningHour`, a once-daily wrap-up at
`WrapupHour`, and everything logged. Both go through `AiService.Compose(brief, fallback)`, which falls
back to a plain line when no model answers, and both read `HistoryService.Digest` rather than naming a
metric. The wrap-up is always written as a short-term memory and only *said* with probability
`WrapupChance`

**Features are switchable.** `PluginService` is the one list of what she runs, the switch behind each
one, and whether it can be switched at all. It is carried in the snapshot, so a page can say it is off
rather than pretend

**Baselines and correlation** make "unusual" computable. `HistoryBaseline` buckets a metric by
hour-of-day into a median and MAD, both robust so one bad afternoon does not move it.
`HistoryCorrelation` joins metrics on their shared row timestamp — exact, not interpolated, because
every metric is written in the same CSV row

## 8. Persistence

Everything the Kernel keeps lives under `~/.config/cortana/CortanaKernel/`, one JSON file per concern
(`Sources.json`, `Registrations.json`, `Channels.json`, `Binds.json`, `Warnings.json`, `Settings.json`,
`Ai.json`, `Layout.json`, `Memory.json`, `Notes.json`, `Volition.json`, `Schedules.json`), plus one CSV
per day under `History/`

**A day is reduced and kept.** `HistoryService.Summarise` turns one day of samples into a `DaySummary`
— when presence and the computer rose and fell, minutes per activity category, per device and each
sensor's average — appended to `Days.json` at midnight and again at the wrap-up. `Rhythm(metric)`
compares today with the median of the same weekday, which is what makes "two hours earlier than usual
for a Tuesday" computable rather than a guess

Transitions, not first-seen: a day that starts with the computer already on has no "came on" moment,
and recording one at 00:00 would poison the median. `DayRhythm.Rose`/`Fell` look for the edge

**Both enum-keyed stores read their file key by key.** `Settings.json` and `Ai.json` are maps from an
enum name to a value, and deserialising the whole map fails outright when a member is removed, which
silently resets every setting to its default. Each unknown name is now skipped with a log line
instead. Never remove an enum member from `SettingKey` or `AiSettingKey` without checking this

**Shipped defaults reach a live install only for registrations.** `Fabric.Seed` is additive, so a new
default sensor appears. Bind and warning *contents* are never migrated, because rewriting someone's
triggers is presumptuous — so `BindStore.Adrift` reports which shipped ones differ and `Restore` puts
one back on request. That is the only path an updated default has

**`Channels.json` is what the outputs were last written to.** A GPIO output cannot be read back, so
the last written value is persisted per channel and re-asserted on boot by `DeviceService.Restore`.
`GpioDeviceController.Dispose` deliberately does **not** close its pins: closing a line releases it,
and a released line stops holding its relay, so shutting the Kernel down would switch the house

`JsonStore` writes through a temporary file, so a crash mid-write cannot truncate a config

History reads resolve columns from **each file's own header**, so adding a metric leaves older files
readable and simply missing it. The day a column is added keeps its old header until midnight rollover

## 9. Protocols

- **Any source → Kernel**: the announcement above, then `{"type":"reading","values":{…}}`. The station
  sends bare JSON objects with no delimiter, so frames are cut by counting braces
- **Desktop agent ↔ Kernel**: one JSON object per line both ways. The agent announces, then sends
  `{"type":"ping"|"reply"|"activity"|"reading"}`; the Kernel sends `{"id","command","argument"}`.
  Replies are correlated by id, so several commands can be in flight
- **API**: plain text for `Accept: text/plain`, JSON otherwise. That is what lets the bots and the CLI
  stay thin

## 10. Testing

There is no test project: the dashboard is how the system is exercised. The logic that most wants
testing is written as pure functions — `AutomationRules`, `BindRules`, `MoodRules`, `ScheduleTiming`,
`HistoryAnalysis`, `HistoryBaseline`, `HistoryCorrelation`, `VolitionRules`, `SettingsStore` — so it
can be covered cheaply if that changes

The Kernel does check one thing at boot: it materialises the whole route graph and refuses to start if
any endpoint signature is invalid, because that failure otherwise turns every request into a 500

## 11. Known limitations

- The ESP32 link is **send-only in the firmware**. The Kernel can send a `SourceCommand` to any station
  that announces outputs, but this sketch never reads, so an LED as an ambient channel needs a read
  path added to the firmware first
- `Wire.begin()` must never be left to the board variant. The sketch probes 21/22, then 13/16, then
  13/33: flashing as `esp32-poe-iso` puts I²C on 13/16 while the sensors are wired for 21/22, and
  every device fails `begin()` while the GPIO PIR keeps working. The tell is `light = -2`, BH1750's
  "not configured" sentinel
- `IdleSeconds` comes from outside: `cortana idle on|off` writes `$XDG_RUNTIME_DIR/cortana/idle`, and
  an `ext-idle-notify` client such as hypridle calls it. Wayland exposes idle no other way, and logind
  is no help — `IdleAction=ignore` and nothing maintains the session `IdleHint`
- The agent is Hyprland-first, not Hyprland-only. It finds the socket under `$XDG_RUNTIME_DIR/hypr/`
  rather than trusting `HYPRLAND_INSTANCE_SIGNATURE`, which is empty when the agent starts before
  Hyprland exports it. Without a compositor it probes for `gamescope` or a `steamapps/common` binary
- `pc_gpu` baselines are skewed until pre-effective-load samples age out of the window
- Only the GPU rail reports power; CPU package energy needs root on this kernel
- `HistoryAnalysis.WorstPeriod` is O(n²) over the window. Fine for a few thousand samples
- The Discord `/games` module needs `CORTANA_IGDB_*`; `yt-dlp` is required for anything YouTube
