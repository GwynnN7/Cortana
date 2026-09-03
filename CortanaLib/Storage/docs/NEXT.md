# Next session — the plan

Written at the end of a long session, with the fabric refactor landed and deployed. Everything here
is either something gwynn7 asked for, something left unfinished, or something I noticed and did not
fix. Ordered so the architecture settles before the UI is rebuilt on top of it

---

## 1. Automation, presence, and what a bind to automation means — DECIDED

**gwynn7's proposal:** if automation *is* the implementation of presence, rename `presence` to
`automation`, and `FeedsPresence` becomes "bind to automation"

**How the computer currently reaches it:** the desktop agent publishes `at_desk` (true when the PC is
connected, unlocked and not idle) and `locked` as ordinary sensors on the `computer` source.
`at_desk` carries `FeedsPresence: true`, so it contributes to presence exactly like the PIR does.
Nothing about the desk is special-cased any more

**Before renaming, one thing to settle.** `presence` and `automation` are not the same kind of thing:

- `presence` is a **reading** — "someone is here" — computed from every `FeedsPresence` sensor
- automation is the **system** that decides to act on readings

Right now `presence` is one sensor a bind can reference, alongside `light`. If it is renamed
`automation`, then "bind the lamp to automation + light" reads as though automation were an input,
which hides that the real inputs are motion and the desk

**DECIDED: keep them separate (option a).** `presence` stays a reading; a device follows automation by
having a bind at all. The night and sleep gates still move into triggers, which is the part that
matters — that is done

Two ways to go, and this is the decision to make first:

- **(a) Keep them separate.** Rename `FeedsPresence` to something clearer (`FeedsPresence` →
  `Presence` on the sensor, exposed in the UI as "counts as presence"). A device "follows automation"
  simply by having a bind at all
- **(b) Take gwynn7's framing.** Rename the sensor to `automation`, and treat "bind to automation" as
  sugar for "bind to the composed presence signal, plus the night/sleep gates". Then the night and
  sleep rules stop being invisible engine behaviour and become part of what the bind says

(b) is closer to what he described and makes the engine's hidden gates explicit, which is worth a lot.
It needs the night/sleep gates lifted out of `AutomationEngine.Evaluate` and expressed as triggers.
**Ask him which, then do it — do not guess**

### How (b) actually looks

Not a composite sensor — binds already take several triggers, so a composite would duplicate
machinery that exists. The Kernel exposes `night` and `sleep` as boolean sensors, and the lamp bind
becomes:

```
lamp <- presence  IsTrue    (sustains)
        light     Below 60  (not sustains)
        sleep     IsFalse   (sustains)
```

`Mode: All` already ANDs them and `Sustains` already separates "keeps it on" from "needed to switch it
on". The gain is that `SleepMode ? Off` stops being invisible engine behaviour and becomes a trigger
that can be seen and removed

Three details:

- **`ComputerOn` is probably the wrong trigger.** `at_desk` already exists and is more precise
  (connected, unlocked, not idle). Note that triggers read **sensors**, not device states — a device's
  power state is not currently readable by a bind, and making it so is a separate decision
- **Night is deliberately not a lamp gate.** Night and sleep used to be one state, so motion did not
  light the lamp after `NightHour`. They are separate now: at night, before sleep, motion still lights
  it. So `night` becomes an available sensor, not one the lamp bind has to use
- **A composite sensor earns its place later.** When two devices should follow identical conditions,
  repeating triggers per bind gets tedious and a reusable composite would be DRY. Second feature, not
  first — the trigger list costs nothing and works today

## 2. Rewrite the Hardware page with tabs — DONE

Tabs like the Sensors page: **Sources · Devices · Sensors · Binds · Warnings**

Each tab gets the room to do add/edit properly instead of the cramped inline `field-row` forms that
are there now. Specifically fix, while rewriting:

All four gaps are closed: the trigger form picks which bind it joins (or starts a new one, with a
unique id), `Sustains` toggles by clicking the trigger chip, devices and sensors have an Edit button
that loads them back into the form and saves under the same id, and warning triggers choose above or
below. A bind left with no sustaining trigger says so, both in the UI and in the API's reply

## 3. Devices and Sensors pages: tabs per source — DONE

Both pages get a tab per source, showing that source's devices or sensors

**The hardcoded rendering gwynn7 dislikes:** station uses tiles, computer and Pi use bars. He is right
that this is arbitrary. The honest fix is that a bar needs a **range** and a tile does not — CPU is
0–100 %, CO₂ has no natural ceiling

So: add optional `Min`/`Max` to `VirtualSensor`. A sensor with a range renders as a bar, one without
renders as a tile. The choice stops being per-page and becomes a property of the sensor, which is
where it belongs. Seed ranges for the machine sensors (0–100 for loads, sensible ceilings for temps)
and leave the room sensors without

`SourceCard` no longer renders its own bars: it renders `SensorGrid` over that source's sensors,
then the memory/disk line in GB, then music, then the ribbon. Every machine sensor is a registered
sensor with a range, so the bars come from the fabric like everything else

## 4. Dashboard rebuild — DONE

- Keep the chat
- Sensor section where **each sensor can be added by hand** (per-user choice of what shows)
- Same for Quick Devices
- Sleep and the Automation switch move to their own card
- `hyprcachy` and `raspberry` keep the bar view, **reusing the same component built for the sensor
  section**, and always show every sensor of that source

The preference lives in the Kernel as `DashboardLayout` behind `GET`/`POST /layout`, so it survives a
browser change. The dashboard falls back to every station sensor and every device the first time.
The push status line reads the same layout, so the phone shows what the dashboard shows

## 5. Re-architecture pass, especially automation — DONE

`AutomationEngine` is 500 lines and mixes at least five concerns:

- automation on/off authority
- sleep mode and its entry delay, hold and daytime variant
- day/night context
- manual holds on devices
- evaluating binds

The bind loop is clean; everything around it is not. Worth splitting sleep and the day/night context
into their own pure rule types, leaving the engine as a scheduler that ticks them

Also:

- `SourcesOnline` is now "every announced source is online" and `WarningActive` is "any warning is
  firing". `WarningStateChanged` carries the warning's id, the notification source is `Warnings`, and
  the schedule event is `WarningRaised`
- `RoomState` split into `PresenceState` (the last-motion latch) and `WarningState` (which warnings
  are firing)
- Automation reads `Idle` when it is on with no enabled bind
- **A multi-channel device is on when any of its channels is on.** State moved off the device and
  onto the channel, so the Lamp and the Room can never disagree about pin25 again
- The pre-handshake `computer`/`esp32` branch is gone from `ConnectionServer`; every client announces
  itself. `Esp32SensorSource` became `StationSource` and holds one connection per station, so a second
  station is just another announcement
- Outputs are no longer Pi-only: `IChannelWriter` is chosen per source, with the GPIO header and a
  station writer that sends `SourceCommand` down the station's own socket
- History columns are whatever the sample holds — every registered sensor and device — written into
  the day's CSV, widening the file when a registration appears. Units come from the sensor
- `Evaluate` is serialised. Readings, the tick, the computer and the endpoints all reached it from
  their own threads, so two passes could both see the lamp off and both switch it on — visible as a
  doubled `[Automation] Lamp on` at every boot. One lock around the pass, and it happens once
- The dead-code audit ran: `Units.For(sensor)`, `Units.ForMetric`, `AutomationRules.AirQualityUnsafe`
  / `AirQualityBackToNormal` / `LampInput`, `SettingsStore.Decimal`, `CortanaState.SensorValue` and
  the duplicate `Fabric` field in `PushService` are gone

### What is left in automation once binds absorb the gates

Asked directly, and worth writing down because it drives section 5

**Stays in automation:**

- the bind evaluation loop
- manual holds — you switch a device by hand and automation backs off for N minutes. Genuinely
  automation's job, with nowhere else to live
- the master on/off
- the tick that expires holds

**Leaves automation — sleep DONE:**

- **day/night** becomes a computed sensor. The Kernel already derives it from `MorningHour` and
  `NightHour`; publishing `night` as a boolean is the whole change. It stops being automation state
- **sleep mode** is now `Domain/Automation/SleepEngine.cs`, reached through an `ISleepHost` seam that
  gives it the time context, whether automation is on, hold clearing and re-evaluation. It never
  touches automation's internals. `AutomationEngine` went 528 → 354 lines
- **day/night** is now `Domain/Automation/DayNightClock.cs`. It owns the context, `Establish` sets it
  at startup and `Advance` reports the change, so `Tick` only reacts to it

`AutomationEngine` therefore goes from five mixed concerns to about two: evaluate binds, respect
holds. That *is* the clarity asked for in section 5, so do this before splitting anything else

## 6. Docs page: a services section — DONE

List every service — memory, history, volition, automation, sleep, push, schedules, activity,
warnings — and whether it is active. Read-only for now

**The switches already exist, scattered as settings.** `AutomationEnabled`, `NotifyWeb` /
`NotifyTelegram` / `NotifyDiscord`, `MemoryDepth: 0`, and volition's `Quiet` are all de-facto service
toggles. So this page consolidates what is there rather than inventing a mechanism, which is why the
plugin framing holds up — and yes, disabling automation from here would be the same act as clearing
`AutomationEnabled` today

### Decided

**"Off" means inert, not invisible.** A disabled service stops *doing* its thing and the AI can no
longer use it, but everything it already produced stays and stays readable. Disabled History stops
recording; the existing CSVs remain. Disabled Memory stops storing and stops being injected; the
entries remain on the page

**The dependency between services is through sensors, and there is a real hazard there.**

Once `night` and `sleep` are sensors, disabling either service means their readings stop. And
`BindRules.Holds` is currently `reading is not null && …`, so **a missing reading makes a trigger
false** — which for a *sustaining* trigger means "switch the device off". Disabling the sleep service
would therefore turn the lamp off rather than leave it alone. The same happens whenever the station
drops

**DONE — suspend the bind.** A trigger whose sensor has no reading suspends the whole bind: no target,
the device is left exactly as it is, and `BindDecision.Suspended` carries a reason such as
`waiting on sleep`. Verified live: a bind on a nonexistent sensor reported `waiting on nonexistent`
and switched nothing

`night` and `sleep` are now Kernel sensors, and the lamp's sleep gate moved out of
`AutomationEngine.Evaluate` into the bind as a sustaining `sleep IsFalse` trigger

**DONE.** Every bind carries a `BindStatusView` — outcome, reason, and whether it is suspended — and
the Hardware page prints it under the triggers, in red when suspended

**What automation losing sleep means.** Today turning automation off also releases holds and cancels
sleep mode. Once sleep is its own service, disabling automation no longer stops sleep — correct, but a
behaviour change worth intending

## 6b. Services as a scope audit — RUN

The services page is also a way to check the architecture is actually divided correctly, which is half
the reason to build it. Two properties to verify service by service:

- **Disabling one thing disables only that thing.** Everything else stays up, possibly with fewer
  capabilities, and nothing crashes. If disabling X takes down Y, the split is wrong
- **One thing is not spread across two services.** If disabling X leaves half of X still running under
  another name, they should be one service

Some cannot be disabled at all — Settings is the obvious one, and the fabric itself

### What the audit found

Twelve services are listed. Eleven pass both properties. The one that does not:

- **Sleep still rides on automation's switch.** Turning automation off suspends sleep mode, so the two
  rows move together. The page now says so — Sleep reads "follows automation" and offers no switch of
  its own — rather than pretending to a switch it does not have. Giving sleep a real switch is a
  behaviour change (automation off would no longer cancel sleep), so it waits on gwynn7

Everything else degrades the way Volition does. Disabling Memory stops injection and storage but
leaves the entries readable. Disabling History stops sampling and leaves the CSVs. Disabling Warnings
stops the notifications and leaves the thresholds. Notes and the wrap-up are always on and hold no
switch, because neither acts on the house

**Volition is the model to copy.** It can be disabled manually to stop unprompted interaction, and
telling Cortana to be quiet disables it temporarily. Meanwhile the AI keeps running with fewer
capabilities. That is exactly the shape every service should have: a clean off switch, a temporary
form, and graceful degradation in whatever depends on it

### Migration when shipped defaults change

`Fabric.Seed` is now **additive** — it adds any default source, device or sensor whose id is not
already registered, so new shipped sensors reach an existing install. `night` and `sleep` arrived that
way with no manual clearing

Two caveats:

- Deleting a shipped default brings it back on the next restart. Acceptable while the model is still
  moving; if it becomes annoying, record deletions as tombstones
- **Bind and warning contents are never migrated**, deliberately — editing a user's triggers is
  presumptuous. So a change to a default bind only reaches fresh installs, and existing ones must be
  updated by hand. That bit me once already: adding `sleep IsFalse` to the shipped lamp bind did not
  reach the Pi, so sleep briefly stopped suppressing the lamp until the live bind was updated

## 7. Cascade rules when deleting — DONE

Implemented and verified. `DELETE /registrations/{id}` now purges through `BindStore.Purge` and
`WarningStore.Purge`, and re-evaluates:

```
'tvoc' removed, and air-quality lost its tvoc trigger
'lamp' removed, and lamp-on-motion removed with its device
```

Still open from the original list: nothing warns when the last **sustaining** trigger is removed,
leaving a device that can never switch off. Deletes cascade silently rather than confirming

Original notes:

- **Delete a device** → delete every bind for that device
- **Delete a sensor** → if it is a bind's only trigger, delete the bind; otherwise remove just that
  trigger and keep the bind
- **Same for warnings** — a warning whose only trigger references a deleted sensor should go; one with
  others should lose the trigger
- Deleting the last *sustaining* trigger from a bind is worth warning about: the device would then
  never switch off

Decide whether these cascade silently or ask first. Silent for triggers, confirm for whole binds,
seems reasonable

## 8. Icons chosen per registration — DONE

When creating a virtual device, sensor or warning, pick the emoji shown for it. Set on the
registration so every client renders the same thing — the Kernel is already the only source of truth
for names, and this closes the last hardcoded display decision

Needs **at least two per entry**, because these are states not labels:

- **Devices** — on and off
- **Boolean sensors** — true and false
- **Numeric sensors** — below and above the threshold, so the same mechanism covers them

`VirtualDevice.Icon` and `VirtualSensor.Icon` are single strings today, used as CSS icon names. They
become a pair. Note `State.MotionIcon` and `DeviceCard.Emoji` in the web client are hardcoded maps
that this replaces

## 9. Everything else outstanding — DONE

**Asked for earlier, now built:**

- **Notes** — `NoteStore` behind `/notes`, with kinds Personal, Feature and Link, a page in the Web,
  and three capabilities so Cortana can write one down, read them back and settle one. A note is a
  task; Memory stays for who gwynn7 is
- **Scheduled wrap-ups** — every evening at `WrapupHour`, `HistoryService.Digest` reduces the day
  (every sensor's range, every device's on-time, minutes per activity category, music) and Cortana
  writes one or two sentences about it. That is stored as a short-term memory whatever happens, and
  said out loud with probability `WrapupChance`. One model call a day, so the free tier is safe
- **Persistent web chat** — the browser no longer holds a random conversation id: every browser shares
  the conversation `web`, `GET /ai/{conversation}` returns its turns, and the panel loads them on
  open. The morning greeting and the wrap-up are appended to it, so an unprompted message lands in
  the dashboard as well as in the notification and can be answered there
- **Mood bound to activity** — happy at the desk, watching when fullscreen or gaming, bored when the
  desk is locked or the computer has been off 45 minutes, alone when nobody has been around for
  hours, calm otherwise. Happy left the random nominal rotation, since it now means something

**Smaller things I noticed and left:**

- `station/air_temperature` is announced but unregistered — free, and probably should stay that way
- **Telegram and Discord come last.** They compile against the new model but their menus were never
  redesigned around sources and still assume a small fixed device list. Leave them until the Kernel
  and Web are settled and gwynn7 says to start
- Warning hysteresis is fixed at ×1.15 / ×0.9. Fine for now, but `Below` triggers use `2 - margin`
  which is correct yet unobvious
- `MetricsRegistry` and the fabric still both hold machine readings. The cards now read the fabric,
  so `MetricsView` is down to host, os, uptime and the absolute GB — worth folding into the fabric one
  day, but no longer duplicated on screen

**Verify before trusting:** device state resets to Off whenever the Kernel restarts, because GPIO
outputs cannot be read back. That is pre-existing, and it now resets per channel

---

## Where things stand right now

Sections 1 to 8 are done. The fabric model is live — hardware declares channels as tags, virtual
devices and sensors are registered on top, several devices may share a channel, and one device may
span several. Binds drive automation, warnings are configurable, both machines publish their metrics
as ordinary sensors, and the Web is rebuilt on top of all of it: Hardware has five tabs with editing,
Sensors and Devices are tabbed by source, and the dashboard shows what you pick

Every section is done, Telegram and Discord included — they read the house and switch what switches,
and deliberately configure nothing.

**What is actually left**, and none of it is from the numbered sections:

- ~~**IDEAS §3 wants the daily digest kept**~~ — done. `Days.json` holds a `DaySummary` per day and
  `POST /history/days` backfills from CSVs on disk. `Rhythm(metric)` compares today with the median of
  the same weekday, and the AI has it as `CompareToUsualDay`
- **Editing a registration cannot rename its id.** Deliberate — history columns and binds key off it.
  gwynn7 has decided this stays as it is
- ~~**`MetricsRegistry` and the fabric both hold machine readings**~~ — done, and generalised rather
  than folded in. A source now carries **facts** as well as readings: `name`, `os`, `uptime`, the GB,
  and whatever else it can offer. The station gives its chip, IP and signal; the Pi and the desktop
  give what they always did. `MetricsRegistry` and `MetricsView` are gone, and a machine card is just
  a source
- ~~**`ActivityDetail` has no UI**~~ — done. The level rides on every activity update and is set from
  the Logs page or by the AI, which writes it back to `activity.conf`
- ~~**Shipped bind and warning contents are never migrated**~~ — still true by design, but no longer
  invisible: the bind and warning lists report which shipped ones drifted and offer Reset
- Everything under "Smaller things I noticed and left" below

### The pass after the first look at it

- **The status line is chosen, not guessed.** Devices, sensors and warnings each carry `InStatus`.
  A device contributes its on-icon while it is on, a warning its icon while it fires, a sensor its
  value when it is a number and its on/off icon when it is a boolean. Order is devices, warnings,
  readings
- **Machine cards read like they used to.** Every numeric sensor carries a range again, so it renders
  as a bar in registration order; only booleans stay tiles. The bar label column is fixed-width and
  no longer wraps, and each bar carries the sensor's icon
- **Features moved from Docs to Core**, next to the systemd units (now called Units), with real
  switches: `POST /plugins/{id}`. Memory, History and the wrap-up gained their own on/off flags
  rather than pretending a value of zero was a switch. Docs kept the API and the notes, which is what
  Docs means
- **Hardware edits properly now.** Binds carry a name, are editable (name, mode, whether they back
  off), and only show the threshold boxes a trigger kind actually reads — two for `Outside`, none for
  `IsTrue`. Warnings are editable, choose their icon, level, cooldown and condition, and their
  triggers add and drop like a bind's. Changing tab clears the draft
### The pass after seeing it on the phone

- **Warnings take the same `Trigger` as binds.** `WarningTrigger` and `RequiresPresence` are gone: a
  sustaining trigger gates the warning, the rest raise it, so "only when someone is here" is now
  simply `presence IsTrue` and any sensor can gate anything
- **Triggers are added and edited where they live.** `TriggerEditor` is one component, used by the
  create forms and by an editor that opens inside the bind or warning being changed
- **Mobile.** `.memory` wraps instead of squeezing, the edit box opens on its own row under the
  buttons, and the plugin switches are one uniform control — disabled rather than replaced by a badge
  when a service has no switch of its own
- **A dropped source loses its readings.** `at_desk` stayed true all night with the computer off and
  kept presence with it. `Fabric.Dropped` now clears every reading of that source, so a sensor whose
  source is gone reads as unavailable rather than as its last value
- **A number has one icon.** The icon only swapped at 85% of the maximum, which is a threshold nobody
  asked for. Only booleans have two states to draw, and the sensor form only offers the second icon
  for them
- **Bars group by their shared word.** CPU and CPU Temp sit under one CPU heading again, with the
  distinguishing word as the row label and the unit standing in for the base sensor
### The pass that made every form a modal

- **Creating and editing are the same dialog.** Devices, sensors, binds, warnings and schedules all
  open a `Modal` — blank for New, filled for Edit — so nothing writes into a form that is also
  sitting on the page. A bind or a warning holds its conditions in that dialog: add, flip between
  sustaining and raising, remove, then save once
- **Twelve features have a switch.** Sleep, Warnings and Notes joined Memory, History and the wrap-up
  as real flags in `SettingKey`, honoured where they act — no warning evaluation, no note read or
  written, no sleep state machine. The Notes and Memory pages say so and leave the nav when off,
  which the snapshot now carries as `Plugins`
- **`AiSettingKey` reads its file key by key.** Removing three members reset every AI setting to its
  default, the same bug the string settings had. Values were still on disk and came back
- **`ScheduleActionType.SwitchRoom` is gone**: the room is a device like any other, so a schedule
  targets it through `SwitchDevice`, and schedule actions print device names instead of ids
### The power incident, 3 September

gwynn7 found the mains socket off. What the journal shows: the computer dropped at 11:31, a
Wake-on-LAN went out at 11:32:55, and the PC booted at 11:33. That wake is only sent by
`SwitchComputer(On)`, so the last part is him turning Power back on. What cut it in the first place is
not recorded, and **that is the first defect**: `Apply` published an event and logged nothing, so
there was no record of who switched what.

The mechanism that can do it without any command is the second: **`GpioDeviceController.Dispose`
closed every pin it had opened.** Closing a line releases it, and a released line stops holding its
relay, so every Kernel restart could physically switch the house — and the Kernel restarted eight
times that morning. It no longer closes them.

Both are fixed, plus the underlying weakness that made a restart a coin toss at all:

- **`Channels.json` remembers what each output was last written to**, and `DeviceService.Restore`
  re-asserts it at boot. Verified live: `[Devices] Restored raspberry/pin23 to on`. This also closes
  the long-standing "device state resets to Off on restart" wart
- **Every applied write is logged** with the device, its channels, the actor, the surface and the
  reason

### Follow-ups on the modal pass

- **Schedules show device names.** The target list was rendering ids — `generic`, `lamp` — because it
  returned one string used as both value and label. It returns `(id, label)` pairs now
- **The dashboard's modes are boxes.** Automatic and Sleep sit in a quick grid like the devices above
  them, and that section is Quick Devices
- **A trigger kind is suggested, never enforced.** Any kind works on any sensor — a number is true
  above 0.5, a boolean compares as 0 or 1 — so picking a sensor only moves the selector to the kind
  that usually fits, and every other kind stays available for both binds and warnings
- **A switched-off feature keeps its page.** The nav no longer hides Notes or Memory; open either and
  it says it is off and points at Core, on any screen
- **The computer and its supply are found, not named.** `Fabric.Machine` is the device registered on a
  `Computer`-kind source and the supply is its `PoweredBy`, so Wake-on-LAN and the shutdown-then-cut
  sequence no longer key off the ids `computer` and `power`
- **Ids stopped leaking to the AI.** She was shown `lamp, generic, power, computer, room`, which is
  why "turn on the speakers" failed while "power" worked. Every surface now resolves by id **or**
  name, and she is shown names
- **More hardcoding gone**: the device toast said `generic switched Off` instead of `Speakers`, the
  activity ribbon correlated a fixed `co2`, the history panel kept its own table of labels and ranges,
  the sensors page kept a per-source list of metric ids, the morning greeting looked up `temperature`
  by name, and the computer's power supply is now read from `PoweredBy` instead of assumed

### Three small fixes, one of them a stale deploy

- **Clear chat did nothing.** `ChatPanel.ClearAsync` is called from the Dashboard through a component
  reference (`_chat.ClearAsync()`), not from an event inside `ChatPanel` itself, so clearing `_turns`
  never triggered a re-render — Blazor had no reason to know the child changed. It now calls
  `StateHasChanged()` explicitly and surfaces the API result as the toast message
- **The default tab was blank on first load.** `Sensors.razor`/`Devices.razor` corrected `_tab` to the
  first available source in `OnInitialized`, but `CortanaState.Snapshot` is filled in asynchronously
  by a background SSE subscription — on the very first render it is still `null`, so `Sources` was
  empty and the "correction" set `_tab` to `""`, and nothing was ever selected. The fix only corrects
  the tab once `Sources` is actually non-empty, checked both at init and every time the snapshot
  updates (`EnsureTab`, called from the subscription callback)
- **The icon-field width fix from the modal pass never reached the Pi.** The session that wrote it hit
  a tool error partway through and the deploy never ran, so the phone was still running `flex: 1 1
  5rem` (grow enabled, no max-width) — a lone icon box wrapped to its own line stretches to fill it.
  Reproduced against the live CSS at 375px (Name+On share a row, Off spans the full row beneath), then
  confirmed the already-written fix (`flex: 0 1 5rem; max-width: 7rem`, grow disabled) resolves it at
  the same width, before deploying. Worth remembering: a fix living only in the working tree is not a
  fix until `cortana deploy` actually runs

### Mood no longer gets stuck Worried over a machine that is *supposed* to go quiet

The PC disconnecting is routine — it is off at night, it sleeps, it reboots. `AutomationWorld.
SourcesOnline` required **every** source online, computer included, so `Mood.Worried` triggered the
instant the PC went to sleep and held for as long as it stayed off. Two changes:

- **The computer no longer counts as a health signal for mood.** A new `CriticalSourcesOnline` (all
  sources *except* `SourceKind.Computer`) feeds `MoodInput` instead of the old `SourcesOnline`, which
  still means "every source" for the general diagnostic text (`GetComputerStatus`, `/automation`) —
  that is genuinely useful information and unrelated to how worried she should sound
- **Worry is now a damped, bounded reaction, not a held state.** `MoodRules.Decide` split into
  `IsWorrying` (the raw condition) and `NonWorried` (what she'd feel instead). `SnapshotService.
  Evaluate` rolls a die only on the *rising edge* of a worrying condition — a 60% chance to actually
  express it — and if it does, for a random 30 minutes to 2 hours, after which it lapses even if the
  condition persists. A station or a service that stays down all day gets one honest reaction, not a
  day-long alarm. `Explain` now takes the mood being shown rather than re-deciding from the raw input,
  so the reason text can never describe a mood other than the one on screen
