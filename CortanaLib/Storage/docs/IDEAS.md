# Cortana — ideas

Direction notes, not a plan, and nothing here is committed to. The question behind all of them:
how does she become *present* without becoming *noise*.

**Contents**

| § | | |
| :--- | :--- | :--- |
| [0](#0-where-things-stand) | Where things stand | what is already built, and what it unlocked |
| [1](#1-volition--the-attention-budget) | **Volition** | the frame exists; the judgement does not |
| [2](#2-rhythm--a-model-of-your-day) | **Rhythm** | a learned model of your day — the biggest missing input |
| [3](#3-the-daily-digest) | The daily digest | one row per day, and a real "yesterday you…" |
| [4](#4-more-sources) | More sources | Discord voice, window-open, weather, games |
| [5](#5-the-desktop-as-her-body) | The desktop | shell, hotkey, two-way notifications |
| [6](#6-physical-presence) | Physical presence | the LED as her eye, and the protocol change it needs |
| [7](#7-voice) | Voice | the largest jump, and the largest project |
| [8](#8-curiosity) | Curiosity | questions that are not random |
| [9](#9-smaller-things) | Smaller things | that still pull their weight |
| [10](#10-the-contract) | **The contract** | rules any unsolicited message must satisfy |
| [11](#11-a-build-order) | A build order | what unblocks what |

---

## 0. Where things stand

Built, in rough order of how much they changed the system:

| | |
| :--- | :--- |
| **Reasons everywhere** | every notification carries a `why`, expandable in the log |
| **Mood** | one word for the whole house, with the sentence behind it, on the status pill and in her prompt |
| **Activity** | the desktop's focus category *and* MPRIS playback as two independent axes, privacy-bounded on the agent |
| **Do not disturb** | a fullscreen game or film holds back desktop notifications and automation |
| **Presence** | idle and lock from the desktop, composed from sensors that either *report* somebody or only *sustain* an existing presence, on one motion timeout instead of two crude ones |
| **Baselines** | median + MAD per hour-of-day, so "unusual" is computable rather than a fixed threshold |
| **Memory** | what she knows about you, weighted, decaying, inspectable, trusted-only |
| **Correlation** | room against desk, including the drift across the current session |
| **Volition, seeded** | a persisted quiet period, a morning greeting, and a daily wrap-up said with probability *p* |
| **The fabric** | hardware announces channels as tags; devices and sensors are registered on top, and nothing names a lamp |
| **Notes** | what you asked her to write down, separate from what she knows about you |
| **Features** | twelve services with a real switch, honoured in the Kernel and reflected in the web |

What that unlocked: **every fact she states can now be computed rather than guessed**, history carries
the room and the desk in the same rows, and what exists is whatever you registered rather than what
was compiled in.

What it did *not* unlock: she speaks unprompted twice a day at fixed hours and otherwise only when
spoken to. Everything below is really about closing that gap without becoming a nuisance.

---

## 1. Volition — the attention budget

The frame exists (`VolitionRules`, a persisted `Quiet`, one greeting, everything logged). What is
missing is everything that makes it *judgement* rather than a cron job.

**`Impulse`** — something she *could* say. Anything may raise one:

```
Impulse { Source, Salience 0..1, Key (dedupe), ExpiresAt, Payload, Actionable? }
```

`VolitionRules.Select(impulses, state, now)` returns **at most one**, usually none. Pure, so it stays
testable without infrastructure — same shape as `AutomationRules` and `ScheduleTiming`.

The budget is the safety, not the prompt wording:

- **Hard quota** — a few unsolicited messages per waking day, refilled at `MorningHour`, never during
  sleep mode, with a minimum spacing between two.
- **A rising bar** — each message raises the salience threshold for a few hours, then it decays. A
  chatty hour makes her quieter by construction.
- **Novelty required** — a `Key` cannot fire inside its cooldown, and cannot fire at all unless the
  underlying fact *moved materially* since last time.
- **Silence is the default output.** An impulse expiring unspoken is the normal case.
- **Learned weights, later.** Record whether a message was *acted on* — lamp switched after she
  mentioned it, window opened after a CO₂ note, a reply within N minutes — and nudge that `Key`'s
  weight. Do this only once the log shows what actually fires; three observations a day converges too
  slowly to be worth guessing at up front.

### Choose the channel by urgency

```
ambient (status line, LED)  →  passive (dashboard badge)  →  interrupting (push, Telegram)
```

Most impulses should die at the ambient level. The status line already carries mood, devices, motion,
air and schedules — that is a working ambient channel that interrupts nobody. Reserve push for things
with a deadline.

**The honest blocker:** there is currently about one thing worth saying unprompted. Arbitration
between one source is not arbitration. §2 is what produces the rest.

---

## 2. Rhythm — a model of your day

The biggest missing *input*, and mostly a reduction over data already being collected.

Per weekday, per hour: when the computer comes on, when motion starts, when sleep mode is entered,
when the lamp goes on. Store medians, not means — one late night should not move the model.

What it produces, all of which are impulse sources with real salience:

- "You are up two hours earlier than usual for a Tuesday."
- "The computer has been on since 09:00, which is your longest stretch this month."
- "You normally go to bed around now."
- Anticipation rather than reaction: warm the room *before* the usual wake time.

It is also the honest prerequisite for a **sleep model** — bedtime and wake time are already implicit
in sleep mode and motion, and the room data over those hours is already recorded.

---

## 3. The daily digest — **mostly built**

`HistoryService.Digest` reduces a window to plain lines — every sensor's range, every device's on-time,
minutes per activity category, music — and the evening wrap-up turns that into a sentence, now stored
as a `Day` memory that keeps a week, and sometimes said.

**Keeping the digests is done**, which was the part §2 wanted: `Days.json` holds a `DaySummary` per
day, `POST /history/days` backfills from the CSVs already on disk, `Rhythm(metric)` compares today
against the median of the same weekday, and the model reaches it as `CompareToUsualDay`. There is a
series to model a rhythm over now.

What is left is the **end-of-session digest** — when a long gaming or coding stretch ends, one line
about what it cost: how long, what the air did, how far the room warmed. `Digest` already takes an
arbitrary window, so this is a trigger away.

---

## 4. More sources

### Discord voice time

Worth doing, and **most of it already exists**. `CortanaDiscord/Program.cs` hooks
`UserVoiceStateUpdated` and keeps `DiscordContext.VoiceSince[userId]` — the join timestamp is already
in memory, purely for greetings. Nothing reports it to the Kernel and nothing records it.

It fills a real gap: the desktop agent sees *what application has focus*, which says nothing about
whether you have been talking to people for three hours. A `voice` column beside `activity` and
`music` would immediately work with the baselines and correlation already built — "you have been in
voice for four hours and the CO₂ is up 500" is exactly the shape of insight §1 wants, with no new
hardware and no protocol change.

The missing pieces are small: a Kernel endpoint the bot can post voice enter/leave to, a registry
beside `ActivityRegistry`, and one history column.

Caveats worth naming before building it: it is a second activity axis rather than a category (you can
be gaming *and* in voice), so it belongs as its own field, not as an `ActivityCategory` value; and it
should record duration only — never who else was in the channel, and never anything said.

### Window-open detection

CO₂ falling faster than any decay plus temperature moving toward outdoor = the window is open.
Deterministic, no new hardware. Makes "open the window" advice verifiable — she can *tell whether you
did it*, and learn how long the room takes to recover, which feeds both baselines and volition.

### Weather

The one external API worth adding. Indoor against outdoor makes ventilation advice actually correct:
"open the window" is bad advice at 35 °C or during a downpour. Also gives context for heating and for
the temperature baselines.

### Games, properly

The detection chain needs no Steam API and no login:

```
running process → exe under steamapps/common/<Dir> → appmanifest_<appid>.acf → appid → IGDB
```

`CORTANA_IGDB_*` is already wired for Discord `/games`, so the knowledge half exists. Session records
into history make every existing reduction work on them the day they land: longest session, totals,
"you always play this on Sundays". Lutris and Heroic keep their own configs for the non-Steam case.

### Music, beyond reading it

`playerctl` play/pause/next as agent commands, so "pause the music" works from chat or Telegram.
Better: **sleep mode pauses the music and dims the lamp in one gesture.** The house acting as one
thing is worth more than either half.

---

## 5. The desktop as her body

- **In the shell.** A Caelestia widget showing mood and the room at a glance; the status line already
  computes everything it needs.
- **One key away.** A global hotkey that opens a prompt, sends to `/ai`, and shows the reply. The CLI
  already does the hard part.
- **Notifications that are hers, and two-way.** Desktop notifications with actions — "open the
  window" with a *Done* button, which is exactly the acted-on signal §1 needs for learned weights.
- **More hands.** Volume, brightness, media keys, window management as capabilities.

---

## 6. Physical presence

The LED on the station as her eye: mood as colour, a slow pulse when she has something to say, dark
when quiet. The cheapest possible ambient channel and the most "alive" thing per line of code.

**It needs a protocol change first.** The station link is currently send-only — the firmware opens the
socket and writes; it never reads. Making the LED expressive means the Kernel talking *back* to the
ESP32, which means a read path, a frame format, and a reconnect story. Worth doing deliberately rather
than bolting on.

---

## 7. Voice

The largest jump and the largest project. Wake word, speech-to-text, and a voice that sounds like her.
Locally on a Pi 4 this is unrealistic; as a hybrid it is a real project rather than a feature. Worth
keeping in view because it changes what the assistant *is*, not just what it does — but everything
else here is cheaper and lands sooner.

---

## 8. Curiosity

Questions that are not random, drawn from material she actually has: a game untouched for three
weeks, an album played four times in a day, a room that has been stuffy every evening this week. The
difference between small talk and being noticed is entirely in whether the question is grounded.

This is downstream of §2 and §3 — without a model of normal, "curiosity" is just a random prompt.

---

## 9. Smaller things

- **An hour × weekday heatmap** of activity on the dashboard. This *is* the §2 rhythm model, made
  visible, and the data is already there.
- **Top games and top artists** over a window. Cheap, and quietly delightful.
- **A separate push tag for her own messages.** Right now everything reuses the status overlay, so a
  greeting replaces the status body for a few seconds and is easy to miss. Anything she says on her
  own initiative should probably persist until dismissed.
- **Pi self-observation.** Disk and temperature trends against their own baselines — she already has
  the machinery, nothing points it at her own host.
- ~~**`ActivityDetail` has no UI.**~~ Done: the level rides on every activity update and is set from
  the Logs page or by the AI, and the agent writes it back to `activity.conf`.
- **Editing a registration cannot rename its id.** Deliberate — history columns and binds key off it —
  but it means a badly named sensor is delete-and-recreate, which loses its history column.
- **Telegram and Discord read; the dashboard configures.** That split is now deliberate rather than
  accidental. If a bot ever needs to configure something, ask whether the AI could do it instead.

---

## 10. The contract

Rules any unsolicited message must satisfy. Written down so they survive future features:

1. **Silence is a valid and common output.** Most impulses die unspoken.
2. **Carry information the user does not already have.** A restatement is noise.
3. **Prefer the ambient channel.** Interrupt only when there is a deadline.
4. **Never twice for one cause.** Dedupe by key, not by text.
5. **Never during sleep.** No exceptions worth the trust it costs.
6. **Trivially silenceable, and the silence persists.** (`Quiet` exists and is honoured.)
7. **Measured.** Every impulse logged with salience and outcome, or none of this can be tuned.

The failure mode to design against is not "she said something wrong". It is "she said something true,
useful, and unwanted, three times". A quota plus learned weights is what prevents that; no amount of
prompt wording will.

---

## 11. A build order

1. ~~**Persist the daily digest (§3)**~~ — done. `Days.json` holds one row per day and backfills from
   the CSVs already on disk.
2. **Rhythm (§2)** — the weekday comparison exists (`CompareToUsualDay`); what is left is turning it
   into *impulse sources* with salience, and anticipation rather than reaction.
3. **Volition proper (§1)** — the impulse queue, quota and rising bar, now that there is something to
   arbitrate between.
4. **Two-way notifications (§5)** — the *Done* button is the acted-on signal that makes learned
   weights possible.
5. **Learned weights (§1)** — only now, with a log of what fired and what was acted on.

Everything in §4 can land at any point and makes each of the above better. §6 and §7 are their own
projects and depend on nothing here.
