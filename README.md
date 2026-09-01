![Logo](CortanaWeb/wwwroot/favicon.png)

# Cortana

**Halo** inspired **home assistant** and **artificial intelligence**.

As **home assistant** she watches the sensors, switches devices, handles decisions and automations, controls the desktop computer and keeps a history of everything.

As **artificial intelligence** she can be directly queried for actions, schedules or information, or accessed via different interfaces.

She lives on a **Raspberry Pi 4** and is written in **C# / .NET 10**.

---

## What she does

- **Devices** — lamp, mains supply, the desktop computer and a generic socket, over the Pi's GPIO relays.
- **Sensors** — temperature, humidity, light, motion, CO₂ and TVOC from an ESP32 station.
- **Automation** — motion and light decide the lamp, with manual overrides that expire on their own.
- **Scheduling** — one-off, repeating and event-triggered actions that survive a restart.
- **AI** — a conversational front over every one of those capabilities.
- **Notifications** — a persistent browser status notification, plus Telegram and Discord.

## The pieces

| Project | What it is |
| :--- | :--- |
| **CortanaKernel** | The brain. Owns all state and the automation rules, and exposes the REST API and the event streams. |
| **CortanaLib** | Contracts, the API client and shared utilities, referenced by every process. |
| **CortanaWeb** | The dashboard: a Blazor Server app, installable as a phone app. |
| **CortanaTelegram** | Telegram bot, one updating menu per topic. |
| **CortanaDiscord** | Discord bot, slash commands. |
| **CortanaDesktop** | The desktop agent and the desktop `cortana` CLI. |
| **CortanaEmbedded** | The ESP32 sensor station firmware. |

Only the Kernel decides anything. Every client, and the AI, goes through the same API.

---

## Setup

### Requirements

- .NET 10 SDK (with the ASP.NET Core runtime) on the Pi
- `yt-dlp` on the Pi — **required** for anything YouTube; there is no fallback
- `nginx` if you want the dashboard and API behind one host
- A passwordless `sudo` rule for the Pi's own shutdown and reboot

### Configuration

Everything lives in `~/.config/cortana`.

`~/.config/cortana/.env` holds the environment, and systemd loads the same file, so `dotnet run`
behaves like the installed services.

| Variable | Needed by | Description |
| :--- | :--- | :--- |
| `CORTANA_API` | everything | Base URL of the Kernel's REST API |
| `CORTANA_API_KEY` | **everything** | Shared secret for `X-Api-Key`. Without it every route except `/` and `/health` is disabled |
| `CORTANA_API_PORT` | Kernel | Port the API listens on |
| `CORTANA_TCP_PORT` | Kernel, Desktop | Port the ESP32 station and the desktop agent connect to |
| `CORTANA_GEMINI_KEY` | Kernel | Language model key. Without it the AI is unavailable |
| `CORTANA_WEB_PORT` | Web | Dashboard port, 5118 by default |
| `CORTANA_WEB_PASSWORD` | Web | Dashboard passcode. Unset means the dashboard is open |
| `CORTANA_TELEGRAM_TOKEN` | Telegram | Bot token |
| `CORTANA_DISCORD_TOKEN` | Discord | Bot token |
| `CORTANA_IGDB_CLIENT` / `CORTANA_IGDB_SECRET` | Discord | Game lookups |
| `CORTANA_KERNEL_HOST` | Desktop | Kernel address for the agent's socket. Derived from the Pi's gateway when unset |
| `CORTANA_PASSWORD` | Kernel | Fallback sudo password; prefer a sudoers rule and leave it unset |

Alongside it, per-project JSON files:

- `CortanaKernel/Network.json` — a list of `{ location, gateway, desktopIp, desktopMac }` profiles.
  The Pi picks the one whose gateway matches, so the same build works in both houses.
- `CortanaTelegram/Data.json` — the home group id, the topic ids and the known usernames.
- `CortanaDiscord/Data.json` — the bot, owner, guild and channel ids.

### Install on the Pi

```bash
git clone <repo> ~/Cortana
~/Cortana/CortanaKernel/Scripts/cortana install
~/Cortana/CortanaKernel/Scripts/cortana start
```

The Kernel unit pulls the other three services up with it, and stopping it stops them all.
`cortana status`, `cortana log` and `cortana update` do what they say.

### Install on the desktop

Put `CortanaDesktop/Scripts/cortana` on your `PATH` and copy
`CortanaDesktop/Scripts/cortana-desktop.service` into `~/.config/systemd/user/`.

The agent builds to `~/.local/share/cortana/desktop`, so nothing lands in the checkout.

```bash
cortana service start          # the resident agent
cortana chat                   # interactive conversation
cortana chat "turn the lamp on"
cortana ask "how hot is it?"   # one-shot interaction with no history
cortana status                 # the whole house in the terminal
cortana deploy kernel -l       # sync this tree to the Pi and follow the journal
```

### The station

Needs `arduino-cli` with the ESP32 core:

```bash
arduino-cli config init --additional-urls https://espressif.github.io/arduino-esp32/package_esp32_index.json
arduino-cli core update-index && arduino-cli core install esp32:esp32
```

Fill in the credentials once, then flash:

```bash
cp CortanaEmbedded/ESP32Station/secrets.example.h CortanaEmbedded/ESP32Station/secrets.h
$EDITOR CortanaEmbedded/ESP32Station/secrets.h
cortana flash # or cortana flash /dev/ttyACM0
```

`cortana flash` copies `secrets.h` to `~/.config/cortana/esp32-secrets.h`, builds, uploads, and then removes it from the checkout.
Override the board with `CORTANA_ESP32_FQBN` and the port with `CORTANA_ESP32_PORT`.

---

## Using her

The **dashboard** is the main surface: devices, sensors, plots, schedules, settings and a chat
window. Add it to your home screen and it behaves like an app, with one persistent notification that
always shows the current state of the room.

**Telegram** gives one updating menu per topic in the home group. **Discord** uses slash commands
under `/home`, `/utility`, `/remind`, `/random`, `/games`, `/server` and `/settings`.

And you can simply ask:

> Turn the lamp on. · Put me in sleep mode. · What was the worst air quality yesterday? ·
> Why didn't the lamp turn on? · Turn on the pc and then reboot into Windows.

---

## Licence

See [LICENSE](LICENSE).
