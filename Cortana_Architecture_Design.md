# Cortana — Target Architecture

**Status:** Target design for greenfield implementation  
**Primary consumer:** An implementation LLM building the new project from the supplied empty project structure.

---

## 0. Authority and Scope

This document defines **how Cortana should be structured**.

`Cortana_Behavior.md` defines **what Cortana should do**.

The current repository is a reference for:

- existing user-facing functionality;
- current terminology;
- current integrations;
- current Web visuals;
- current emojis;
- exact existing hardware/sensor definitions;
- details that this specification explicitly says to inspect in the current source.

The current repository is **not** the target architecture.

### Priority

```text
1. Explicit requirement in these design documents
2. Explicit user decisions captured in these documents
3. Existing user-facing behavior that was not intentionally changed
4. Existing implementation details
```

Preserve **behavior** where required. Do not preserve bad implementation.


### Implementation order

Work in this order:

Finish the Kernel first.
Make the new Kernel architecture and functionality coherent and usable before spending significant effort on the clients.

Adapt the Web application.
Keep the existing Web UI, graphs, plots, and general UX essentially as they are, while adapting their internals to the new Kernel.

The Web will be my primary way of manually testing the system, so don't spend excessive effort building a huge or heavy automated test suite. Add focused tests for important/complicated logic and safety-critical behavior, but I will manually exercise most functionality through the Web.

Adapt Desktop / CLI.

The Desktop CLI should provide:

an interactive persistent chat mode, like the current terminal experience;

a non-interactive persistent chat invocation that sends one message through the same persistent conversation and returns;

the existing stateless ask behavior, which does not use conversation history.

In other words, persistent interactive chat, persistent one-shot chat, and stateless one-shot ask should remain distinct capabilities.

Adapt Telegram.

Adapt Discord.

Keep the existing interaction styles. Discord voice is currently out of scope because the library is broken.

---

# 1. Design Goals

The new system should be:

- a modular monolith at the Kernel level;
- strongly separated by domain responsibility;
- event-driven where facts need to be distributed;
- command/query driven for operations and reads;
- hardware-transport independent;
- client independent;
- LLM-provider independent;
- testable without physical hardware;
- simple enough that another LLM can continue implementing it safely.

The architecture should prefer explicit, small boundaries over generic service abstractions.

---

# 2. Non-Goals

Do not:

- turn the Kernel into microservices;
- introduce event sourcing;
- require Redis unless a concrete requirement appears;
- create a giant global event handler;
- create generic `Manager`, `Helper`, or `Utility` abstractions for unrelated behavior;
- let clients implement business logic;
- let AI bypass application/domain operations;
- redesign the Web UI;
- preserve internal backward compatibility;
- preserve legacy project structure merely to minimize file changes.

---

# 3. System Overview

```text
                    ┌─────────────────────────┐
                    │          Users          │
                    └────────────┬────────────┘
                                 │
          ┌──────────────────────┼──────────────────────┐
          │                      │                      │
        Web                 Telegram                Discord
          │                      │                      │
          └──────────────┬───────┴──────────────┬──────┘
                         │                      │
                      Desktop                 CLI
                         │                      │
                         └──────────┬───────────┘
                                    │
                             API / Client Boundary
                                    │
                         ┌──────────▼───────────┐
                         │     Cortana Kernel    │
                         │                       │
                         │ Application           │
                         │ Domain                │
                         │ Infrastructure        │
                         └──────────┬────────────┘
                                    │
                    ┌───────────────┼────────────────┐
                    │               │                │
                 Raspberry        ESP32           Desktop
                  GPIO          sensors only       agent
```

The Kernel owns Cortana's authoritative application state and domain behavior.

---

# 4. Layering

```text
┌─────────────────────────────────────────┐
│ Client / Delivery                       │
│ Web / Telegram / Discord / Desktop      │
└───────────────────┬─────────────────────┘
                    │
┌───────────────────▼─────────────────────┐
│ API / Application                       │
│ Commands / Queries / Authorization       │
└───────────────────┬─────────────────────┘
                    │
┌───────────────────▼─────────────────────┐
│ Domain                                  │
│ Devices / Sensors / Automation / AI ... │
└───────────────────┬─────────────────────┘
                    │ interfaces
┌───────────────────▼─────────────────────┐
│ Infrastructure                          │
│ GPIO / ESP32 / persistence / providers  │
└─────────────────────────────────────────┘
```

### Dependency rule

Dependencies point inward.

Domain code must not depend on:

- HTTP;
- ASP.NET;
- Telegram;
- Discord;
- Blazor;
- GPIO libraries;
- TCP socket types;
- Redis;
- Gemini SDK;
- systemd.

Infrastructure implements interfaces required by inner layers.

---

# 5. Kernel Domains

The Kernel should be conceptually divided into:

| Domain | Responsibility |
|---|---|
| Devices | Device concepts, device commands, device state |
| Sensors | Sensor observations, availability, sensor state |
| Automation | Rules and automatic decisions |
| Scheduling | Persistent schedules and timing |
| AI | Conversations, capabilities, tool orchestration |
| Notifications | Notification policy and notification delivery requests |
| Metrics | Measurements and current metric projections |
| History | Historical persistence and queries |
| Settings | Domain-owned runtime settings |
| System | service/process/system state |

These are **logical ownership boundaries**, not a requirement to create exactly one project per row.

---

# 6. Commands, Queries, Events

## Command

A request to perform an action.

```text
TurnDeviceOn
TurnDeviceOff
SetDeviceState
EnableAutomation
DisableAutomation
EnableSleepMode
DisableSleepMode
CreateSchedule
DeleteSchedule
AskCortana
SendNotification
```

Commands can fail.

## Query

A read operation.

```text
GetDeviceState
GetSensorState
GetAutomationState
GetSleepState
GetComputerState
GetHistory
GetMetrics
GetSystemStatus
```

Queries do not mutate state.

## Event

A fact about something that happened.

```text
DeviceStateChanged
MotionDetected
SensorAvailabilityChanged
ComputerStateChanged
ScheduleTriggered
SleepModeChanged
AutomationChanged
ConversationUpdated
SubfunctionStateChanged
```

Events are immutable facts, not RPC requests.

---

# 7. Command Origin

Commands preserve origin.

```text
CommandOrigin
├── Actor: User | System
└── Source/Surface
```

Examples:

```text
User + Web
User + Telegram
User + Discord
User + Desktop
User + LLM + Web
System + Automation
System + Scheduler
System + Startup
```

This is required for:

- Sleep Mode wake behavior;
- auditing/debugging;
- future authorization;
- user/system semantic differences.

An action initiated by a user through AI remains user-originated even though an LLM executes the tool.

---

# 8. Internal Event Bus

The Kernel has an in-process typed EventBus.

It is conceptually similar to hooks:

```text
Event
  ↓
EventBus
  ├── Automation handler
  ├── Notification handler
  ├── History handler
  └── other subscribers
```

Prefer:

```text
Subscribe<DeviceStateChanged>(handler)
```

not string hooks.

### Rules

- handlers may subscribe to typed events;
- handlers may publish new events;
- handlers may issue commands through application boundaries;
- handlers should have one understandable responsibility;
- do not build one giant `EventHandler`;
- do not use the bus for normal request/response operations.

### Event durability

Events are ephemeral.

A subscriber that is unavailable may miss an event.

Current state must be sufficient for recovery.

History is a separate subscriber/store; it is not the EventBus itself.

---

# 9. State Ownership

The domain owns live state.

The Kernel exposes a coherent read-only snapshot:

```text
CortanaSnapshot
├── Devices
├── Sensors
├── Computer
├── Automation
├── Sleep
├── Metrics
├── Services
└── other public state
```

The snapshot is a **read model**, not a second mutable source of truth.

Only application/domain logic changes domain state.

---

# 10. State Change Flow

The standard pattern is:

```text
Command
  ↓
Application
  ↓
Domain
  ↓
state mutation
  ↓
Event
  ↓
snapshot changes
  ↓
external clients receive latest snapshot
```

For observations:

```text
hardware/input
  ↓
adapter
  ↓
domain/application
  ↓
state update
  ↓
event
```

---

# 11. External Client Synchronization

External processes cannot subscribe directly to the in-memory EventBus.

Use:

```text
Kernel state
    ↓
external state stream
    ↓
client process
```

The primary client synchronization model is:

```text
1. Fetch current snapshot
2. Subscribe to live StateChanged updates
3. Receive new coherent snapshots
4. Reconnect by fetching a fresh snapshot
```

Missed ephemeral events are acceptable because the snapshot is authoritative.

---

# 12. Web State Synchronization

The Web should use a persistent live subscription.

```text
Web
 ↓
GET current snapshot
 ↓
subscribe to StateChanged
 ↓
receive coherent snapshots
 ↓
update existing UI
```

A complete snapshot per state change is acceptable at current scale.

Selective subscriptions can be introduced later if actual performance requires them.

The existing Web dashboard's visual design is **out of refactor scope**.

---

# 13. External Event Stream

The current API exposes an SSE state stream.

The target can retain SSE or replace it with an equally simple server→client stream, but the semantics should remain:

```text
server owns state
client receives newest coherent state
```

The Web may fall back to polling while the stream is unavailable.

---

# 14. Hardware Topology

## Raspberry Pi

Cortana Kernel runs here.

Current local devices are attached to Raspberry GPIO:

```text
Lamp
Power
Generic
PC-related power/device control
```

## ESP32

The ESP32 currently provides sensors:

```text
Motion
Temperature
Light
Humidity
CO2
TVOC
...
```

ESP32 connectivity affects sensor availability only.

It does not own or determine Raspberry GPIO device state.

---

# 15. Hardware Interfaces

Use semantic interfaces.

Examples:

```text
ISensorSource
ILocalDeviceController
IComputerEndpoint
```

Do not expose hardware transport details to the domain.

Bad:

```text
Esp32Packet
TcpClient
GpioPin
Socket
```

Good:

```text
SensorReading
DeviceState
ComputerMetrics
```

---

# 16. Device State

Raspberry GPIO devices cannot be read back as physical state.

Therefore device state is Cortana's internal logical state.

Rules:

```text
successful GPIO command
    → internal state changes

failed GPIO command
    → internal state remains unchanged
```

No physical verification is required.

---

# 17. Device Startup

On startup:

```text
all device internal states = OFF
```

But:

**do not physically switch every GPIO output OFF.**

This is mandatory because one GPIO-controlled device controls PC power.

Startup establishes logical state only.

Later explicit commands may write the required physical state.

An explicit ON command against a physically already-ON device is acceptable and can restore Cortana's logical state.

---

# 18. Sensor State

Sensors retain:

```text
last observation
observation timestamp
availability/freshness
```

When ESP32 disconnects:

```text
sensor = unavailable
last observation = retained
```

The retained observation may still be useful historically but is not fresh forever.

When ESP32 reconnects:

```text
first valid reading
→ accept immediately
→ sensor becomes available
```

---

# 19. Motion

Motion is represented by:

```text
LastMotionAt
```

not a durable true/false state.

Automation derives:

```text
MotionActive =
    now - LastMotionAt < applicable timeout
```

Timeout:

```text
PC ON  → longer
PC OFF → shorter
```

Changing PC state causes immediate motion reevaluation.

If the motion sensor is unavailable, retain `LastMotionAt` and let it age out.

Once it exceeds the applicable timeout:

```text
MotionActive = false
```

---

# 20. Automation

Automation is a domain capability that consumes:

```text
time
PC state
motion
sensor state
device state
user actions
Sleep Mode
settings
```

and produces device/application commands.

Automation is **not** a hardware module.

Current main actuator relationship is the Lamp, but the automation design must not hard-code Automation as "the Lamp service."

---

# 21. Automation Authority

```text
AutomationEnabled = false
    → automation makes no autonomous device changes

AutomationEnabled = true
    → automation is authoritative
```

When enabled:

```text
enable
 ↓
clear temporary device overrides
 ↓
immediately evaluate current state
 ↓
reconcile automatically controlled devices
```

Previous manual device state is not retained as an override.

---

# 22. Time Context

Time context is:

```text
Day
Night
```

derived from the configured time window.

It is not a control mode.

```text
Night ≠ Sleep Mode
Night ≠ Automation OFF
```

---

# 23. Sleep Mode

Sleep Mode is a runtime state:

```text
SleepMode = Active | Inactive
```

It means:

> Cortana considers the user to be sleeping and applies sleep-specific automation rules.

Sleep Mode does not disable Automation.

Valid:

```text
AutomationEnabled = true
SleepMode = active
```

---

# 24. Sleep Mode State Model

The important runtime layers are:

```text
Automation
└── Enabled: bool

Time Context
└── Day | Night

Sleep Mode
└── Active | Inactive

Device Overrides
└── temporary per-device state

Sleep Hold
└── temporary suppression of automatic sleep
```

Do not collapse these into one enum.

---

# 25. Sleep Mode Manual Activation

The Sleep Mode control is a toggle:

```text
Inactive → Active
Active   → Inactive
```

Manual activation is always allowed.

The PC being ON does not prevent manual Sleep Mode activation.

---

# 26. Daytime Sleep Mode

Manual Sleep Mode activation during Day:

```text
SleepMode = Active
```

uses the same sleep behavior as nighttime Sleep Mode.

Only its lifetime differs.

```text
Daytime SleepMode
    → expires after DaySleepDuration
```

When it expires:

```text
SleepMode = Inactive
→ automation reevaluates immediately
```

Daytime Sleep Mode is not automatically entered just because it is daytime.

---

# 27. Automatic Nighttime Sleep Entry

At the start of the configured Night period:

```text
if SleepMode already active:
    do nothing

else if PC is ON:
    do not enter Sleep Mode
    notify PC to go to sleep

else:
    start SleepEntryDelay
```

When SleepEntryDelay expires:

```text SleepMode = Active
```

This transition is independent of motion/activity during the delay.

---

# 28. PC State During Sleep Entry

If PC turns ON while SleepEntryDelay is pending:

```text
cancel pending Sleep Mode entry
```

Do not allow the old timer to trigger later.

If PC turns OFF while Sleep Mode is already active:

```text
do nothing
```

Do not toggle or re-enter Sleep Mode.

---

# 29. Morning Sleep Exit

At the configured morning/end-of-night boundary:

```text
SleepMode = Inactive
→ automation reevaluates immediately
```

This happens automatically.

The morning boundary is configurable.

Do not create a separate hard-coded wake threshold.

---

# 30. Sleep Wake Sources

Only these wake the user out of Sleep Mode early:

```text
user-originated Device-layer action
PC power-on
explicit user Automation ON after morning threshold
```

Do not wake Sleep Mode because of:

```text
sensor event
automation command
scheduled action
notification
AI/background work
settings changes
```

Except the explicit Automation ON rule above.

---

# 31. Device Wake Actions

The current Devices layer includes:

```text
Lamp
Power
Generic
PC
```

User-originated commands that pass through this layer can be wake evidence at/after the morning boundary.

Settings changes do not count.

AI-issued actions requested by a user retain user origin and therefore participate in the same rule.

---

# 32. Normal Manual Device Override

When Automation is enabled and Sleep Mode is inactive:

```text
user device action
    → temporary manual device override
```

One global duration:

```text
ManualOverrideDuration
```

Rules:

- timer starts from first manual action;
- later manual actions do not reset it;
- sensor events do not reset it;
- automation events do not reset it.

On expiry:

```text
evaluate current state
```

Do not replay events that occurred during the override.

---

# 33. Sleep Device Override

During Sleep Mode:

```text
user device action
    → SleepManualOverrideDuration
```

One global duration for now.

The behavior applies to device ON and OFF actions.

The override lifetime is independent from Sleep Mode lifetime.

---

# 34. Sleep Hold

At night, if Sleep Mode was active automatically and the user toggles it OFF:

```text
SleepMode = Inactive
SleepHold = Active
```

Automation remains enabled.

When Sleep Hold expires:

```text
automation reevaluates
```

If automatic nighttime sleep is still appropriate:

```text
SleepMode = Active
```

Device actions during Sleep Hold do not extend Sleep Hold.

---

# 35. Automation / Sleep Interactions

### Automation OFF

```text
Automation OFF
→ Sleep Mode OFF
→ temporary overrides cleared
→ no autonomous device control
```

### Automation ON

```text
Automation ON
→ current state evaluated immediately
→ automation takes authority
```

### Sleep Mode ON

```text
Sleep Mode ON
→ automation remains enabled
→ sleep rules become active
→ existing device overrides are cleared
```

### Night

```text
Night
→ time context only
→ may initiate automatic Sleep Mode path
```

---

# 36. Device and Room Semantics

The current user-visible Room operations must remain:

```text
Room OFF
    → Lamp OFF
    → Power OFF
```

```text
Room ON
    → Power ON
    → ensure PC ON
    → Lamp ON only when manual mode is the source of truth
    → otherwise Automation remains authoritative for Lamp
```

Because Power can turn the PC on automatically, ensuring PC ON may be a physical no-op.

The exact current device definitions should be checked in the current source.

---

# 37. PC Shutdown Sequence

Turning PC power OFF is a multi-step operation:

```text
request Desktop shutdown
    ↓
wait until PC/Desktop is actually OFF
    ↓
wait additional configured delay
    ↓
switch physical Power OFF
```

The extra delay is intentional because the Desktop client can terminate before the physical PC shutdown has completely finished.

Never cut physical PC power immediately after requesting shutdown.

---

# 38. Generic Device

The Generic device has location-dependent meaning.

Current mapping:

```text
Orvieto → speaker
Pisa    → lamp-equivalent
```

Pisa has no dedicated Lamp connection, so Generic represents the lamp-equivalent control there.

The legacy relay/pulse lamp option remains available but disabled.

Location-specific mappings belong in configuration/infrastructure, not hard-coded domain semantics.

---

# 39. Scheduling

Scheduling answers:

> When should an action happen?

A scheduled action becomes a normal application command.

```text
ScheduleTrigger
    ↓
normal Command
    ↓
application/domain
```

The Scheduler must not bypass normal validation.

Persistent schedules survive restart.

Event-triggered schedules may react to every matching event unless explicit throttling is introduced.

Missed-run behavior may be simplified from the legacy model while retaining the useful user-facing semantics.

---

# 40. AI as a Unified Cortana Interface

AI is not just "chat."

It is a conversational interface to meaningful Cortana capabilities.

The AI should be able to access essentially anything a user can access through Cortana, subject to authorization.

Examples:

```text
devices
sensors
automation
sleep
schedules
computer
notifications
settings
metrics
history
analysis
system status
```

A user should be able to say:

```text
Turn the lamp on.
Turn automation off.
Put me in sleep mode.
What is the temperature?
What was the average temperature today?
What was the worst air quality yesterday?
Why didn't the lamp turn on?
Create a schedule for tomorrow.
```

---

# 41. AI Capability Types

Use explicit capability categories:

| Capability | Purpose |
|---|---|
| Query | read current state |
| Analysis | deterministic calculations over data |
| Action | mutate system state |
| Management | modify persistent/runtime configuration |

AI tools should represent application capabilities, not low-level infrastructure operations.

---

# 42. AI Analysis

Deterministic operations should be executed by the application, not calculated informally by the LLM.

Examples:

```text
AverageTemperature
MinimumTemperature
MaximumTemperature
ValueAtTime
CountEvents
DurationInState
Trend
AverageAirQuality
WorstAirQualityPeriod
```

The LLM interprets the request and presents the structured result.

Do not give the model raw database access merely to answer an analytical question.

---

# 43. AI Historical Queries

History/metrics capabilities should support conversational questions such as:

```text
average temperature today
temperature yesterday
maximum CO2 around a time
worst air-quality period
compare today and yesterday
time spent in a device state
```

Queries should be deterministic, typed, and bounded.

---

# 44. AI Diagnostic Queries

AI should be able to explain system decisions using explicit diagnostic information.

Examples:

```text
Why did the lamp turn on?
Why didn't the lamp turn on?
Why didn't Sleep Mode start?
Why did Cortana notify the PC?
```

The application should expose relevant facts such as:

```text
current state
relevant sensor observations
automation state
Sleep Mode state
active overrides
timers
recent relevant events
command origins
```

AI must not invent explanations unsupported by those facts.

---

# 45. AI and User Capabilities

If a user can perform an operation through Web/Telegram/Discord/Desktop, the AI should use the same underlying capability.

Example:

```text
Web ───────┐
Telegram ──┤
Discord ───┼──► application command ─► domain
Desktop ───┤
AI ────────┘
```

Do not create an AI-only implementation of domain behavior.

---

# 46. AI Tools Must Not Access Infrastructure Directly

Never:

```text
AI → GPIO
AI → TCP socket
AI → SQL
AI → filesystem
AI → Redis
AI → provider SDK internals
```

Instead:

```text
AI
 ↓
application capability
 ↓
domain
 ↓
infrastructure adapter
```

---

# 47. AI Chat

Chat maintains persistent conversation history.

```text
message
 ↓
conversation
 ↓
AI
 ↓
tools as needed
 ↓
final answer
 ↓
history persisted
```

Conversation history survives Kernel restart.

Different conversations may run independently.

Turns within one conversation are sequential.

---

# 48. AI Ask

Ask is fire-and-forget.

```text
ask
 ↓
AI
 ↓
queries/actions/tools if needed
 ↓
result
```

Ask:

- may use tools;
- may perform user-authorized mutations;
- may perform analysis;
- does not persist the request in the conversation history;
- does not require loading the chat conversation.

---

# 49. AI Provider Boundary

The AI domain/application knows about:

```text
messages
conversation
tool calls
capabilities
model selection
AI requests/responses
```

The provider adapter owns:

```text
Gemini SDK
provider-specific model IDs
provider-specific fallback
provider-specific metadata/signatures
provider-specific errors
```

No provider-specific types outside the adapter.

---

# 50. Notifications

Notifications are driven by events/state.

```text
event
 ↓
notification policy
 ↓
notification
 ↓
delivery adapter
```

Domains should not know Telegram/Discord/Web delivery details.

---

# 51. Metrics and History

Metrics are measurements.

History is persistent historical information.

Live state is authoritative for "what is true now."

History is for:

- graphs;
- plots;
- trends;
- comparisons;
- AI analysis;
- historical views.

Do not reconstruct normal live state from history.

---

# 52. Push Notification Service Worker

The Web includes a service-worker-backed persistent status notification.

This is a specialized Web presentation of Kernel state.

## Format

```text
notification title = empty
notification body  = status/event text
```

Do not set a notification title.

## Persistence

The notification remains persistent.

The normal body is a live status line.

## Freshness

The notification must remain **always fresh**.

A status older than roughly ten minutes is considered useless.

The primary mechanism must therefore be:

```text
Kernel state change
 ↓
event/hook
 ↓
Web/service worker
 ↓
notification body updated immediately
```

Do not depend on periodic ten-minute polling as the normal update path.

## Status body

Conceptual structure:

```text
Online · {lamp_emoji} {pc_emoji} {generic_emoji} · {motion_state} {air_warning} {temperature}
```

The exact emojis must be copied from the current source.

Rules:

- Lamp emoji shown when Lamp is ON.
- PC emoji shown when PC is ON.
- Generic emoji shown when Generic is ON, subject to current source behavior.
- PC ON for this presentation means Desktop client connected.
- Motion has exactly three presentation states:
  - detected + automatic mode;
  - not detected + automatic mode;
  - Sleep Mode, ignoring motion detection.
- Air warning shown when the existing air-quality warning is active.
- Temperature shows room temperature.

## Event overlay

When a selected event occurs:

```text
normal status
    ↓
event message
    ↓
vibration if enabled
    ↓
configured overlay duration
    ↓
latest status restored
```

The event overlay duration is configurable.

The restored status must be generated from the newest state, not from stale cached text.

## Event selection

The Web interface currently lets users choose which events generate push notification overlays.

Preserve that capability.

Vibration selection is independent and configurable.

The persistent status notification itself must not continuously vibrate.

---

# 53. Client Architecture

## Web

- richest client;
- preserve current visual design;
- preserve dashboard/graphics/graphs/plots;
- use new state/query/command infrastructure underneath.

## Telegram

- reach relevant Kernel capability parity;
- retain current interaction model;
- one updating message per channel/topic context where appropriate;
- update from Kernel state changes.

## Discord

- reach relevant Kernel capability parity;
- retain slash-command interaction model;
- Kernel enforces authorization.

### Discord Voice

Discord voice integration is **out of scope for this refactor** because the current library/integration is broken.

Do not spend implementation effort restoring it.

Also, for youtube, use yt-dlp directly from system installation (mark as required in the README, otherwise just fail and dont fallback to YoutubeExplode)

## Desktop

- remains a client plus computer integration endpoint;
- preserves useful current CLI/agent behavior;
- supports persistent `chat`;
- supports fire-and-forget `ask`.

---

# 54. Client Capability Parity

The clients differ in presentation:

```text
Web       → UI/dashboard
Telegram  → commands + updating message
Discord   → slash commands
Desktop   → CLI/chat
AI        → conversation/tools
```

The underlying Kernel capability should be shared.

No client should intentionally implement a second business-rule system.

---

# 55. API

The API is a public boundary.

Expose capabilities rather than internal class names.

Conceptually:

```text
Devices
Sensors
Automation
Sleep
Schedules
AI
Notifications
Metrics
History
System
```

API endpoints translate into commands/queries.

They do not implement domain rules.

Use explicit DTOs/contracts where exposing domain entities would leak implementation details.

---

# 56. Authentication and Authorization

Authentication belongs at the API/client boundary.

Authorization is enforced in the application.

The client is not trusted to restrict itself.

AI tool availability is also controlled by the application.

Current API authentication behavior can be used as a compatibility reference, but internal endpoint shapes are allowed to change because no retrocompatibility is required.

---

# 57. Persistence

Persistence is infrastructure.

Use domain/application repositories/interfaces where useful.

Examples:

```text
IConversationRepository
IScheduleRepository
ISettingsRepository
IHistoryRepository
IMetricsRepository
```

Storage formats are implementation details.

---

# 58. Redis

Redis is **not required by the target architecture**.

The current implementation uses Redis in parts of IPC/state handling, but the new design should not retain it by default.

Use:

```text
in-process EventBus
HTTP/API
SSE or equivalent state stream
TCP where a machine/hardware protocol actually requires it
```

Introduce Redis only when a concrete requirement proves it necessary.

---

# 59. Process Model

Current independently deployed processes may remain:

```text
CortanaKernel
CortanaWeb
CortanaTelegram
CortanaDiscord
Desktop agent
```

The Kernel is authoritative.

Client process failure must not stop the Kernel.

---

# 60. Bootloader

Bootloader responsibilities:

- start;
- stop;
- restart;
- update;
- supervise processes;
- detect process crashes.

Do not put domain business rules in Bootloader.

Keep distinct:

```text
process health
connection health
sensor availability
device state
```

---

# 61. Failure Principles

Use simple deterministic recovery.

### Client unavailable

```text
client misses events
→ reconnect
→ fetch fresh snapshot
```

### Sensor unavailable

```text
retain last observation
→ mark unavailable
→ let freshness expire
```

### Device GPIO failure

```text
state remains unchanged
```

### Pending Sleep entry + PC ON

```text
cancel timer
```

### Restart

```text
fresh runtime state
→ immediate reevaluation
```

---

# 62. Startup / Restart

Persistent:

```text settings
schedules
conversation history
historical data
```

Transient:

```text Sleep Mode
Sleep Hold
device manual overrides
in-process subscriptions
other runtime-only state
```

On restart:

```text device internal states = OFF
Sleep Mode = inactive
device overrides = cleared
Sleep Hold = cleared
```

Then load persistence and reevaluate current conditions.

Do not restore transient state blindly.

---

# 63. Greenfield Refactor

A new empty project structure is provided.

Use it as the target.

Do not overwrite the current repository in place merely to preserve its layout.

For individual functionality:

```text
copy
partial-copy
rewrite
replace
delete
```

are all valid.

Choose the cleanest implementation.

---

# 64. No Retrocompatibility

Internal and external backward compatibility is not a requirement.

The implementation may break:

- namespaces;
- class names;
- project references;
- internal APIs;
- API endpoint shapes;
- serialization formats;
- persistence formats;
- IPC formats;
- file layouts.

Do not add compatibility wrappers solely to preserve legacy structure.

Preserve only the user-visible behavior explicitly required.

---

# 65. Rewrite vs Edit

Prefer rewriting when the old implementation:

- mixes responsibilities;
- has unclear state ownership;
- embeds transport details in domain logic;
- contains legacy coupling;
- would require many corrective edits;
- makes the target architecture harder to express.

Existing code is a behavior/integration reference.

It is not an architectural constraint.

---

# 66. Comments

Small comments are allowed.

Good:

```text
// PC power must remain on until the Desktop confirms shutdown.
```

Bad:

```text
// Large explanation of the entire automation architecture...
```

Use comments only for short, non-obvious constraints/invariants/external behavior.

Do not use comments to compensate for unclear code.

---

# 67. Testing Boundaries

## Domain tests

Test without external infrastructure:

- automation rules;
- Sleep Mode transitions;
- manual overrides;
- time rules;
- command/state invariants.

## Application tests

Test:

- commands;
- queries;
- authorization;
- capability/tool execution;
- event handling.

## Infrastructure tests

Test:

- GPIO;
- ESP32 transport;
- Desktop transport;
- persistence;
- LLM provider;
- external networking.

## Client tests

Test:

- Telegram mapping;
- Discord slash commands;
- Web state synchronization;
- Desktop CLI/agent.

---

# 68. Final Implementation Rules

The implementation LLM must:

1. read this document and `Cortana_Behavior.md` before changing code;
2. inspect the current repository when reproducing established functionality;
3. use the supplied empty structure as the target;
4. preserve user-facing behavior where required;
5. freely replace legacy implementation;
6. keep domain logic independent of infrastructure and clients;
7. route actions through commands;
8. distribute facts through typed events;
9. keep current state authoritative and snapshots coherent;
10. avoid unnecessary infrastructure.

When a legacy implementation conflicts with this architecture, **rewrite it**.
