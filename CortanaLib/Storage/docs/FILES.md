# Cortana — the map

What every file is for, grouped the way the code is. Each section opens with **"change this when…"**
so you can find the right file without reading the whole tree

`DEV.md` explains how the pieces find each other; this is the index

---

# CortanaLib

The shared vocabulary. Every project references it; it references nothing. **If a type crosses a
process boundary it belongs here**

## `Contracts/` — the shapes that cross the wire

*Change these when a client needs to see something new. Adding a field here means every client and
the API see it; nothing else needs touching*

| File | What it carries |
| :--- | :--- |
| `Snapshot.cs` | `CortanaSnapshot`, the single object every client renders, plus `DeviceView`, `SensorView`, `AutomationView`, `MetricsView` |
| `Fabric.cs` | The hardware vocabulary: `SourceDescriptor`, `DeviceDescriptor`, `SensorDescriptor`, `Trigger`, `Bind` |
| `Activity.cs` | The desktop's focus category and, separately, what is playing |
| `Memory.cs` | What Cortana remembers, and its two horizons |
| `Ai.cs` | Ask/reply shapes, AI settings, `VolitionState` |
| `History.cs` | Series, analysis results, baselines, correlations, session insights |
| `Metrics.cs` | `MachineSample`, what a machine reports about itself |
| `Notifications.cs` | A notification and its `Reason`, plus the channel envelope |
| `Schedules.cs` | Schedule definitions, triggers and actions |
| `Push.cs` | Browser push subscriptions and their per-device preferences |
| `Requests.cs`, `Responses.cs` | Request bodies and list wrappers used by the API |

## `Primitives/` — vocabulary with no behaviour

*Change `Ids.cs` when you want a new well-known device or sensor name. Note that you do not have to:
ids are strings, and a source can announce anything*

| File | What it carries |
| :--- | :--- |
| `Ids.cs` | Well-known device, sensor and source ids. Conventions, not a closed set |
| `Enums.cs` | Every enum crossing a boundary: power state, mood, automation status, notification source and level, command origin |
| `Result.cs` | `Result<T>`, the success-or-message type returned by anything that can fail |

## `Client/` and `Runtime/`

*Change `CortanaClient.cs` when you add an API route a client needs. Everything in `Runtime/` is
plumbing shared by all processes*

| File | What it does |
| :--- | :--- |
| `Client/CortanaClient.cs` | The typed HTTP client every client process uses. One method per route |
| `Runtime/CortanaEnvironment.cs` | The `.env` file, the config and storage folders, and the JSON options |
| `Runtime/JsonStore.cs` | Read and write JSON through a temp file, so a crash cannot truncate a config |
| `Runtime/MachineMetrics.cs` | What a Linux box can say about itself: CPU, memory, disk, GPU load, GPU power |
| `Runtime/Units.cs` | Units and number formatting, in one place so every surface agrees |
| `Runtime/Log.cs`, `Shell.cs`, `ProcessSignals.cs` | Logging, shelling out, and signal handling |
| `Media/MediaLibrary.cs` | The shipped assets: icon, prompt, sounds |

---

# CortanaKernel

The only process that owns state. Laid out `Api → Application → Domain ← Infrastructure`

## `Domain/` — rules and in-memory state, no I/O

*This is where behaviour lives. If you are changing **what Cortana decides**, it is in here. Nothing
in this folder knows about HTTP, sockets or files*

### `Domain/Fabric/` — the hardware model

*Change these to alter how devices and sensors are modelled, or how sensors drive devices*

| File | What it does |
| :--- | :--- |
| `Fabric.cs` | Sources and their channels, the virtual devices and sensors registered on them, and the live state |
| `BindRules.cs` | The pure decision: given a bind and the readings, should this device be on |
| `BindStore.cs` | The bindings themselves, persisted and editable |
| `WarningRules.cs` | What Cortana watches for, with fixed hysteresis, plus `WarningStore` and `WarningState`. Warnings take the same `Trigger` as binds |
| `FabricDefaults.cs` | The shipped channels and the registrations seeded on first boot. **Edit this to change what exists out of the box** |
| `PresenceState.cs` | The last-motion latch, which outlives one reading |
| `Hardware.cs` | The seams Infrastructure implements: `IChannelWriter` per source, and the desktop link |

### `Domain/Automation/`

*Change these to alter when Cortana acts, or how she describes her own state*

| File | What it does |
| :--- | :--- |
| `AutomationEngine.cs` | Owns automation authority, sleep mode, device holds and the sleep hold. Ticks once a second, then evaluates every bind |
| `SleepEngine.cs` | The sleep state machine: entry delay, hold, the daytime nap, and what wakes it |
| `AutomationRules.cs` | The pure decisions that are not per-device: day/night and presence |
| `DayNightClock.cs` | Produces the time context, so nothing else has to know the hours |
| `MoodRules.cs` | The mood word and the sentence behind it |

### `Domain/Ai/`, `Domain/Volition/`

*Change these to alter what Cortana knows, or when she speaks first*

| File | What it does |
| :--- | :--- |
| `Ai/AiProvider.cs` | Conversations and the provider-agnostic request/tool/response shapes |
| `Ai/Capability.cs` | What a capability is, and its four kinds. **`IsReadOnly` here decides what guests can reach** |
| `Ai/MemoryStore.cs` | Permanent facts and expiring state, with weighted recall |
| `Ai/AiSettingsStore.cs` | Model choice, memory depth, history cadence, wrap-up hour and chance |
| `Volition/VolitionRules.cs` | Whether she may speak unprompted, and the quiet period that stops her |

### `Domain/History/`, and the rest

| File | What it does |
| :--- | :--- |
| `History/HistoryRepository.cs` | The recorded-sample store interface |
| `History/HistoryAnalysis.cs` | Deterministic reductions the AI uses instead of doing arithmetic |
| `History/HistoryBaseline.cs` | What is normal for a metric at this hour, as a median and a robust spread |
| `History/HistoryCorrelation.cs` | Room against desk: correlation, per-activity split, current-session drift |
| `History/DayRhythm.cs` | One day reduced to the numbers a rhythm is made of, and the median that makes "usual" |
| `Activity/ActivityRegistry.cs` | What the desktop is doing, and the do-not-disturb rule |

| `Notifications/NotificationLog.cs` | Bounded history of everything she has said, and the sink interface |
| `Settings/SettingsStore.cs` | Runtime settings with bounds and validation. **Add a `SettingKey` here to expose a new tunable** |
| `Scheduling/ScheduleRepository.cs` | Schedule persistence interface and the timing rules |
| `Services/ServiceSupervisor.cs` | Process supervision and host-machine interfaces |
| `Common/EventBus.cs` | The typed in-process publish/subscribe |
| `Common/DomainEvents.cs` | Every event that can be published. **Add one here to let anything react to a new fact** |
| `Common/CommandOrigin.cs` | Who asked, from where, and whether the AI relayed it |

## `Application/` — orchestration

*Change these when a command needs to do several things, or when a client needs a new operation. This
is the layer that turns one request into domain calls plus an event*

| File | What it does |
| :--- | :--- |
| `SnapshotService.cs` | Builds the read model every client renders, and the mood |
| `DeviceService.cs` | The one place device commands run: name resolution, wake-on-LAN, the PC shutdown sequence, and re-asserting outputs on boot |
| `SensorService.cs` | Turns source observations into facts; applies each sensor's offset; evaluates warnings |
| `AutomationService.cs` | Wires the engine to the bus and drives its tick; also the world and effects adapters |
| `VolitionService.cs` | The one place she decides to speak first: the morning greeting, the daily wrap-up and quiet |
| `AiService.cs` | The conversation loop, the system prompt, memory recall, and `Compose` for phrasing anything |
| `CapabilityRegistry.cs` | Every capability the AI can reach. **Add a capability here to give her a new ability** |
| `NotificationService.cs` | Channel policy and fan-out to the sinks |
| `HistoryService.cs` | Records the house on a cadence; answers series, analysis, baselines and correlations |
| `ScheduleService.cs` | Persistent schedules: validation, the due loop, event hooks, dispatch |
| `MetricsService.cs` | Samples the Pi on a timer and turns any machine sample into readings plus facts |
| `ServiceControlService.cs` | Start/stop/restart/update with a short status cache |
| `SettingsService.cs` | Setting writes as domain facts |
| `ComputerPresenceService.cs` | Turns an agent connection into device state and source liveness |
| `PluginService.cs` | Every feature she runs, the switch behind each one, and whether it has one |
| `StateBroadcaster.cs` | Turns every event into "there is a newer snapshot", and feeds the notification stream |

## `Api/` — transport only

*Change these to expose something over HTTP. No decisions belong here*

| File | What it serves |
| :--- | :--- |
| `ApiResults.cs` | Content negotiation: plain text or JSON from one `Result<T>` |
| `ApiAccess.cs` | The three access levels and the API-key check |
| `Endpoints/HomeEndpoints.cs` | Identity, health, the snapshot, and the two SSE streams |
| `Endpoints/FabricEndpoints.cs` | Sources, channels, registrations, binds, warnings and the dashboard layout |
| `Endpoints/PluginEndpoints.cs` | The feature list and its switches |
| `Endpoints/NoteEndpoints.cs` | Notes: read, write, settle, drop |
| `Endpoints/DeviceEndpoints.cs`, `SensorEndpoints.cs` | Reading and switching |
| `Endpoints/AutomationEndpoints.cs` | Automation, sleep mode, holds, diagnostics |
| `Endpoints/AiEndpoints.cs` | Ask, one conversation's turns, prompt, model, settings, memory, quiet |
| `Endpoints/HistoryEndpoints.cs` | Series, analysis, baselines, correlation, session |
| `Endpoints/ScheduleEndpoints.cs`, `SettingEndpoints.cs`, `ServiceEndpoints.cs` | Schedules, settings, services |
| `Endpoints/MachineEndpoints.cs` | The desktop and the Raspberry |
| `Endpoints/NotificationEndpoints.cs` | The log, sending, and push subscriptions |

## `Infrastructure/` — the outside world

*Change these to talk to different hardware, a different model, or a different storage format. Each
one implements an interface declared in `Domain`*

| File | What it does |
| :--- | :--- |
| `Gpio/GpioDeviceController.cs` | The relays on the Pi's header. **Reads `Pins.json` for the pin map, and never closes a pin on shutdown: releasing a line stops it holding its relay** |
| `Network/ConnectionServer.cs` | The TCP listener both the station and the agent connect to |
| `Network/StationSource.cs` | Every announced station: frames in, readings out, one connection each |
| `Network/StationChannelWriter.cs` | Outputs that live on a station, switched down its own socket |
| `Network/DesktopComputerEndpoint.cs` | The agent link: commands out, replies and activity in |
| `Ai/GeminiProvider.cs` | The only Gemini-aware file in the codebase |
| `Ai/ModelCatalogue.cs` | Which models exist and which is selected |
| `Push/PushService.cs` | Web push: the persistent status line and her own messages |
| `Persistence/JsonRepositories.cs` | Every JSON-backed repository, in one file |
| `Persistence/CsvHistoryRepository.cs` | One CSV per day, columns discovered from the sample and widened as registrations appear |
| `Process/SystemdSupervisor.cs` | Starting, stopping and reading the journal |
| `Raspberry/RaspberryHost.cs`, `NetworkProfile.cs` | The host machine and its network identity |

`Program.cs` is the composition root: **every registration lives there, and a new service must be
registered there or the Kernel will crash at startup rather than fail to compile**

---

# CortanaWeb

Blazor Server dashboard. *Change `Pages/` for a screen, `Shared/` for a piece reused across screens*

| File | What it is |
| :--- | :--- |
| `Services/CortanaState.cs` | The dashboard's view of the Kernel: the snapshot, the live stream, and every command |
| `Components/Pages/Dashboard.razor` | Chat, the sensors and devices you picked, the modes, the machines |
| `Components/Pages/Devices.razor`, `Sensors.razor`, `Core.razor` | Devices, sensor detail and history, the Pi. **Both render whatever the fabric offers, so a new device or sensor appears on its own** |
| `Components/Pages/Hardware.razor` | Five tabs — sources, devices, sensors, binds, warnings — each creating and editing through the same modal |
| `Components/Pages/Memory.razor`, `Notes.razor` | What she knows about you, and what you asked her to note. Both say so when the feature is switched off |
| `Components/Pages/Logs.razor` | Activity, service journals, push preferences |
| `Components/Pages/Docs.razor` | The API reference link and how the pieces fit. The feature switches live on `Core.razor` |
| `Components/Pages/Settings.razor`, `Utility.razor`, `Quick.razor`, `Login.razor` | Settings, tools, one-tap actions, auth |
| `Components/Shared/ActivityRibbon.razor` | The day as coloured segments by category |
| `Components/Shared/SourceCard.razor`, `MachineFooter.razor`, `SensorGrid.razor` | Any source, drawn the same wherever it appears: its sensors as bars grouped by shared name, the facts it reports, activity and now playing |
| `Components/Shared/HistoryChart.razor`, `HistoryPanel.razor` | The SVG plot and its pickers |
| `Components/Shared/ChatPanel.razor`, `SchedulePanel.razor`, `AiSettingsPanel.razor` | Conversation, schedules, model settings |
| `Components/Shared/DeviceCard.razor`, `StatTile.razor`, `Metric.razor`, `Icon.razor`, `Toast.razor`, `StatusPill.razor` | The repeated display pieces |
| `Components/Shared/NumberSetting.razor`, `AiNumberSetting.razor` | Editable settings, integer or decimal |
| `Components/Shared/Modal.razor`, `TriggerEditor.razor` | The one dialog every create and edit opens in, and the condition row inside it |
| `Components/Shared/TabStrip.razor` | Every tabbed page's strip, swipeable on touch. Vertical scrolling always wins over a lazy diagonal drag |
| `Components/Shared/ActingPage.cs`, `Models/TriggerDraft.cs` | The busy/message/run base every acting page shares, and what a typed form parses back into |
| `Components/App.razor`, `Routes.razor`, `Layout/MainLayout.razor`, `NavMenu.razor`, `EmptyLayout.razor` | The shell: document, routing, navigation. **Add a page to `NavMenu.razor` and the Dashboard "More" grid, or it is unreachable on mobile** |
| `Components/Shared/RedirectToLogin.razor` | Sends unauthenticated visitors to the login page |
| `wwwroot/app.css` | All styling |
| `wwwroot/service-worker.js` | Shell caching, push display, notification clicks |
| `wwwroot/notify.js` | Local notifications and the push subscription API |

---

# The other clients

| File | What it is |
| :--- | :--- |
| `CortanaTelegram/Menus/*.cs` | One menu per topic. `Menu.cs` is the base; `LiveMenu.cs` keeps a message in step with the house |
| `CortanaTelegram/Runtime/TelegramSession.cs` | The bot connection, topics and message helpers |
| `CortanaDiscord/Modules/*.cs` | Slash-command groups. Discord caps a group at 25 subcommands. They read the house and switch what switches; they do not configure it |
| `CortanaDesktop/Agent.cs` | The resident socket, the JSON-line protocol, the metrics push |
| `CortanaDesktop/Activity.cs` | Watches Hyprland and `playerctl`; maps window class to a category. **Edit the map here to categorise a new application** |
| `CortanaDesktop/DesktopOs.cs` | Everything done to this machine |
| `CortanaDesktop/Cli.cs` | `chat`, `ask`, `monitor`, `pc`, `status` |
| `CortanaDesktop/Scripts/cortana` | The desktop wrapper: agent dispatch, API, git, deploy, notify, idle |
| `CortanaKernel/Scripts/cortana` | The Pi wrapper, including `flash` for the station |
| `CortanaEmbedded/ESP32Station/ESP32Station.ino` | The station firmware. **Probes for the I²C bus rather than trusting the board variant** |
