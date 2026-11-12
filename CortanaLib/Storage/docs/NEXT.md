# Next — what is left to build

Features that do not exist yet. Nothing else belongs here: how a thing works and why it was decided
that way live in `DEV.md`, what has already shipped lives in `git log`, and speculative ground lives
in `IDEAS.md`. If something is small enough to argue about in here, it is small enough to just do.

---

## The attention budget — `IDEAS §1`

Volition and morning greetings are now opt-in. When enabled, the available occasions are still fixed
clock windows. What `IDEAS §1` describes is unwritten:

- a hard quota of unsolicited messages per waking day, refilled at `MorningHour`
- a rising bar — each message raises the salience threshold for a few hours, then it decays
- novelty required: a `Key` cannot fire inside its cooldown, and not at all without something new
- silence as the normal output, an impulse expiring unspoken being the common case

`VolitionService` is where it goes, and it is deliberately almost empty so this can be built into it.
Any new selection path must honour the persistent Off switch and decide whether to speak before
spending a model request.

## An end-of-session digest — `IDEAS §3`

When a long gaming or coding stretch ends, one line about what it cost: how long, what the air did,
how far the room warmed. `HistoryService.Digest` already takes an arbitrary window and the daily
digests are already kept (`Days.json`, `Rhythm(metric)`, `CompareToUsualDay`), so what is missing is
only the trigger that notices a stretch ending.
