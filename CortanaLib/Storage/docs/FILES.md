# Cortana — file map

What every file in the repository is for, in a line each. Directory order follows the layering:
shared contracts, then the Kernel from the outside in, then the clients.

---

## CortanaLib — shared by every process

Contracts, the API client, and small runtime helpers. Holds no business rules.

| File | Purpose |
| :--- | :--- |
| `Primitives/Enums.cs` | Every enum crossing a boundary: devices, sensors, settings, services, command origin, automation status |
| `Primitives/Result.cs` | `Result<T>`: a value or a human-readable error, used at every layer boundary |
| `Contracts/Snapshot.cs` | `CortanaSnapshot` and its views — the read model clients render |
| `Contracts/Requests.cs` | Request bodies for switching, settings, commands and notifications |
| `Contracts/Responses.cs` | Generic message and problem envelopes |
| `Contracts/Schedules.cs` | Schedule model, triggers, events, actions and their request/response shapes |
| `Contracts/Ai.cs` | Ask request/response, model list, AI setting keys |
| `Contracts/History.cs` | History points, series, and the deterministic analysis request/result |
| `Contracts/Metrics.cs` | `MachineSample`, pushed by the desktop and produced locally for the Pi |
| `Contracts/Notifications.cs` | Notification entry and the channel envelope used by the event stream |
| `Contracts/Push.cs` | Browser push subscription and its per-device preferences |
| `Client/CortanaClient.cs` | The only way a client talks to the Kernel: typed HTTP plus both SSE streams |
| `Runtime/CortanaEnvironment.cs` | Config folders, the `.env` loader, and the two JSON conventions |
| `Runtime/JsonStore.cs` | Atomic read/write of JSON files; every repository is built on it |
| `Runtime/Log.cs` | Console logging (user-facing messages go through notifications instead) |
| `Runtime/Shell.cs` | Shell execution with a timeout and bounded, merged output |
| `Runtime/MachineMetrics.cs` | CPU/RAM/GPU/disk/uptime read from `/proc` and `/sys`, plus their rendering |
| `Runtime/Units.cs` | One place deciding how a reading is spelled, so clients agree |
| `Runtime/ProcessSignals.cs` | SIGTERM/SIGINT wait for the non-web processes |
| `Media/MediaLibrary.cs` | QR codes, and YouTube through the system `yt-dlp` only |
| `Storage/prompt.txt` | The system prompt Cortana ships with |

---

## CortanaKernel — the brain

### `Domain/` — the rules

Pure C#. No ASP.NET, no GPIO, no sockets, no provider SDK.

| File | Purpose |
| :--- | :--- |
| `Common/CommandOrigin.cs` | Who asked and through which surface; sleep-wake rules depend on it |
| `Common/DomainEvents.cs` | Every typed fact: device, sensor, sleep, automation, schedule, hold |
| `Common/EventBus.cs` | In-process typed publish/subscribe; no string hooks, no central switch |
| `Automation/AutomationRules.cs` | The pure decisions: day/night, motion freshness, the lamp, air-quality hysteresis |
| `Automation/AutomationEngine.cs` | Owns automation authority, sleep mode, device holds and the sleep hold; ticks once a second |
| `Devices/DeviceRegistry.cs` | Cortana's *belief* about each device, plus the hardware and computer interfaces |
| `Sensors/SensorRegistry.cs` | Last observation, freshness, motion timestamp and the air-quality flag |
| `Settings/SettingsStore.cs` | Domain-owned settings: definitions, validation, persistence, change notification |
| `Scheduling/ScheduleRepository.cs` | The schedule store interface and `ScheduleTiming` — next run, and whether an event should fire |
| `Ai/AiProvider.cs` | Conversations and the provider-agnostic request/tool/response shapes |
| `Ai/Capability.cs` | What an AI capability is, and its four kinds (query, analysis, action, management) |
| `Ai/AiSettingsStore.cs` | Model choice, memory depth, history cadence, push overlay duration |
| `History/HistoryRepository.cs` | The recorded-sample store interface |
| `History/HistoryAnalysis.cs` | The deterministic reductions the AI uses instead of doing arithmetic |
| `Metrics/MetricsRegistry.cs` | Latest desktop and Raspberry samples, with staleness |
| `Notifications/NotificationLog.cs` | Bounded activity history and the delivery-sink interface |
| `Services/ServiceSupervisor.cs` | Process supervision and host-machine interfaces |

### `Application/` — commands, queries, orchestration

| File | Purpose |
| :--- | :--- |
| `DeviceService.cs` | The one place device commands run: room semantics, wake-on-LAN, the PC shutdown sequence |
| `AutomationService.cs` | Wires the engine to the bus and drives its tick; also the world/effects adapters |
| `SensorService.cs` | Turns station observations into facts; applies the temperature offset; air-quality policy |
| `SettingsService.cs` | Setting writes as domain facts |
| `ScheduleService.cs` | Persistent schedules: validation, the due loop, event hooks, dispatch to real commands |
| `NotificationService.cs` | Channel policy and fan-out to the sinks |
| `SnapshotService.cs` | Builds the read model and the automation diagnostics |
| `StateBroadcaster.cs` | Turns every event into "there is a newer snapshot", and feeds the notification stream |
| `MetricsService.cs` | Samples the Pi on a timer, accepts the desktop's pushes |
| `HistoryService.cs` | Records the house on a cadence; answers series and analysis queries |
| `ServiceControlService.cs` | Start/stop/restart/update with a short status cache |
| `ComputerPresenceService.cs` | Turns an agent connection into device state — this is what "PC is on" means |
| `CapabilityRegistry.cs` | Every capability the AI can reach, each calling an ordinary application command |
| `AiService.cs` | Conversation persistence, prompt handling, and the single door between model and Cortana |

### `Infrastructure/` — the outside world

| File | Purpose |
| :--- | :--- |
| `Gpio/GpioDeviceController.cs` | The relays, the pin map, the location difference, and the pulse-relay option |
| `Network/ConnectionServer.cs` | One TCP port for both machines, with the handshake and the shared socket plumbing |
| `Network/DesktopComputerEndpoint.cs` | The desktop as a capability: JSON-line protocol, correlated replies, shutdown wait |
| `Network/Esp32SensorSource.cs` | The station's brace-counted JSON frames turned into readings |
| `Persistence/JsonRepositories.cs` | Settings, AI settings, schedules and conversations on disk |
| `Persistence/CsvHistoryRepository.cs` | One CSV per day, pruned by retention |
| `Ai/GeminiProvider.cs` | The only file that knows about Gemini: its shapes, tool dialect and errors |
| `Ai/ModelCatalogue.cs` | Model ids per family, refreshed daily, parked when rate limited |
| `Push/PushService.cs` | The persistent browser status notification, its event overlay and the subscriptions |
| `Process/SystemdSupervisor.cs` | systemd user units |
| `Raspberry/RaspberryHost.cs` | The Pi itself: temperature, gateway, public IP, power, shell, wake-on-LAN |
| `Raspberry/NetworkProfile.cs` | Location profiles, chosen by matching the live gateway |

### `Api/` — the public boundary

| File | Purpose |
| :--- | :--- |
| `ApiAccess.cs` | Access levels, the API key gate, and the caller's declared surface |
| `ApiResults.cs` | Dual rendering: plain text for terminals and bots, JSON for the dashboard |
| `Endpoints/HomeEndpoints.cs` | Identity, health, the snapshot, and both SSE streams |
| `Endpoints/DeviceEndpoints.cs` | Device and room switching |
| `Endpoints/AutomationEndpoints.cs` | Automation, sleep mode, and the diagnostics |
| `Endpoints/SensorEndpoints.cs` | Readings, plus the station calibration note |
| `Endpoints/SettingEndpoints.cs` | Reading and writing automation settings |
| `Endpoints/MachineEndpoints.cs` | The desktop, the Raspberry, and both metric streams |
| `Endpoints/ServiceEndpoints.cs` | Service state, control and journals |
| `Endpoints/ScheduleEndpoints.cs` | Schedule CRUD and run/enable/disable |
| `Endpoints/AiEndpoints.cs` | Ask, conversations, prompt, models and AI settings |
| `Endpoints/HistoryEndpoints.cs` | Series (with time paging) and deterministic analysis |
| `Endpoints/NotificationEndpoints.cs` | The activity log, sending, and push subscriptions |
| `Program.cs` | The composition root: registration, the API key middleware, routes, fail-fast route check |

### `Scripts/`

| File | Purpose |
| :--- | :--- |
| `cortana` | Control script on the Pi: start/stop/status/log/install/update, and ESP32 flashing |
| `migrate-config` | Converts a legacy `~/.config/cortana` tree to the current layout, idempotently |
| `cortana-*.service` | The four systemd user units; the Kernel pulls the others up |
| `nginx` | Dashboard on `/`, API on `/api/`, with the WebSocket upgrade Blazor needs |

---

## CortanaWeb — the dashboard

| File | Purpose |
| :--- | :--- |
| `Program.cs` | Host, cookie auth, and the QR/audio/video media endpoints |
| `Services/CortanaState.cs` | Holds the newest snapshot, keeps the stream open, falls back to polling |
| `Services/WebAuth.cs` | The optional dashboard passcode |
| `Components/App.razor`, `Routes.razor`, `_Imports.razor` | Document shell, routing, shared usings |
| `Components/Layout/*` | The frame, the nav, and the bare layout used by the login page |
| `Components/Pages/Dashboard.razor` | Chat, sensors, automation, quick toggles, machine cards |
| `Components/Pages/Devices.razor` | Device cards, room control, and the desktop command panel |
| `Components/Pages/Sensors.razor` | Station, computer and Raspberry tabs with their plots |
| `Components/Pages/Settings.razor` | Automation, sleep/override durations, AI, notification destinations |
| `Components/Pages/Core.razor` | Raspberry info, service control, broadcast, shell, power |
| `Components/Pages/Logs.razor` | Activity, service journals, and push notification preferences |
| `Components/Pages/Utility.razor` | Schedules, QR codes, YouTube downloads |
| `Components/Pages/Quick.razor` | The home-screen shortcut targets |
| `Components/Pages/Login.razor` | Passcode entry |
| `Components/Shared/HistoryChart.razor` | The SVG plot: real-time x-axis, gaps left as gaps |
| `Components/Shared/HistoryPanel.razor` | Metric and window pickers, and paging back through time |
| `Components/Shared/SchedulePanel.razor` | Schedule list and the creation form |
| `Components/Shared/ChatPanel.razor` | The conversation, with its id kept in the browser |
| `Components/Shared/AiSettingsPanel.razor` | Model, temperature, memory, and the system prompt |
| `Components/Shared/DeviceCard.razor`, `ComputerCard.razor`, `StatTile.razor`, `Metric.razor` | The repeated display pieces |
| `Components/Shared/NumberSetting.razor`, `AiNumberSetting.razor` | Editable settings, integer or decimal |
| `Components/Shared/Toast.razor`, `StatusPill.razor`, `Icon.razor`, `RedirectToLogin.razor` | Small shared UI |
| `wwwroot/app.css` | The whole visual design, carried over unchanged |
| `wwwroot/service-worker.js` | Shell caching, the offline page, and the push handler |
| `wwwroot/notify.js` | Notification permission, push subscription, and the stored preferences |
| `wwwroot/manifest.webmanifest` | Installable app metadata and the home-screen shortcuts |

---

## Clients and devices

### CortanaTelegram

| File | Purpose |
| :--- | :--- |
| `Program.cs` | Update routing, the home menu, and both Kernel streams |
| `Runtime/TelegramConfig.cs` | The home group, its topics, and the known usernames |
| `Runtime/TelegramSession.cs` | Bot handle, pending-input registry, duration parsing, acks and toasts |
| `Menus/Menu.cs` | One updating message per topic, edited in place only within its own topic |
| `Menus/LiveMenu.cs` | Keeps the visible menus in step with Kernel state |
| `Menus/DeviceMenu.cs` | Devices, room, sleep, automation, and device timers |
| `Menus/SensorMenu.cs` | Readings and the editable automation settings |
| `Menus/SystemMenu.cs` | The desktop on one tab, the Raspberry on the other |
| `Menus/CortanaMenu.cs` | Chat, AI settings, and the services |
| `Menus/UtilityMenu.cs` | QR codes, reminders, schedules, downloads, and the relay chat |

### CortanaDiscord

| File | Purpose |
| :--- | :--- |
| `Program.cs` | Gateway wiring, mention-driven chat, presence, and the notification stream |
| `Runtime/DiscordContext.cs` | Identities, per-guild settings, embeds, and the Kernel client |
| `Runtime/CommandHandler.cs` | Slash-command registration and error reporting |
| `Modules/HomeModule.cs` | The house: devices, sensors, automation, machines, schedules, services, AI |
| `Modules/UtilityModule.cs` | QR codes, downloads, avatars, counting, and reminders |
| `Modules/FunModules.cs` | Random picks and IGDB game lookups |
| `Modules/ServerModule.cs` | Moderation and per-server settings |

### CortanaDesktop

| File | Purpose |
| :--- | :--- |
| `Program.cs` | Agent when run bare, CLI when given arguments |
| `Agent.cs` | The resident socket, the JSON-line protocol, and the metrics push |
| `Cli.cs` | `chat`, `ask`, `monitor`, `pc`, `status` |
| `DesktopOs.cs` | Everything done to this machine, including misspelling-tolerant app matching |
| `Scripts/cortana` | The desktop wrapper: agent dispatch, API, git, deploy, notifications |
| `Scripts/cortana-desktop.service` | The agent's systemd user unit |

### CortanaEmbedded

| File | Purpose |
| :--- | :--- |
| `ESP32Station/ESP32Station.ino` | The sensor station: reads the sensors, streams JSON, reports motion immediately |
| `ESP32Station/secrets.example.h` | Template for the network and Kernel address |

---

## Documentation

| File | Purpose |
| :--- | :--- |
| `README.md` | What Cortana is, and how to set her up and use her. Stays at the repository root so GitHub renders it |
| `DEV.md` | Architecture, decisions, non-obvious behaviour, and remaining work |
| `FILES.md` | This map |
| `.editorconfig` | Tab indentation, and unused variables and parameters treated as warnings |
