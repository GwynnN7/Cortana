# Cortana — ideas

Direction notes, not a plan, and nothing here is committed to. The question behind all of them:
how does she become *present* without becoming *noise*.

**Built so far**

| § | | |
| :--- | :--- | :--- |
| 8 | `why` on every notification | done — reason on `NotificationEntry`, expandable in the log |
| 5 | the mood word | done — `MoodRules`, with the reason carried to every client |
| 11 | activity, games, music | done — focus category *and* MPRIS playback as separate axes, gaming ⇒ do-not-disturb, both recorded to history |
| 10.2 | idle and lock | done — lock via the shell's IPC, idle via an `ext-idle-notify` daemon calling `cortana idle` |

**Still open:** §2 baselines, §4 memory and its dashboard page, and the room × desktop correlation at
the end of §11 — which is the one the other two make possible.

**Contents**

| § | | |
| :--- | :--- | :--- |
| [0](#0-the-diagnosis) | The diagnosis | what is actually missing |
| [1](#1-volition--the-attention-budget) | **Volition** | the attention budget — the frame everything plugs into |
| [2](#2-baselines--giving-her-a-sense-of-normal) | **Baselines** | a sense of *normal*, so something can be surprising |
| [3](#3-rhythm--a-model-of-your-day) | Rhythm | a learned model of your day |
| [4](#4-memory-of-you) | **Memory** | of *you*, not just of conversations |
| [5](#5-interiority--something-of-her-own-to-express) | Interiority | a mood of her own to express |
| [6](#6-voice) | Voice | the largest jump, and the largest project |
| [7](#7-physical-presence) | Physical presence | the LED as her eye |
| [8](#8-smaller-additions-that-pull-their-weight) | Smaller additions | that still pull their weight |
| [9](#9-curiosity--questions-that-arent-random) | Curiosity | questions that aren't random |
| [10](#10-the-linux-desktop-as-her-body) | The desktop | Hyprland + Caelestia as her body |
| [11](#11-activity-games-and-music--one-model-not-three) | **Activity** | games and music as one model — and the room × desktop loop |
| [12](#12-the-contract-worth-pinning-in-devmd) | The contract | rules any unsolicited message must satisfy |
| [13](#13-where-to-start) | Where to start | a build order |

---

## 0. The diagnosis

What exists today is an excellent **reflex arc**: sensors → rules → devices → notifications, with a
conversational front bolted cleanly on the side. Every path is reactive. She answers, she reports,
she obeys. Nothing originates with her.

Four things are missing, and each has an obvious home in the current layering:

| Missing | What it means | Where it would live |
| :--- | :--- | :--- |
| **Initiative** | She never speaks first | `Domain/Volition` |
| **Noticing** | No sense of *normal*, so nothing is ever surprising | `Domain/History` (baselines) |
| **Continuity** | She remembers conversations, not *you* | `Domain/Memory` |
| **Interiority** | No state of her own to express | `Domain/Automation` + push/UI |

More capabilities will not produce aliveness. These four will, and three of them are deterministic —
no extra model trust required.

---

## 1. Volition — the attention budget

The one mechanism everything else plugs into, and the answer to "without being annoying".

Introduce **`Impulse`**: something Cortana *could* say. Anything may raise one — the automation
engine, a sensor anomaly, a schedule, the desktop agent, a memory. Each carries:

```
Impulse { Source, Salience 0..1, Key (dedupe), ExpiresAt, Payload, Actionable? }
```

A pure `VolitionRules.Select(impulses, state, clock)` decides which — **usually none** — becomes
speech. Same shape as `AutomationRules` and `ScheduleTiming`: pure, fake-clock testable, no
infrastructure.

The budget is what makes it safe:

- **Hard quota.** N unsolicited messages per waking day (3 is probably right), refilled at
  `MorningHour`. Never during `SleepMode`. Minimum spacing between two.
- **A rising bar.** Every message spoken raises the salience threshold for the next few hours, then
  it decays back. Self-limiting by construction — a chatty hour makes her quieter, automatically.
- **Novelty required.** A `Key` cannot fire twice inside its cooldown, and cannot fire at all unless
  the underlying fact moved materially since last time.
- **Silence is the default output.** An impulse that expires unspoken is simply dropped, and that is
  the normal case, not a failure.
- **Learned weights.** Record whether an unsolicited message was *acted on* (lamp switched after she
  mentioned it, window opened after a CO₂ note, reply within N minutes). Nudge that `Key`'s weight
  up or down in a small JSON file. She learns what you don't care about and stops raising it. This
  is the single highest-value anti-annoyance feature in the whole document and it costs one file.
- **An off switch that persists.** `Quiet(duration)` as a `Management` capability plus a setting, so
  "leave me alone until tonight" is a thing she can actually honour.

Log **every** impulse, spoken or not, with its salience and outcome. Without that there is no way to
tune this and it becomes vibes.

### Choose the channel by urgency

Not everything that is worth expressing is worth *interrupting* for. Rank the surfaces:

```
ambient (LED colour, status line)  →  passive (dashboard badge)  →  interrupting (push, Telegram)
```

Most impulses should die at the ambient level. Reserve push for things with a deadline.

---

## 2. Baselines — giving her a sense of "normal"

`HistoryAnalysis` already does deterministic reductions over the CSVs. Add **`HistoryBaseline`**:
per sensor, per hour-of-day, per weekday, a rolling median + MAD over the last few weeks.

What that unlocks:

- **Anomaly instead of threshold.** `Co2Threshold` becomes "CO₂ is higher than it has been at this
  hour in three weeks" — which is a *thing to say*, where a fixed number is not.
- A new AI capability `CompareToUsual(metric, window)`, deterministic, so it never invents.
- Better automation input than a fixed `LightThreshold`.
- **Applies to `MachineSample` too.** The desktop already pushes CPU/RAM/GPU/uptime and nobody is
  looking at it. "GPU has been pinned for two hours" or "something's been chewing CPU since you went
  to bed" is useful *and* alive, and it is her noticing something about your machine unprompted.

Being noticed is most of what makes a presence feel present. This is the highest-leverage item here.

---

## 3. Rhythm — a model of your day

Derived from history, not configured:

- Typical wake time, typical PC-on time, typical session length, typical night.
- `MorningHour` / `NightHour` become *learned*, with the settings as bounds rather than the truth.
- "You're up two hours earlier than usual" is a real observation with zero invention behind it.
- **Absence.** No motion + PC off for long enough = away. Motion returning fires exactly one
  welcome-back — and it earns its place by carrying what happened while you were out: air quality
  peaked at 21:00, a schedule fired, the desktop rebooted itself.
- **Suggestions, never actions.** "You've switched the lamp off manually around 23:40 five nights
  running — want that as a schedule?" Proposing an automation is alive *and* useful, and it puts the
  decision back with you, which is why it doesn't grate.

---

## 4. Memory of *you*

Conversations persist; nothing about the person does. Add a small, capped, human-readable store:

```
Memory { Id, Text, Kind (fact | preference | event), Source, CreatedAt, LastUsedAt, Weight }
```

- Two capabilities: `Remember(text, kind)` and `Recall(query)`. Top-N by weight and recency get
  injected into the system prompt — which is what `memory depth` in `Ai.json` is already for.
- **She says when she stores something**, or asks first. Silent accumulation is the difference
  between a companion and surveillance.
- Unused memories decay and fall out.
- **A "what she knows about me" page in the dashboard**, with delete. This is a feature in its own
  right and it is the whole answer to the creepiness objection: the memory is inspectable.

Alongside it, an **episodic layer**: one generated digest row per day — `up 07:40, PC 09:12–01:30,
worst air 21:00, lamp on 6h`. Cheap to compute from existing CSVs, and it gives her a genuine
"yesterday you…" faculty that no amount of prompt engineering can fake.

---

## 5. Interiority — something of her own to express

The push status is currently pure fact: `Online · 💡🖥️🔌 · 🔆|💠|💤 💨 21.4°`. Give her a mood,
**derived from real state**, not simulated emotion:

- *watching* — idle, everything nominal
- *busy* — schedules firing, machine loaded
- *concerned* — air degrading, a sensor gone stale, disk filling
- *resting* — sleep mode
- *alone* — no motion for hours

One word in front of the facts. Expose it to the model so her tone shifts for a reason instead of
randomly. This is honest — it's a summary of her actual situation — and it makes the status line
read as a state of mind rather than a readout.

Two more, both already true of the architecture and both more alive than false confidence:

- **She believes, she does not know.** GPIO can't be read back. "I believe the lamp is on, I can't
  verify it" is more characterful *and* more accurate than asserting it.
- **Her own health is hers.** A stale ESP32, a dropped agent, a full disk. "One of my senses has
  been out for an hour" is exactly the kind of line the character should have, and it is a real
  operational alert.

---

## 6. Voice

The largest single jump, and conspicuously absent. Halo's Cortana is a voice.

- **Output first.** Piper TTS on the Pi is fast enough for short lines. Even speaking *only* the
  volition messages — and only when you're at the desk and awake — changes the character of the
  whole system.
- **Input second, and elsewhere.** STT belongs on the desktop, not the Pi: whisper.cpp on the GPU
  machine, reached over the agent socket, which is already a bidirectional JSON-line protocol built
  for exactly this. Wake word via openWakeWord on the desktop, or on the ESP32 if you want it to
  work with the PC off.
- Architecturally this is a new `CommandSurface.Voice` and one more client project. The Kernel does
  not need to learn anything new.

---

## 7. Physical presence

Non-verbal channels are the anti-annoyance channels: they communicate continuously without ever
demanding a response.

- **An RGB LED or a short strip on the ESP32 as her eye.** Colour is the mood from §5 — blue
  watching, amber concerned, dim while resting, a brief pulse when a command lands or she's
  thinking. Most impulses should end their life here and nowhere else.
- A soft chime tied to an event class rather than a text notification.
- In the dashboard, a subtle breathing indicator on snapshot updates — she is visibly *running*.

---

## 8. Smaller additions that pull their weight

- **Morning brief.** One message on first contact of the day: last night's air, how long sleep mode
  held, today's schedules, weather if a source gets added. Bounded, once, obviously useful.
- **Generalise `why`.** `ExplainAutomation` answers it for the lamp. Attach the deciding fact to
  *every* notification and let the dashboard expand any state change into its reason. This is the
  deepest "she is actually thinking" signal available and it is already half built.
- **The desktop agent as a sense, not just hands** — see §10, which is where this grew into its
  own answer.
- **Real away-mode** via phone presence (ARP ping, or a Telegram location share).
- **A guest persona.** `trusted` already exists for Discord; a visitor-facing manner is a cheap and
  fun surface.

---

## 9. Curiosity — questions that aren't random

A random question is a gimmick with a shelf life of about a week: you learn the pattern and it
becomes a slot machine. What makes a question feel like it came from a person is that it is
**grounded in something she observed** and that **she has a use for the answer**.

So invert it. Questions are not generated for their own sake — they are generated by **gaps in
memory that she could actually use**:

> She knows you played *Elden Ring* four nights this week. She does not know whether you like it.
> That gap is worth one question, because the answer changes recommendations, tone, and whether
> she brings it up again.

If there is no slot the answer would fill, she doesn't ask. That single rule kills every "what's
your favourite colour" failure mode, and it self-limits: gaps get filled, curiosity subsides, and
new observations open new ones.

### Rules

- **Cite the observation.** "Four nights this week — is it good, or are you just stuck?" is alive.
  "Do you like games?" is a chatbot.
- **Ask at seams, never in flow.** PC just on, just back from an absence, a long session just
  ended, right before sleep, first contact of the day. The rhythm model (§3) hands her these
  boundaries for free. Never mid-session, never during fullscreen.
- **Lowest priority impulse there is.** Curiosity spends the same §1 budget as a CO₂ alert and
  always loses to it. One every few days at most.
- **Answer-optional by construction.** A remark with a hook, not an interrogation. Never follow up
  on an unanswered one — silence is an answer, and it is a negative weight on that curiosity class.
- **Guess, don't interrogate.** "I'm guessing you shelved it. Right?" is cheaper to answer, more
  characterful, and proves she was paying attention. A wrong guess is *better* material than a
  right one.
- **Opinions often beat questions.** Her prompt already says she has opinions and will disagree.
  "Third night past 3am, and the window's been shut since Tuesday. I'm not your mother, but." —
  stake, information, character, and no reply demanded.
- **Remember being wrong.** "You told me you hated roguelikes and you have thirty hours in one" is
  the single most alive line a system can produce, and it falls straight out of §4.

### Where the material comes from

Games and music are the two richest seams, and they are the same integration — §11 has the model,
the detection chain and the plumbing. What matters *here* is what curiosity does with them:

- **A play journal makes gaps visible.** Sixty-hour game and you are twelve hours in. Three titles
  bounced off in a fortnight. One untouched for three weeks. Each is a specific thing she noticed
  and does not know the reason for — which is exactly the shape of a question worth asking.
- **Music is warmer and cheaper.** That album four times today; the thing you put on at 2am; the
  music stopping an hour before you actually went to bed. Mostly these should surface as remarks,
  not questions — there is rarely a memory slot that needs filling, and the observation alone is
  the point.
- **Same for anything else the desktop can see**: what you are reading, what you are building.

The test never changes: *would her behaviour differ once she knows the answer?* If not, it is small
talk, and small talk is what makes assistants insufferable.

---

## 10. The Linux desktop as her body

Hyprland on CachyOS, with a personal fork of Caelestia, is about the best case available. Hyprland
has an **event socket** (`$XDG_RUNTIME_DIR/hypr/$HIS/.socket2.sock`) that *streams* `activewindow`,
`workspace`, `fullscreen`, `openwindow` and `closewindow` — no polling, no guessing, a live feed of
what you are doing. And owning the shell means she is not a guest in it.

The desktop agent already holds an open socket to the Kernel and speaks JSON lines, so all of this
is one more subscription on the other end of a process that is already running.

Ordered by value per hour of work:

### 10.1 She becomes part of the shell

A personal fork of **Caelestia** changes this from "bolt a module onto a bar" to "she is a component
of the shell", which is a different and much better thing. Quickshell/QML gives her a bar module, an
orb, a sidebar pane, her own notification style and the lock screen — all in one idiom, all yours to
edit.

**The data path should not be new.** The agent already holds the socket to the Kernel, so let it be
the machine's Cortana endpoint: the agent writes `$XDG_RUNTIME_DIR/cortana/state.json` atomically on
every change, and QML watches that file. No second SSE consumer, no API key inside the shell, and it
degrades honestly — if the Pi is unreachable the file just goes stale and the orb can say so. The
reverse direction is already solved too: QML shells out to `cortana`, which is on `PATH`.

Start with the **bar module**: her mood word from §5, the room temperature, the lamp state, and a
colour that is her eye. Click toggles the lamp, right-click opens the pane. Permanently present,
never interrupting — the ambient channel from §1, in the place you already look fifty times a day.

### 10.2 Activity as a sense, category only

Map window class → coarse category (`gaming | coding | browsing | media | idle`) **on the desktop
side** and send only the category over the socket. Never window titles. That boundary is the whole
answer to the privacy objection, and it is enforced by where the mapping lives rather than by
policy.

Add idle seconds (hypridle / a `swayidle`-style watcher) and "PC on" finally splits into **at the
desk** vs **machine on, nobody home** — a far better sleep-entry signal than the current one, and
the thing that tells volition when interrupting is acceptable.

Lock/unlock events (hyprlock) are a second presence signal, and combining them with the PIR gives a
much better away model than either alone.

### 10.3 One key away

`SUPER+C` → a floating terminal running `cortana chat`, or a fuzzel/rofi prompt that pipes one line
to `cortana ask` and returns the answer as a notification. Hyprland window rules make the floating
centred pane two lines of config. A `wtype` variant lets her **type the answer into the focused
window**, which turns her into an editor tool rather than a separate app.

### 10.4 Notifications that are hers, and two-way

`notify-send` is already the fallback path — make it a real surface:

- A consistent app name, her icon, and **urgency mapped from `NotificationLevel`**.
- A **replace-id**, so her status notification updates in place instead of stacking — precisely what
  the web push status notification already does. Mirror that behaviour on the desktop.
- **`notify-send --action`**: "Turn it off" / "Not now" / "Remind me later". The chosen action comes
  back on stdout, and the agent routes it to the Kernel as a command. That is the difference between
  a notification and an interaction, and it makes "Not now" a real, one-click way to feed the
  learned weights in §1.

### 10.5 An actual presence on screen

With the shell in your hands, four things worth building in roughly this order:

- **The orb.** A layer-shell widget that breathes, takes her mood colour, and pulses when a
  command lands or she is thinking. Click-through while idle. Caelestia's animation idiom already
  suits this; it is the closest a desktop gets to Cortana on her pedestal.
- **A Cortana pane in the sidebar/dashboard.** Room state, sensor readouts, her last few utterances,
  and a text field wired to `cortana ask`. This retires the floating-terminal hack in §10.3 in
  favour of something that belongs to the shell.
- **Her notifications, rendered by the shell.** Caelestia already draws its own popups, so she gets
  her avatar, an urgency colour from `NotificationLevel`, in-place replacement for the status line,
  and **native inline actions** — with the buttons calling `cortana` directly. That is §10.4 done
  properly instead of fighting `notify-send`'s limits.
- **The lock screen.** Room state, temperature, whether the lamp is on, her mood. Very Halo, and
  unlock is the strongest "you're back" signal the machine can give her.

Then the striking one: **let her drive the shell's accent.** Caelestia already themes dynamically;
invert it so the palette warms toward amber when she is concerned and cools with the night boundary,
in step with the physical lamp. The room and the desktop visibly become one system — which is the
actual thesis of this project, and it is only available to you because you own the shell.

Keep the amplitude small and put it behind a toggle. A desktop that changes colour on you is magic
for a week and an irritant in month two.

### 10.6 More hands

The agent has six commands. Linux gives a dozen more for almost nothing:

- Volume and media (`wpctl`, `playerctl`) — and with MPRIS she gets §9's music context in the same
  stroke.
- **Clipboard both ways** (`wl-clipboard`). "What's on my clipboard?" is fine; *"here's the command,
  it's on your clipboard"* is the one you'd use daily.
- Screenshot, lock, workspace switch, and "open my work layout" — launch a set of apps into
  workspaces, which Hyprland scripts trivially.
- **Phone ⇄ desk handoff.** Telegram already exists on one end and the clipboard on the other. Small
  feature, disproportionate daily utility.

### 10.7 She can see what you're building

Long build finished, test loop failing repeatedly, uncommitted changes sitting for three days. All
genuinely useful, all one careless step from insufferable — so this one only ships **behind the §1
budget with learned weights**, never as a direct notification.

---

## 11. Activity, games and music — one model, not three

Activity category, the running game and the current track are the same fact wearing three hats:
**what the machine is being used for right now.** Model it once in `CortanaLib/Contracts` and it
flows for free to the shell, the dashboard, the AI, automation, volition and history. Model it three
times and you will be reconciling three half-truths by Christmas.

```
DesktopActivity(
    ActivityCategory Category,   // Idle | Browsing | Coding | Gaming | Media | Away | Locked
    string?          Subject,    // game title or track, only at the detail level the user chose
    string?          Detail,     // artist/album, or the Steam appid
    DateTimeOffset   Since,
    int              IdleSeconds,
    bool             Locked)
```

Keep it **out of `MetricsView`** — that record is about hardware and should stay that way. A sibling
`DesktopActivity?` on the snapshot, next to `ComputerMetrics`, is the right shape.

One privacy dial, three positions, enforced **on the desktop side** where the mapping lives:
`category only` → `+ game titles` → `+ now playing`. Never window titles, at any setting.

### The hazard worth writing down now

`StateBroadcaster` subscribes to *every* event and turns it into a snapshot rebroadcast. Hyprland's
socket2 emits on every focus change; `playerctl --follow` emits on every seek. Wiring either
straight into the bus will melt the Blazor clients.

So: the **agent** debounces, not the Kernel. It emits only *transitions* — category changed, game
started or stopped, track changed — and never streams `IdleSeconds`; the clients derive elapsed time
from `Since` themselves. Coalesce on the Kernel side too, the way `PushService` already does at
300 ms. This is the one place where the new sense could genuinely destabilise a working system.

### Games

The detection chain on Linux needs no Steam API and no login:

```
running process  →  exe path under a Steam library's steamapps/common/<Dir>
                 →  appmanifest_<appid>.acf in that library   (gives name + appid)
                 →  appid  →  IGDB / SteamGridDB  →  cover art, length, genre, similar games
```

Lutris and Heroic keep their own configs for the non-Steam case, with a class→name map as the last
fallback. `CORTANA_IGDB_*` is already wired for Discord `/games`, so the knowledge half exists.

Write **session records** into the existing history mechanism — `game, start, end, duration` — and
every reduction in `HistoryAnalysis` works on them the day they land: longest session, totals,
comparisons, "you always play this on Sundays".

The immediately *useful* payoff, independent of any personality feature: **gaming plus fullscreen is
automatic do-not-disturb.** Volition goes silent, notifications are held rather than shown, and the
lamp stops being clever. That alone justifies the feature.

The *alive* payoff is §9's material: sixty-hour game and you are twelve hours in, three games
bounced off in a fortnight, a title untouched for three weeks — each one a grounded question or a
grounded opinion, not small talk.

### Music

`playerctl --follow --format '{{status}}|{{artist}}|{{title}}|{{album}}|{{mpris:artUrl}}'` is one
long-running process the agent reads line by line. That is the entire integration, and it covers
Spotify, browsers and everything else that speaks MPRIS.

- **History.** Track plays into the same store: top artists this week, what you put on at 2am, "that
  album four times today" — which is a warm observation rather than a metric.
- **Control.** `playerctl` play/pause/next as agent commands, so "pause the music" works from chat,
  Telegram or eventually voice. Better: **sleep-mode entry pauses the music and dims the lamp in the
  same gesture.** The house acting as one thing is worth more than either half.
- Titles are more personal than game names — that is what the third position on the privacy dial is
  for.

### The dashboard section, and the actual killer feature

A desktop panel on `Core.razor` is the natural home:

- **A day ribbon** — today as a horizontal band of coloured segments by category. The single most
  satisfying way to see what you actually did, and it comes straight out of the history CSVs.
- **An hour × weekday heatmap** over a few weeks. This *is* the §3 rhythm model, made visible.
- Now-playing with album art; current game with cover art, session length and tracked total.
- Top games and top artists over a window. Cheap, and quietly delightful.

And then the thing **no other assistant on earth can do**, because no other assistant has both
halves:

> **Correlate the room against the machine.**
> CO₂ against gaming sessions — door shut, two hours in, the air goes off a cliff.
> Room temperature against GPU load. Late sessions against the next morning's wake time.

That closes a loop worth closing: *"ninety minutes in, CO₂ is up four hundred — open the window"* is
specific, non-obvious, genuinely useful, and structurally impossible for anything that only watches
the desktop or only watches the room. It is the strongest single argument for this whole project
existing, and every piece needed to build it is already in the repository.

---

## 12. The contract (worth pinning in DEV.md)

Rules any unsolicited message must satisfy. Written down so they survive future features:

1. **Silence is a valid and common output.** Most impulses die unspoken.
2. **Carry information the user does not already have.** A restatement is noise.
3. **Prefer the ambient channel.** Interrupt only when there is a deadline.
4. **Never twice for one cause.** Dedupe by key, not by text.
5. **Never during sleep.** No exceptions worth the trust it costs.
6. **Trivially silenceable, and the silence persists.**
7. **Measured.** Every impulse logged with salience and outcome, or none of this can be tuned.

The failure mode to design against is not "she said something wrong". It is "she said something
true, useful, and unwanted, three times". A quota plus learned weights is what prevents that; no
amount of prompt wording will.

---

## 13. Where to start

Roughly in dependency order, not importance order.

**Groundwork — build these first, everything else assumes them.**

1. **Volition: the budget and the learned weights** (§1). The frame every proactive idea plugs into.
   Nothing else in this document is safe to build before it exists, because without a budget each
   new idea is another thing that can interrupt you.
2. **Activity as a category, with idle and lock** (§10.2, §11). What tells volition whether you are
   even there, and the sense that games, music and the do-not-disturb rule all ride on.

**Then the two that change what she is.**

3. **Baselines** (§2) — turns her from a readout into something that notices.
4. **Memory of you, with the dashboard page** (§4) — turns her from a session into a relationship.

**Pure payoff, buildable early if you want a win.**

- **The Caelestia bar module** (§10.1). Continuously present for almost no work, and it makes every
  later ambient idea have somewhere to land.
- **The room × desktop correlation** (§11). The CO₂-versus-session loop needs nothing beyond
  activity category, and it is the one capability nothing else can copy.
- **Gaming ⇒ do-not-disturb** (§11). Falls out of step 2 for free and is immediately useful.

**Bigger than it looks.**

- **Voice** (§6) is a larger project than the first four combined. Worth it, but not first.
- **Shell-wide accent driven by her state** (§10.5) is a week of magic and a month of irritation
  unless the amplitude stays tiny and it sits behind a toggle.

Whatever gets built: **log every impulse, spoken or not, with its salience and its outcome** from
day one (§12.7). Without that log there is no way to tell whether any of this is working, and the
whole document becomes a matter of taste.
