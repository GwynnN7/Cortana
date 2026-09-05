# Next session — the plan

The numbered plan this file used to carry is finished: the fabric model, the automation
re-architecture, the Hardware/Devices/Sensors/Dashboard rebuild, cascade rules, per-registration
icons, the services audit — all shipped, and all described in `DEV.md`, which is where the *how it
works* belongs. The blow-by-blow of how each landed is in `git log`; this file is now only what is
still open, what was decided on purpose, and what is worth not re-learning.

---

## Actually open

Both of these are from `IDEAS.md`, not from anything half-built here.

- **The attention budget** — `IDEAS §1`. She speaks unprompted twice a day at fixed hours and
  otherwise only when spoken to. The quota, the rising salience bar, the novelty cooldown and
  silence-as-the-default-output are all still unwritten. This is the big one
- **An end-of-session digest** — `IDEAS §3`. Keeping the daily digests is done (`Days.json`,
  `Rhythm(metric)`, `CompareToUsualDay`, and `POST /history/days` to backfill), so what is left is the
  trigger: when a long gaming or coding stretch ends, one line about what it cost. `HistoryService.
  Digest` already takes an arbitrary window

**Telegram and Discord are done.** This file used to say their menus still assumed a fixed device
list. They do not, and have not for a while: the Telegram menus render from `snapshot.Devices`,
`snapshot.Sensors` and `snapshot.Plugins`, `SystemMenu.Machine` draws any source from its facts and
readings, and Discord takes device, sensor and feature as free-form ids. Neither holds a `DeviceId`
enum any more. They are deliberately read-and-switch: no registrations, no binds, no warnings, no
hardware settings — the only configuration either exposes is the AI model, and Telegram's prompt
editor. Keep them that way; the web is where the house gets configured.

---

## Decided on purpose, so do not "fix" them

- **A registration's id cannot be renamed.** History columns and binds key off it. gwynn7 decided
  this stays
- **Bind and warning *contents* are never migrated.** Editing a user's triggers is presumptuous, so a
  change to a shipped default only reaches fresh installs. No longer invisible: the bind and warning
  lists flag which shipped ones have drifted and offer Reset
- **`Fabric.Seed` is additive**, so deleting a shipped default brings it back on the next restart.
  Acceptable while the model is still moving; record tombstones if it ever becomes annoying
- **`UpgradePresence` maps every legacy `feedsPresence: true` to `Reports`,** `at_desk` included.
  Which sensors should only *sustain* presence is a judgement about the room, not something to infer
  from a bool that could not express it. Set it on the Hardware page; on this Pi `at_desk` was moved
  to `Sustains` through the API
- **`station/air_temperature` is announced but unregistered.** Free, and probably should stay that way
- **Warning hysteresis is fixed at ×1.15 / ×0.9.** Fine, though `Below` triggers use `2 - margin`,
  which is correct and unobvious
- **Device state resets to Off whenever the Kernel restarts.** GPIO outputs cannot be read back. It is
  pre-existing and now resets per channel

---

## Raised by review and refuted — do not re-raise

Each of these looks like a bug, was investigated properly, and is not one.

- **The presence latch "losing" presence on a single low pass.** That *is* the contract: a `Sustains`
  sensor may never announce presence, only extend it. `reported` is the live PIR level, not the 30s
  window, so the next station batch re-arms it
- **`Lately` feeding her own greetings back as gwynn7's words.** Turns are tagged `you:` versus
  `gwynn7:` from `ChatRole`, so nothing is misattributed
- **`Patience` compounding across tool rounds.** A second brief limit inside one `Send` exceeds the
  budget and steps down instead, so the deliberate sleep is capped per request

---

## Traps worth not re-learning

- **A Razor `string` parameter needs the `@`.** `Current="_tab"` passes the literal text `_tab`;
  `Current="@_tab"` passes the field. Non-string parameters compile as expressions either way, which
  is exactly what hides it. This silently broke tab swiping, the slide animation and the active-tab
  highlight for two sessions
- **`~/.config/cortana/CortanaKernel/Prompt.txt` shadows the shipped prompt.** Any prompt improvement
  in `CortanaLib/Storage/prompt.txt` is invisible while it exists. Check after editing either
- **A fix in the working tree is not a fix until it is deployed**, and the two machines ship
  separately: `cortana deploy kernel web` reaches the Pi, `cortana service build && cortana service
  restart` reaches the desktop agent
- **Bump `CACHE` in `service-worker.js` for any web asset change.** The fetch handler is cache-first
  and ignores HTTP freshness, so an installed PWA keeps serving the old file otherwise
- **`Compose` must never be handed tools.** Asked for two sentences with the full tool set in reach,
  she ran the house instead and stored her own tool-call narration as the day's summary
