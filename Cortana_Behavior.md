# Cortana — Behavioral Specification

**Status:** Target user/system behavior  
**Primary consumer:** An implementation LLM implementing the new architecture.

---

# 0. How to Read This Document

This document defines **what Cortana does**.

`Cortana_Architecture_Design.md` defines **how the system should be structured**.

The current source is used as the reference for established functionality that this specification does not redefine, especially:

- exact Web behavior;
- exact graphics/plots;
- exact existing emoji characters;
- exact sensor/device definitions;
- exact air-quality warning logic;
- existing client wording/menus where retained.

When this document explicitly changes behavior, the new behavior wins.

---

# 1. Core Concepts

Cortana's automation behavior uses separate concepts.

| Concept | Meaning |
|---|---|
| `AutomationEnabled` | Is autonomous automation allowed? |
| `TimeContext` | Is it currently Day or Night? |
| `SleepMode` | Does Cortana consider the user to be sleeping? |
| `DeviceOverride` | Temporary user control of a device |
| `SleepHold` | Temporary suppression of automatic nighttime Sleep Mode |

Do not collapse these into one Automatic/Manual/Night enum.

---

# 2. Automation Enabled

## OFF

When Automation is OFF:

```text
no autonomous device control
current devices remain where they are
Sleep Mode ends
temporary automation/device overrides are cleared
```

Turning Automation OFF is an explicit indefinite user decision until Automation is enabled again.

## ON

When Automation is turned ON:

```text
Automation becomes authoritative immediately
temporary device overrides are cleared
current conditions are evaluated immediately
devices are reconciled immediately
```

Previous manual device state does not become a new override.

---

# 3. Time Context

Time context is derived from the configured time window:

```text
Day
Night
```

Night does **not** mean Sleep Mode.

Night only provides context to automation.

---

# 4. Sleep Mode

Sleep Mode means:

> The user is considered to be sleeping.

It is active/inactive independently from the Day/Night clock.

```text
AutomationEnabled = true
SleepMode = active
```

is normal.

Sleep Mode changes automation rules; it does not turn Automation off.

---

# 5. Sleep Mode Button

The user-facing Sleep Mode control toggles:

```text
Inactive → Active
Active   → Inactive
```

A manual ON command always activates Sleep Mode.

A manual OFF command always deactivates Sleep Mode.

The user may turn it ON again later, even during the same night.

---

# 6. Daytime Sleep

If Sleep Mode is manually activated during Day:

```text
SleepMode = Active
```

Use the same sleep automation behavior as nighttime Sleep Mode.

Its duration is:

```text
DaySleepDuration
```

When the duration expires:

```text
SleepMode = Inactive
→ automation reevaluates immediately
```

Daytime Sleep Mode never starts automatically just because it is daytime.

---

# 7. Nighttime Automatic Sleep

When the configured Night period begins:

### Sleep already active

Do nothing.

Never toggle Sleep Mode.

### Sleep inactive + PC ON

Do not activate Sleep Mode.

Instead notify the PC that it is time to sleep.

### Sleep inactive + PC OFF

Start:

```text
SleepEntryDelay
```

When the timer expires:

```text
SleepMode = Active
```

The timer is independent of motion and other activity.

---

# 8. PC During Sleep Entry

If PC turns ON while `SleepEntryDelay` is pending:

```text
cancel SleepEntryDelay
```

If PC turns OFF while Sleep Mode is already active:

```text
do nothing
```

Do not re-trigger or toggle Sleep Mode.

---

# 9. Manual Sleep While PC Is ON

Manual Sleep Mode activation is allowed even when PC is ON.

This is intentional.

The PC restriction applies only to **automatic nighttime Sleep Mode entry**.

---

# 10. Morning

The configured morning/end-of-night boundary is the wake threshold.

At the boundary:

```text
SleepMode = Inactive
→ automation reevaluates immediately
```

No user action is required.

Do not introduce a separate hard-coded `06:00` setting.

---

# 11. Early Sleep Wake Rules

Only these can end Sleep Mode before its normal end:

```text
user-originated Device-layer action
PC power-on
explicit user Automation ON after the morning threshold
```

Everything else leaves Sleep Mode alone.

The following do not wake Sleep Mode:

```text
sensor events
automation-generated actions
scheduled actions
notifications
AI/background work
settings changes
```

---

# 12. What Counts as a Device Wake Action?

The current Devices layer covers:

```text
Lamp
Power
Generic
PC
```

User-originated actions passing through this layer can be wake evidence at/after the morning threshold.

Settings changes do not count.

A user action made through AI still counts as user-originated.

---

# 13. Sleep Wake Before Morning

Before the morning threshold:

```text
user Device action
→ does not end Sleep Mode
→ may create a Sleep device override
```

This allows the user to interact with devices during the night without Cortana deciding they are awake.

---

# 14. PC Power-On as Wake

PC power-on always ends Sleep Mode.

Cortana does not need to know why the PC powered on.

```text
PC ON
→ SleepMode = Inactive
→ automation reevaluates
```

---

# 15. Normal Manual Device Override

When:

```text
AutomationEnabled = true
SleepMode = inactive
```

a user-originated device action creates a temporary manual device override.

Duration:

```text
ManualOverrideDuration
```

Rules:

- duration starts at the first manual action;
- subsequent manual actions do not reset it;
- sensor events do not reset it;
- automation events do not reset it.

When it expires:

```text
automation evaluates current state
```

Do not replay events from during the override.

---

# 16. Manual Override Example

```text
10:00  User turns Lamp ON
       → override until 10:10

10:05  Motion detected
       → ignored by override

10:07  User turns Lamp OFF
       → timer remains until 10:10

10:10  override expires
       → evaluate current state
```

---

# 17. Sleep Device Override

While Sleep Mode is active, a user-originated device action creates a temporary device override using:

```text
SleepManualOverrideDuration
```

One global duration applies for now.

It works for both:

```text
device ON
device OFF
```

The device override does not change Sleep Mode lifetime.

---

# 18. Entering Sleep Mode

When Sleep Mode activates:

```text
existing device manual overrides are cancelled
sleep rules become authoritative
```

This prevents a daytime manual override from leaking into a sleep session.

---

# 19. Sleep Hold

At night, when Sleep Mode was active automatically, the user may toggle it OFF.

That creates:

```text
SleepMode = Inactive
SleepHold = Active
```

Automation remains enabled.

Sleep Hold temporarily suppresses automatic Sleep Mode re-entry.

When Sleep Hold expires:

```text
automation reevaluates
```

If nighttime automatic sleep is still applicable:

```text
SleepMode = Active
```

---

# 20. Sleep Hold Duration

Sleep Hold has its own configurable duration.

It is independent of:

```text
ManualOverrideDuration
SleepManualOverrideDuration
DaySleepDuration
SleepEntryDelay
```

Device actions during Sleep Hold do not reset or extend Sleep Hold.

---

# 21. Sleep Toggle During Hold

If the user turns Sleep Mode OFF and later turns it ON again:

```text
SleepMode = Active
SleepHold = cancelled
new sleep period begins
```

Cortana follows explicit user intent.

---

# 22. Night vs Sleep

The intended distinction is:

```text
Night
    = clock/time context

Sleep Mode
    = user/system sleeping state
```

At night:

```text
PC ON
→ Sleep Mode may be blocked automatically
→ PC is notified
```

This is why "Night Mode" is not the correct user-facing concept.

---

# 23. Motion

Motion is represented by:

```text
LastMotionAt
```

Motion-active behavior is derived.

The timeout depends on PC state:

```text
PC ON  → longer timeout
PC OFF → shorter timeout
```

PC state changes trigger immediate reevaluation.

---

# 24. Sensor Failure and Motion

When the ESP32 becomes unavailable:

```text
retain LastMotionAt
mark sensor unavailable
```

Continue using the timestamp while it is fresh.

Once it ages beyond the applicable timeout:

```text MotionActive = false
```

This prevents a failed sensor from keeping the lamp on indefinitely.

When the ESP32 reconnects:

```text first valid reading
→ accepted immediately
```

---

# 25. Device State

GPIO device state is Cortana's internal logical state.

The GPIO outputs are not read back.

Therefore:

```text successful GPIO operation
→ internal state changes

failed GPIO operation
→ internal state remains unchanged
```

The state represents what Cortana believes the device state to be.

---

# 26. Device Startup

Every Kernel restart begins with:

```text all device internal states = OFF
```

This does **not** mean physically switching outputs OFF.

Startup must not send implicit OFF commands because one GPIO-controlled device controls PC power.

Explicit ON/OFF commands after startup are allowed.

---

# 27. Room OFF

The existing Room OFF user operation is:

```text
Lamp OFF
Power OFF
```

---

# 28. Room ON

Room ON is coordinated behavior:

```text Power ON
 ensure PC ON
```

Lamp behavior depends on automation authority.

### Manual mode / Automation not authoritative

```text Lamp ON
```

### Automation authoritative

```text Lamp is decided by Automation
Room ON must not blindly force Lamp ON
```

The current exact behavior should be checked against the source implementation.

---

# 29. PC Power-Off

When the user requests PC power-off:

```text
request Desktop shutdown
↓
wait until PC is actually OFF
↓
wait additional delay
↓
switch physical Power OFF
```

The extra delay accounts for the Desktop client closing before the PC has fully powered down.

Never cut physical PC power immediately after sending the shutdown request.

---

# 30. PC State

For user-facing status:

```text PC ON = Desktop client connected
```

This definition is especially important for the push notification.

---

# 31. Generic Device

Generic is location-dependent.

```text Orvieto → speaker
Pisa    → lamp-equivalent
```

Pisa has no dedicated Lamp connection, so Generic replaces it there.

Generic should preserve the relevant legacy semantics in both locations.

The legacy relay/pulse Lamp option remains available but disabled.

---

# 32. Scheduler

Schedules describe when actions happen.

Scheduled execution uses normal commands.

Persistent schedules survive restart.

Event-triggered schedules can trigger for each matching event.

The legacy missed-run behavior may be simplified while retaining the useful user-facing intent.

---

# 33. AI Capability

AI is a conversational interface over Cortana.

A user should be able to ask AI to:

```text
read state
control devices
control automation
control Sleep Mode
manage schedules
inspect computer
inspect notifications
inspect settings
inspect history
inspect metrics
analyze measurements
explain decisions
```

AI uses the same underlying application capabilities as other clients.

---

# 34. AI Analysis

Use deterministic application functions for calculations.

Examples:

```text
average temperature
maximum temperature
minimum temperature
temperature at time
average air quality
worst air quality period
trend
count
duration in state
```

The LLM interprets the question and explains the result.

It should not manually calculate from huge raw datasets when a deterministic function can do it.

---

# 35. AI `chat`

`chat` is persistent conversation.

```text
user message
→ conversation history
→ AI
→ tools if needed
→ response
→ history persisted
```

History survives restart.

---

# 36. AI `ask`

`ask` is fire-and-forget.

It may use tools, including mutating tools when authorized.

It does not load or persist conversational history.

```text
ask
→ capability execution
→ result
```

---

# 37. AI Diagnostics

AI should be able to answer questions like:

```text
Why did the lamp turn on?
Why didn't it turn on?
Why didn't Sleep Mode activate?
Why did Cortana notify the PC?
```

Use explicit diagnostic state/events/timers/origins.

Do not let the LLM invent a reason from incomplete context.

---

# 38. AI Origins

When a user asks AI to operate a device:

```text
Actor = User
Source = LLM
Surface = originating client
```

The command remains user-originated.

This matters for Sleep wake behavior and future authorization.

---

# 39. Notifications

Normal notifications are driven by:

```text
event
→ policy
→ notification
→ delivery
```

Not every internal event needs to notify a user.

---

# 40. Web Service-Worker Push Notification

The persistent browser/service-worker notification is a special status surface.

## Title

```text
empty
```

Never add a title.

Android renders the title poorly for this notification.

## Body

The body is either:

1. normal current status; or
2. a temporary event message.

---

# 41. Normal Push Status

Conceptually:

```text
Online · {lamp_emoji} {pc_emoji} {generic_emoji} · {motion_state} {air_warning} {temperature}
```

Use the exact emoji characters already used in the current source.

Do not invent new ones.

### Lamp

Show Lamp emoji only when Lamp is ON.

### PC

Show PC emoji only when PC is ON.

PC ON here means Desktop client connected.

### Generic

Show Generic emoji when Generic is ON, according to current source behavior.

### Motion

Exactly three display modes:

```text
motion detected + automatic mode
motion not detected + automatic mode
Sleep Mode
```

Sleep Mode display ignores motion.

### Air warning

Display when the current air-quality warning condition is active.

Keep the existing air-quality warning logic.

### Temperature

Display room temperature.

---

# 42. Push Freshness

The normal push notification must be **always fresh**.

A state older than roughly ten minutes is considered useless.

Normal path:

```text
Kernel state change
→ event/hook
→ service worker update
→ newest notification status
```

Do not use a ten-minute timer as the normal refresh mechanism.

A freshness safeguard may exist, but event-driven updates are mandatory.

---

# 43. Push Event Overlay

When an accepted event occurs:

```text
status
 ↓
event message
 ↓
vibration if enabled
 ↓
configured duration
 ↓
latest status
```

The normal status restored afterwards must be generated from current state.

Do not restore stale text from before the event.

---

# 44. Push Event Selection

The Web interface currently lets the user choose which event types trigger push notification overlays.

Preserve this.

The selected set is user-configurable.

---

# 45. Push Vibration

Vibration is independent from event acceptance.

For an accepted event:

```text
event selected + vibration enabled
→ vibrate
```

Otherwise:

```text
event selected + vibration disabled
→ no vibration
```

Normal status changes do not vibrate.

---

# 46. Web

The Web remains the richest user interface.

Preserve:

- dashboard;
- layout;
- graphics;
- plots;
- graphs;
- existing controls;
- existing visual language;
- current UX where not intentionally changed.

The data/API/state layer may be completely rewritten.

---

# 47. Telegram

Telegram should have capability parity with relevant Kernel functions.

Keep the current interaction style:

```text
one updating message per channel/topic context where appropriate
```

State updates should come from Kernel state changes.

Telegram should not own business logic.

---

# 48. Discord

Discord should have capability parity with relevant Kernel functions.

Keep:

```text
slash commands
```

as the primary interaction style.

Authorization is enforced by the Kernel/application.

## Voice

Discord voice/voice-channel integration is out of scope for now because the current library is broken.

Do not restore it during this refactor.

---

# 49. Desktop

Desktop remains both:

```text client
+
computer integration endpoint
```

It should retain current useful functions.

The Desktop CLI must support:

```text
cortana chat
cortana ask "..."
```

with the semantics defined above.

---

# 50. Current Version as User-Behavior Reference

The existing project should be checked when the new implementation needs to reproduce existing functionality.

Especially inspect the current source for:

- exact emojis;
- exact sensor/device definitions;
- air-quality warning thresholds/logic;
- existing Web behavior;
- graphs/plots;
- Telegram interaction;
- Discord slash-command behavior;
- Desktop behavior;
- current command details.

The goal is:

```text
existing good user behavior
→ preserved

existing implementation
→ replaceable
```

---

# 51. Refactor Visibility

From the user's perspective, the refactor should be **almost invisible** for existing functionality.

"Almost" allows:

- internal timing/implementation changes;
- new capability parity;
- improved architecture;
- changed APIs/IPC/storage;
- intentionally changed semantics described by this document.

Do not redesign the Web visuals merely because the backend is being rewritten.

---

# 52. Restart

Runtime state is fresh on every restart.

Start with:

```text device states = OFF internally
Sleep Mode = inactive
Sleep Hold = inactive
device overrides = cleared
```

Then:

```text load persistent data
observe current environment
evaluate automation
reconcile
```

Do not restore transient runtime state blindly.

---

# 53. Persistent Data

Survive restart:

```text schedules
conversation history
settings
historical measurements
other intentional persistent configuration
```

Do not persist merely to restore temporary runtime state such as active Sleep Mode or manual overrides.

---

# 54. Client Failure

A Web/Telegram/Discord client process may fail without stopping the Kernel.

The process supervisor handles restart.

A client reconnects and fetches the current snapshot.

---

# 55. Implementation Philosophy

This is a greenfield rewrite.

The supplied empty project structure is the starting point.

Existing files can be:

```text
copied
partially copied
rewritten
replaced
deleted
```

No retrocompatibility is required.

Breaking old internal APIs, file formats, IPC, namespaces, and project structure is acceptable.

---

# 56. Comments

Small comments are allowed when they explain a non-obvious constraint.

Keep them short.

Do not add long architectural comments.

---

# Appendix A — Confirmed Decisions

- Automation OFF disables autonomous control.
- Automation OFF ends Sleep Mode and temporary overrides.
- Automation ON immediately takes authority and reevaluates.
- Night and Sleep Mode are separate concepts.
- Sleep Mode is the user/sleeping state, not a clock mode.
- Daytime Sleep Mode has a fixed configurable duration.
- Nighttime Sleep Mode normally lasts until morning.
- Automatic nighttime Sleep Mode is blocked while PC is ON.
- PC ON at night triggers a PC notification instead.
- PC OFF starts a configurable sleep-entry delay when automatic sleep is pending.
- PC ON during the delay cancels it.
- PC OFF while Sleep Mode is already active does nothing.
- Manual Sleep Mode can activate while PC is ON.
- Morning boundary automatically ends Sleep Mode.
- Before morning, device actions do not wake Sleep Mode.
- At/after morning, user Device-layer actions may wake Sleep Mode.
- PC power-on always wakes Sleep Mode.
- Explicit user Automation ON after the morning threshold wakes Sleep Mode.
- Sensors and autonomous system activity never wake Sleep Mode.
- Normal device overrides are fixed-duration.
- Later manual actions do not reset the normal override timer.
- Sensor/automation events do not reset it.
- Override expiration reevaluates current state only.
- Sleep device override has its own global duration.
- Sleep Hold is independent from device overrides.
- Entering Sleep Mode clears existing device overrides.
- Sleep Mode OFF at night creates Sleep Hold.
- ESP32 serves sensors; Raspberry GPIO serves local devices.
- Sensor failure retains last observation until stale.
- Motion uses LastMotionAt.
- Motion timeout changes immediately with PC state.
- GPIO device state is internal/logical.
- Device internal state starts OFF, but startup must not physically switch outputs OFF.
- Successful GPIO operation changes internal state.
- Failed GPIO operation leaves it unchanged.
- AI can access meaningful user capabilities through application tools.
- AI supports deterministic analysis over history/metrics.
- `chat` persists history.
- `ask` is fire-and-forget and tool-capable without persistent history.
- AI-generated user-requested actions remain user-originated.
- Web keeps existing visual design.
- Telegram keeps updating-message-per-topic behavior.
- Discord keeps slash commands.
- Discord voice is out of scope for now.
- Service-worker notification has no title.
- Service-worker notification is persistent and event-updated.
- Event overlays temporarily replace the status body.
- Event vibration is separately configurable.
- Notification status uses the existing source emoji set.
- Room OFF means Lamp OFF + Power OFF.
- Room ON controls Power/PC and lets Automation remain authoritative over Lamp.
- PC power-off waits for actual shutdown before cutting physical power.
- Generic maps to speaker in Orvieto and lamp-equivalent in Pisa.
- Legacy relay/pulse lamp option remains available but disabled.
- No retrocompatibility is required.
