![Logo](CortanaWeb/wwwroot/favicon.png)

# Cortana

**Halo** inspired **Home Assistant** and _Artificial Intelligence_

My personal assistant in daily routines, integrated with **sensors**, **devices**, **applications** and **internet**

Currently living on **Raspberry Pi 4** running mostly on **C# .NET and ASP.NET CORE**

---

## Structure

### Kernel

- **Bootloader**
  - Builds, boots and stops **subfunctions** and check their status
- **Hardware API**
  - Gives an interface to the **Kernel** to interact with hardware devices and _GPIO_ in the room
- **Cortana API**
  - Gives an interface through **REST-API** to interact with **Cortana**'s functions

### Subfunctions

- **Cortana Telegram**
  - Telegram bot to integrate **Cortana** with _Telegram_
- **Cortana Discord**
  - Discord bot to integrate **Cortana** with _Discord_
- **Cortana Web**
  - Blazor Server dashboard covering every API from the browser, and installable as a phone app

### Modules

- **Cortana Desktop**
  - Computer software to handle PC through **Cortana** with **Client-Server** communication
- **Cortana Embedded**
  - Collection of scripts running on embedded devices with **Client-Server** or **REST API** communications

Each **Subfunction** runs as a **systemd user service** managed by the **Bootloader**: the Kernel starts them on boot and cascades stop/restart, and they talk back through the **Cortana API** and **Redis IPC**. Each **Module** is standalone software on another device, reaching the Kernel over the same API.

---

## Configuration

All configuration lives in `~/.config/cortana`. Environment variables are read from
`~/.config/cortana/.env`, which systemd loads via `EnvironmentFile=` and which every entry point
also loads itself, so `dotnet run` behaves the same as the installed services.

| Variable                 | Required | Description                                                    |
| :----------------------- | :------- | :------------------------------------------------------------- |
| `CORTANA_API`            | yes      | Base URL of the Cortana REST API, used by every subfunction     |
| `CORTANA_API_PORT`       | yes      | Port the Kernel serves the REST API on                          |
| `CORTANA_TCP_PORT`       | yes      | Port the Kernel listens on for the desktop agent and the ESP32  |
| `CORTANA_PASSWORD`       | no       | Legacy sudo password. Leave unset once the sudoers rule below is in place |
| `CORTANA_TELEGRAM_TOKEN` | Telegram | Telegram bot token                                              |
| `CORTANA_DISCORD_TOKEN`  | Discord  | Discord bot token                                               |
| `CORTANA_IGDB_CLIENT`    | Discord  | IGDB client id for the game lookup commands                     |
| `CORTANA_IGDB_SECRET`    | Discord  | IGDB client secret                                              |
| `CORTANA_API_KEY`        | **yes**  | Shared secret for `X-Api-Key`. Without it every route except `/` and `/health` is disabled |
| `CORTANA_WEB_PASSWORD`   | no       | When set, the web dashboard requires this passcode to sign in   |
| `CORTANA_WEB_PORT`       | no       | Port the dashboard binds (default `5118`, must match the nginx vhost) |
| `CORTANA_REDIS`          | no       | Redis connection string for IPC (default `localhost`)           |
| `CORTANA_SHELL`          | no       | Shell used to run commands (auto-detected)                      |
| `CORTANA_GEMINI_KEY`     | Chat     | Google AI Studio key. Without it every chat route answers `503` |

Same file on the desktop (`~/.config/environment.d/cortana.conf` there) needs `CORTANA_PATH`,
`CORTANA_API` and `CORTANA_API_KEY` for the `cortana` CLI and the desktop agent.

---

## Privileged commands

Shutdown, reboot and wake-on-LAN need root. Grant them without a password:

```bash
sudo visudo -f /etc/sudoers.d/cortana
```

```
cortana ALL=(root) NOPASSWD: /sbin/shutdown, /sbin/reboot, /usr/sbin/etherwake, /usr/bin/wakeonlan
```

```bash
sudo chmod 0440 /etc/sudoers.d/cortana
sudo -n /sbin/shutdown --help >/dev/null && echo "sudoers rule works"
```

Then remove `CORTANA_PASSWORD` from `.env`: unset, the Kernel uses `sudo -n`; set, it pipes the
password into `sudo -S`, leaving it visible in `ps`.

---

## Authentication

Every route carries an access tier, enforced by one middleware:

| Tier          | Routes                                | Behaviour                                             |
| :------------ | :------------------------------------ | :---------------------------------------------------- |
| **Public**    | `GET /`, `GET /health`                | Always reachable, no key                              |
| **ReadOnly**  | every other `GET`                     | Requires `X-Api-Key`                                  |
| **Sensitive** | every `POST`                          | Requires `X-Api-Key`                                  |

| Header      | Value                        |
| :---------- | :--------------------------- |
| `X-Api-Key` | Your `CORTANA_API_KEY` value |

Without `CORTANA_API_KEY` the ReadOnly and Sensitive routes answer `503` rather than running open.
Failures are RFC 9457 problem details; the OpenAPI docs (`/scalar`) stay reachable without a key.

The dashboard has its own gate, `CORTANA_WEB_PASSWORD`. It can power off the Pi and the desktop, so
set it on any network you do not fully trust.

---

## API Reference

Routes are shown relative to the API base; OpenAPI docs live at `/scalar`.

Every endpoint answers in two formats, chosen by `Accept`: `text/plain` for the bots and CLI,
`application/json` for the dashboard. Collection routes return every member at once, and
`GET /status` returns all of them together.

#### Home

```http
  GET /
  GET /health
```

#### Status

```http
  GET /status
  GET /events
```

`/status` returns a full snapshot: devices, sensors, settings, Raspberry info, subfunction states,
the desktop's last metrics and the automation state. `/events` is the same snapshot as a
**server-sent-event stream**, pushed on every change plus a 20-second heartbeat; the dashboard falls
back to polling `/status` only while the stream is down.

#### Devices

```http
  GET  /Devices
  GET  /Devices/{device}
  POST /Devices/{device}
  POST /Devices/sleep
```

| Parameter           | Type     | Description                 | Values                                                   |
| :------------------ | :------- | :-------------------------- | :------------------------------------------------------- |
| `device`            | `string` | **Device** to interact with | **Computer**, **Lamp**, **Power**, **Generic**, **room** |
| `PostAction.action` | `string` | **Action** for the device   | **On**, **Off**, **Toggle** (default **Toggle**)         |

#### Raspberry

```http
  GET  /Raspberry
  GET  /Raspberry/{info}
  POST /Raspberry/
```

| Parameter             | Type     | Description                | Values                                             |
| :-------------------- | :------- | :------------------------- | :------------------------------------------------- |
| `info`                | `string` | **Info** to retrieve       | **Temperature**, **Location**, **Ip**, **Gateway** |
| `PostCommand.command` | `string` | **Command** to execute     | **Shutdown**, **Reboot**, **Command**              |
| `PostCommand.args`    | `string` | Shell command for **Command** |                                                 |

#### Computer

```http
  GET  /Computer/
  POST /Computer/
  GET  /Computer/metrics
  POST /Computer/metrics
```

| Parameter             | Type     | Description                | Values                                                                                |
| :-------------------- | :------- | :------------------------- | :------------------------------------------------------------------------------------ |
| `PostCommand.command` | `string` | **Command** to execute     | **Shutdown**, **Suspend**, **Reboot**, **System**, **Command**, **Notify**, **Launch** |
| `PostCommand.args`    | `string` | Optional text **argument** |                                                                                       |

**Command** waits for the process to finish and returns its output, killing it after 18 seconds.
**Launch** starts a detached process with `setsid` and returns immediately, which is what graphical
applications need. `GET /Computer/metrics` returns the last snapshot pushed by the desktop agent,
flagged `stale` once it is older than two minutes.

#### Sensors

```http
  GET /Sensors
  GET /Sensors/{sensor}
```

| Parameter | Type     | Description               | Values                                                                          |
| :-------- | :------- | :------------------------ | :------------------------------------------------------------------------------ |
| `sensor`  | `string` | **Sensor** to get data of | **Motion**, **Temperature**, **Light**, **Humidity**, **CO2**, **Tvoc**         |

#### Subfunctions

```http
  GET  /SubFunctions
  GET  /SubFunctions/{subfunction}
  POST /SubFunctions/{subfunction}
  POST /SubFunctions/
```

| Parameter             | Type     | Description                      | Values                                                                     |
| :-------------------- | :------- | :------------------------------- | :------------------------------------------------------------------------- |
| `subfunction`         | `string` | **Subfunction** to interact with | **CortanaKernel**, **CortanaTelegram**, **CortanaDiscord**, **CortanaWeb** |
| `PostAction.action`   | `string` | **Action** to execute            | **Start**, **Restart**, **Update**, **Stop**                               |
| `PostCommand.command` | `string` | **Type of Message** to publish   | **Telegram**, **Discord**                                                  |
| `PostCommand.args`    | `string` | **Message Text** to publish      |                                                                            |

#### Schedules

```http
  GET    /Schedules
  GET    /Schedules/{id}
  POST   /Schedules
  POST   /Schedules/{id}
  DELETE /Schedules/{id}
```

Persistent kernel-owned schedules, stored in `~/.config/cortana/CortanaKernel/Schedules.json` and
re-armed on boot.

| Field                    | Description                                                                        |
| :----------------------- | :---------------------------------------------------------------------------------- |
| `name`                   | Free text                                                                            |
| `trigger`                | **Once**, **Interval**, **Daily**, **Weekly**, **Event**                             |
| `at`                     | Timestamp, for `Once`                                                                |
| `intervalSeconds`        | For `Interval`, minimum 10                                                           |
| `hour` / `minute`        | For `Daily` and `Weekly`                                                             |
| `day`                    | For `Weekly`                                                                         |
| `event`                  | **ComputerOn**, **ComputerOff**, **NightStart**, **MorningStart**                    |
| `actionType`             | **Device**, **Room**, **Computer**, **Raspberry**, **Setting**, **Notify**, **Subfunction** |
| `target` / `value`       | Meaning depends on `actionType`, validated against the same enums as the live routes |

`POST /Schedules/{id}` takes `{"command": "enable" \| "disable" \| "run"}`.

A `Once` schedule missed while the Kernel was down still fires if it was due within the last hour;
older ones are dropped. `Interval` schedules skip missed periods rather than firing a catch-up burst.

#### Settings

Automation settings live under **Sensors** because that is what they drive, and the logging
switches live under **SubFunctions** because they choose which subfunction mirrors the log.

```http
  GET  /Sensors/settings
  GET  /Sensors/settings/{setting}
  POST /Sensors/settings/{setting}
  GET  /SubFunctions/logs
  POST /SubFunctions/logs/{target}
```

`target` is **Web**, **Telegram** or **Discord**.

| Parameter         | Type     | Description         | Values                                                                                                                        |
| :---------------- | :------- | :------------------ | :---------------------------------------------------------------------------------------------------------------------------- |
| `setting`         | `string` | **Setting** to read or write | **LightThreshold**, **LampToggle**, **CO2Threshold**, **TvocThreshold**, **AutomaticMode**, **MorningHour**, **NightHour**, **MotionOffMax**, **MotionOffMin**, **ManualModeMinutes** |
| `PostValue.value` | `number` | **Value** to update. For the `On`/`Off` settings, any other number toggles.                                                                     |

#### AI

```http
  POST   /AI
  DELETE /AI/{conversation}
  GET    /AI/prompt
  POST   /AI/prompt
  DELETE /AI/prompt
  GET    /AI/models
  GET    /AI/model
  POST   /AI/model
  GET    /AI/settings
  GET    /AI/settings/{setting}
  POST   /AI/settings/{setting}
```

| Parameter              | Type      | Description                                                        | Values                                                        |
| :--------------------- | :-------- | :------------------------------------------------------------------ | :-------------------------------------------------------------- |
| `PostChat.message`     | `string`  | What to ask                                                         |                                                               |
| `PostChat.conversation`| `string`  | Thread key. The prefix decides which tools are offered              | `web:*`, `telegram:*`, `discord:*`, `desktop:*`                |
| `PostChat.author`      | `string`  | Display name, so she can address people by name                     |                                                               |
| `PostChat.remember`    | `boolean` | `false` runs one-shot: reads no history and writes none             | default `true`                                                 |
| `PostChat.owner`       | `boolean` | Whether she is talking to her owner. Forced true off Discord        | default `true`                                                 |
| `PostModel.model`      | `string`  | Model family to switch to                                           | **Flash**, **Flash Lite**, **Gemma**                          |
| `PostPrompt.prompt`    | `string`  | New system prompt. `DELETE` restores the shipped one                |                                                               |
| `setting`              | `string`  | AI setting to read or write                                         | **Temperature**, **History**, **DiscordMinutes**              |
| `PostNumber.value`     | `number`  | New value for that setting                                          |                                                               |

---

## AI

Cortana answers in her own voice through **Gemini**, from the web dashboard, Telegram, Discord and
the terminal. The Kernel owns it, so every surface shares one implementation, one conversation store
and one set of tools.

Get a free key at [aistudio.google.com](https://aistudio.google.com/apikey) and set
`CORTANA_GEMINI_KEY`. Without it the AI routes answer `503` and everything else keeps working.

### Tools

She reads and drives the house rather than talking about it. Tools are plain C# methods described
with `[Description]`; `Microsoft.Extensions.AI` generates their schemas and binds the arguments.

| Tool                                              | What it does                                  |
| :------------------------------------------------ | :--------------------------------------------- |
| `GetDevices` `GetSensors` `GetSettings`            | read the house                                 |
| `GetComputerMetrics`                               | desktop performance and temperatures           |
| `SwitchDevice` `SetSetting` `EnterSleepMode`       | change the house                               |
| `RunOnComputer` `LaunchOnComputer` `RunOnRaspberry`| shell and app launching on either machine      |

**Discord gets only the four read-only tools.** Anyone in a channel can talk to her, so she must not
switch hardware or run commands from there. The Kernel decides this from the `discord:` conversation
prefix, so a bug in the bot cannot widen its own access, and a prompt line tells her to say that
changes have to come from the web app, Telegram or the terminal.

Gemini 3 requires the `thought_signature` it returns on a function call to be echoed back verbatim,
so the transport keeps the model's parts untouched inside an exchange.

### History

Threads are keyed per surface: `discord:{channel}`, `telegram:{topic}`, `web:{browser}`,
`desktop:{host}`. Only prose is kept — tool calls and results are dropped once she has answered, so
stale readings cannot resurface as current facts. Threads keep the last `History` exchanges, cap at
60 entries, and expire after 6 hours idle; trimming only cuts on a question boundary.

`remember: false` runs one-shot: nothing read, nothing stored. `cortana ask` uses it.

On Discord, `@Cortana` opens a session and clears what came before. While it is open every message
in that channel continues the conversation without a mention, and each message pushes the expiry out
by `DiscordMinutes`. Once it lapses she ignores the channel until tagged again.

### Model

You pick a **family**, not a version. She runs the newest model in it and falls back down the chain
when one is unavailable.

| Family       | Chain                                                   | Free tier                                  |
| :----------- | :------------------------------------------------------- | :------------------------------------------ |
| `Flash`      | `gemini-3.7-flash` → `3.6` → `3.5` → `3-flash-preview`   | 5/min, 20/day                               |
| `Flash Lite` | `gemini-3.5-flash-lite` → `3.1` → `2.5`                  | 15/min, 500/day — the default               |
| `Gemma`      | `gemma-4-31b-it` → `gemma-4-26b-a4b-it`                  | 30/min, 14.4k/day, weaker in character      |

The chain is rebuilt from Google's model list at boot and at 00:01, so new versions appear without a
code change. Nothing is probed per request — picking a model is an in-memory lookup, and the
fallback only moves down the chain **after** a `429` or `503`. Cooldowns match the cause: a
per-minute limit parks the model for whatever `RetryInfo` asks, a per-day limit until after
midnight, an overloaded model briefly. The next model answers within the same request:

```
gemini-3.7-flash unavailable, parked for 90s     # 503, overloaded
falling back to gemini-3.6-flash
gemini-3.6-flash unavailable, parked for 1.9h    # 429, daily quota spent
falling back to gemini-3.5-flash                 # answered
```

### Settings

Everything is editable at runtime from the web **AI** page, the Telegram **Cortana → Settings** menu
and the Discord `/hardware llm-*` commands. Model and the values below live in
`~/.config/cortana/CortanaKernel/Ai.json`, the prompt in `Prompt.txt` beside it; deleting the prompt
restores the one shipped in `CortanaLib/Storage/prompt.txt`.

| Setting          | Range      | Default | Meaning                                        |
| :--------------- | :--------- | :------ | :---------------------------------------------- |
| `Temperature`    | 0 – 2      | 0.9     | how much she improvises                        |
| `History`        | 1 – 40     | 8       | exchanges kept per thread                      |
| `DiscordMinutes` | 0.5 – 120  | 1       | how long a Discord session stays open          |

---

## Installing the dashboard on a phone

The dashboard ships a web manifest and a service worker, so Chrome on Android offers **Install app**
(iOS: Share -> Add to Home Screen). Once installed it runs without browser chrome, and long-pressing
the icon exposes shortcuts for **Toggle lamp**, **Toggle Computer** and **Sleep mode**, which open
`/quick/{action}`.

Blazor Server needs a live connection, so the service worker caches only the shell and shows an
offline page. Installing requires HTTPS or `localhost`; on a plain-HTTP LAN vhost the browser will
not offer it.

---

## Automation

The lamp is driven by motion, light level and a day/night window.

| State         | When                                                    | Motion turns lamp on | Motion turns lamp off |
| :------------ | :------------------------------------------------------ | :------------------- | :-------------------- |
| **Automatic** | `AutomaticMode` on, outside the night window, no hold   | yes, below `LightThreshold` | yes |
| **Night**     | inside `[NightHour, MorningHour)`                        | no                   | yes, after `MotionOffMin` |
| **Manual**    | `AutomaticMode` off, or a lamp switched by hand recently | no                   | no |

- Switching the lamp by hand starts a **manual hold** for `ManualModeMinutes`; turning `AutomaticMode`
  back on ends it immediately. The dashboard shows the derived state, so the toggle reads off while a
  hold or night mode is active.
- The night window wraps midnight, so `NightHour: 23` / `MorningHour: 9` works as expected, and
  boundaries are scheduled onto the hour rather than polled.
- At the night boundary, **if the computer is still on the lamp is left alone**: Cortana sends a
  desktop notification and applies night as soon as the computer goes off. `POST /Devices/sleep`
  applies it immediately either way.
- Lamp-off timeout is `MotionOffMax` when the computer is on, `MotionOffMin` at night or when off.

---

## Raspberry Configuration

### .NET Installation

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel STS [--runtime dotnet, aspnetcore]

echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.zshrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.zshrc
source ~/.zshrc

dotnet --version
```

### Dependencies

```bash
# Configuration files are located in .config/Cortana

# Dependencies
sudo apt install git zsh redis-server nginx ffmpeg opus-tools libopus0 libopus-dev libsodium-dev

# yt-dlp: YouTube extraction backend (see below). Not in apt at a usable version.
mkdir -p ~/.local/bin
curl -fsSL https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux_aarch64 -o ~/.local/bin/yt-dlp
chmod +x ~/.local/bin/yt-dlp

redis-server --daemonize yes
sudo cp Cortana/CortanaKernel/Scripts/nginx /etc/nginx/sites-available/default
sudo systemctl enable nginx

# Environment
echo '﻿alias temp='/bin/vcgencmd measure_temp'' >> ~/.zshrc
echo 'export PATH=$PATH:/home/cortana/.local/bin' >> ~/.zshrc
```

`ffmpeg` is required for Discord voice playback, and `libopus`/`libsodium` for the voice
encryption Discord.Net performs.

### YouTube extraction

`MediaHandler` prefers **yt-dlp**, falling back to YoutubeExplode when absent. YouTube enforces
proof-of-origin tokens on stream URLs that YoutubeExplode cannot produce, so it fails with *"Video is
not available"* on almost everything. Keep yt-dlp current with `yt-dlp -U`; override its location
with `CORTANA_YTDLP` if it is not on `PATH` or in `~/.local/bin`.

### Discord voice encryption

Discord **requires** DAVE, its end-to-end voice encryption: without it the voice websocket closes the
moment it opens. `Discord.Net.Dave` P/Invokes a native `libdave.so` that the NuGet package ships for
no runtime, so on arm64 it has to be compiled from
[discord/libdave](https://github.com/discord/libdave). The build lives at
`CortanaDiscord/Voice/libdave.so` and the unit file copies it beside the executable on every start,
because `dotnet build -o out` never cleans its output. `CORTANA_DISCORD_DAVE=false` skips it, though
Discord will then refuse the connection.

Cortana joins **undeafened**: DAVE members refuse an MLS add proposal for anyone not announced in a
previous `clients_connect`, and a `selfDeaf` client is never told who is in the media session.

**Voice is unreliable, and the cause is upstream.** Discord.Net's DAVE session sometimes builds an
empty recognised-user set, so it rejects the MLS welcome — including ones listing the bot's own user
id — resets, and loops until Discord closes the session and drops everyone from the channel:

```
Attempted to verify credential for unrecognized user ID: <the bot's own id>
MLS welcome lists unrecognized user ID
Resetting MLS session
```

Whether it happens is a matter of ordering, so reconnecting often clears it. The session reconnects
itself up to three times with exponential backoff when a connection dies within 20 seconds, which is
the signature of this failure. Nothing in this repo can fix it properly; it needs a patched
`Discord.Net.WebSocket`.

### ESP32

```bash
sudo curl -fsSL https://raw.githubusercontent.com/arduino/arduino-cli/master/install.sh | BINDIR=~/.local/bin sh
arduino-cli config init --additional-urls https://espressif.github.io/arduino-esp32/package_esp32_index.json
arduino-cli core update-index
arduino-cli core install esp32:esp32
arduino-cli lib install OneWire DallasTemperature WiFi

# Credentials are not committed: create secrets.h before the first build
cp CortanaEmbedded/ESP32Station/secrets.example.h CortanaEmbedded/ESP32Station/secrets.h
$EDITOR CortanaEmbedded/ESP32Station/secrets.h

arduino-cli compile --fqbn esp32:esp32:esp32-poe-iso ProjectName
arduino-cli upload -p /dev/ttyUSB0 --fqbn esp32:esp32:esp32-poe-iso ProjectName
```

---

## Run Locally

### Manually Run

```bash
git clone https://github.com/GwynnN7/Cortana.git

cd Cortana/CortanaKernel
dotnet build -o out --artifacts-path out/lib
./out/CortanaKernel
```

### Use Systemd Services

```bash
git clone https://github.com/GwynnN7/Cortana.git
cd Cortana/CortanaKernel/Scripts
chmod +x cortana
cortana install
cortana start

(run 'cortana help' for more commands)
```

---

## CortanaDesktop Configuration

### Linux

```bash
sudo pacman -S dotnet-sdk python-requests

# Environment for both the agent and the CLI
mkdir -p ~/.config/environment.d
cat >> ~/.config/environment.d/cortana.conf <<'EOF'
CORTANA_PATH=/path/to/Cortana
CORTANA_API=http://cortana.local/api
CORTANA_API_KEY=your_api_key
EOF

# Config file the agent reads for the Kernel's host part and TCP port
mkdir -p ~/.config/cortana
cat > ~/.config/cortana/Settings.json <<'EOF'
{ "networkAddr": "117", "tcpPort": 5116 }
EOF

# CLI and user service
install -Dm755 CortanaDesktop/Scripts/cortana ~/.local/bin/cortana
install -Dm644 CortanaDesktop/Scripts/cortana-desktop.service ~/.config/systemd/user/cortana-desktop.service
systemctl --user daemon-reload
systemctl --user enable --now cortana-desktop
```

`cortana-desktop.service` hardcodes its paths — edit them if the repo is not at `~/Projects/Cortana`.

The agent holds the TCP connection to the Kernel, runs what it is told, and every 30 seconds pushes
CPU, RAM, GPU, disk and uptime to `POST /Computer/metrics`, read straight from `/proc` and
`/sys/class/hwmon` — AMD (`k10temp`, `amdgpu`) and NVIDIA (`nvidia-smi`), no dependencies. The
snapshot rides the dashboard's SSE stream, so the Pi and both bots see it.

One `cortana` command covers both halves: the shell wrapper keeps what must work before the binary
exists (install, build, deploy, service, git) and hands everything else to the agent, building it
first if needed.

```bash
cortana service restart
cortana deploy                       # rsync this tree to the Pi and restart the services
cortana api sensors
cortana api Devices/Lamp -act toggle
cortana api raspberry/temperature | cortana notify

cortana chat                         # conversation, /reset clears it
cortana ask "is the lamp on?"        # one-shot, left out of the conversation
cortana monitor --watch              # live local bars
cortana pc                           # what the Pi last heard from this machine
cortana status                       # house, sensors, services and this computer
```

### Windows

```bash
# Windows
# Install dotnet-sdk
# Add CORTANA_PATH and CORTANA_API environment variable through GUI
# Create a shortcut of cortana-desktop.vbs in autostart folder
# Download notify-send (https://vaskovsky.net/notify-send/) and add it to PATH
```

---

<b>Note</b>: this repo contains just the source code of the project, and it's missing every <b>configuration file</b>, <b>api key</b> and <b>token</b> needed for the execution of <b>Cortana</b>.

## License

[GNU GPLv3](https://choosealicense.com/licenses/gpl-3.0/)
