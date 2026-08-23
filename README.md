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
  - Blazor Server dashboard exposing every API from the browser: devices, sensors and their
    automation settings, Raspberry info and shell, subfunction control, and the media utilities.
    Installable as a phone app, with home-screen shortcuts for the quick toggles

### Modules

- **Cortana Desktop**
  - Computer software to handle PC through **Cortana** with **Client-Server** communication
- **Cortana Embedded**
  - Collection of scripts running on embedded devices with **Client-Server** or **REST API** communications

Each **Subfunction** runs as a **systemd user service** managed by the **Bootloader**. The **Kernel** service auto-starts all sub-services on boot and cascades stop/restart to them. They communicate with the **Kernel** through **Cortana API** and **Redis IPC**

Each **Module** is a standalone software that runs on a different device and communicates with **Cortana's Kernel** through **Cortana API** or **Hardware API**

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

Then remove `CORTANA_PASSWORD` from `.env`. With it unset the Kernel uses `sudo -n`; with it set it
falls back to piping the password into `sudo -S`, which leaves it visible in `ps` output.

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

If `CORTANA_API_KEY` is **not** configured, ReadOnly and Sensitive routes answer `503` and refuse to
run at all — the API cannot be left accidentally open. Failures are RFC 9457 problem details
(`401` wrong or missing key, `503` no key configured, `404` unknown enum value, `503` hardware offline).

OpenAPI docs (`/scalar`) stay reachable without a key.

The web dashboard has its own gate: set `CORTANA_WEB_PASSWORD` to require a passcode. The dashboard
can power off the Pi and the desktop, so on any network that is not fully trusted this should be set.

---

## API Reference

Note: "**cortana.net**" is a placeholder for the actual address, which is private. API docs are available at `http://cortana.net/scalar`.

Every endpoint answers in two formats, chosen by the `Accept` header:

- `text/plain` — a human-readable line, used by the Telegram and Discord bots
- `application/json` — a typed object, used by the dashboard

Every collection route (`GET /Devices`, `GET /Sensors`, `GET /Settings`, `GET /Raspberry`,
`GET /SubFunctions`) returns every member at once, and `GET /status` returns all of them in a single
response.

#### Home

```http
  GET http://cortana.net/
  GET http://cortana.net/health
```

#### Status

```http
  GET http://cortana.net/status
  GET http://cortana.net/events
```

`/status` returns a full snapshot: devices, sensors, settings, Raspberry info and subfunction states.

`/events` is the same snapshot as a **server-sent-event stream**, pushed whenever anything changes
(device switched, sensor frame, setting written, subfunction started) plus a 20-second heartbeat.
The dashboard consumes this and only falls back to polling `/status` while the stream is down.

#### Devices

```http
  GET  http://cortana.net/Devices
  GET  http://cortana.net/Devices/{device}
  POST http://cortana.net/Devices/{device}
  POST http://cortana.net/Devices/sleep
```

| Parameter           | Type     | Description                 | Values                                                   |
| :------------------ | :------- | :-------------------------- | :------------------------------------------------------- |
| `device`            | `string` | **Device** to interact with | **Computer**, **Lamp**, **Power**, **Generic**, **room** |
| `PostAction.action` | `string` | **Action** for the device   | **On**, **Off**, **Toggle** (default **Toggle**)         |

#### Raspberry

```http
  GET  http://cortana.net/Raspberry
  GET  http://cortana.net/Raspberry/{info}
  POST http://cortana.net/Raspberry/
```

| Parameter             | Type     | Description                | Values                                             |
| :-------------------- | :------- | :------------------------- | :------------------------------------------------- |
| `info`                | `string` | **Info** to retrieve       | **Temperature**, **Location**, **Ip**, **Gateway** |
| `PostCommand.command` | `string` | **Command** to execute     | **Shutdown**, **Reboot**, **Command**              |
| `PostCommand.args`    | `string` | Shell command for **Command** |                                                 |

#### Computer

```http
  GET  http://cortana.net/Computer/
  POST http://cortana.net/Computer/
```

| Parameter             | Type     | Description                | Values                                                                     |
| :-------------------- | :------- | :------------------------- | :------------------------------------------------------------------------- |
| `PostCommand.command` | `string` | **Command** to execute     | **Shutdown**, **Suspend**, **Reboot**, **System**, **Command**, **Notify** |
| `PostCommand.args`    | `string` | Optional text **argument** |                                                                            |

#### Sensors

```http
  GET http://cortana.net/Sensors
  GET http://cortana.net/Sensors/{sensor}
```

| Parameter | Type     | Description               | Values                                                                          |
| :-------- | :------- | :------------------------ | :------------------------------------------------------------------------------ |
| `sensor`  | `string` | **Sensor** to get data of | **Motion**, **Temperature**, **Light**, **Humidity**, **CO2**, **Tvoc**         |

#### Subfunctions

```http
  GET  http://cortana.net/SubFunctions
  GET  http://cortana.net/SubFunctions/{subfunction}
  POST http://cortana.net/SubFunctions/{subfunction}
  POST http://cortana.net/SubFunctions/
```

| Parameter             | Type     | Description                      | Values                                                                     |
| :-------------------- | :------- | :------------------------------- | :------------------------------------------------------------------------- |
| `subfunction`         | `string` | **Subfunction** to interact with | **CortanaKernel**, **CortanaTelegram**, **CortanaDiscord**, **CortanaWeb** |
| `PostAction.action`   | `string` | **Action** to execute            | **Start**, **Restart**, **Update**, **Stop**                               |
| `PostCommand.command` | `string` | **Type of Message** to publish   | **Telegram**, **Discord**                                                  |
| `PostCommand.args`    | `string` | **Message Text** to publish      |                                                                            |

#### Schedules

```http
  GET    http://cortana.net/Schedules
  GET    http://cortana.net/Schedules/{id}
  POST   http://cortana.net/Schedules
  POST   http://cortana.net/Schedules/{id}
  DELETE http://cortana.net/Schedules/{id}
```

Persistent, kernel-owned schedules, stored in `~/.config/cortana/CortanaKernel/Schedules.json` and
re-armed on boot. Replaces the in-memory timers each bot used to keep to itself.

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
older ones are dropped and logged. `Interval` schedules skip whole missed periods rather than firing
a catch-up burst.

#### Settings

```http
  GET  http://cortana.net/Settings
  GET  http://cortana.net/Settings/{setting}
  POST http://cortana.net/Settings/{setting}
```

| Parameter         | Type     | Description         | Values                                                                                                                        |
| :---------------- | :------- | :------------------ | :---------------------------------------------------------------------------------------------------------------------------- |
| `setting`         | `string` | **Setting** to read or write | **LightThreshold**, **LampToggle**, **CO2Threshold**, **TvocThreshold**, **AutomaticMode**, **MorningHour**, **MotionOffMax**, **MotionOffMin** |
| `PostValue.value` | `number` | **Value** to update. For the `On`/`Off` settings, any other number toggles.                                                                     |

---

## Installing the dashboard on a phone

The dashboard ships a web manifest and a service worker, so Chrome on Android offers **Install app**
(iOS: Share -> Add to Home Screen). Once installed it runs without browser chrome, and long-pressing
the icon exposes shortcuts for **Toggle lamp**, **Room on**, **Room off** and **Sleep mode**, which
open `/quick/{action}`.

Blazor Server needs a live connection, so the service worker caches only the shell and shows an
offline page rather than pretending to work. Installation requires the site be served over HTTPS or
from `localhost`; on a plain-HTTP LAN vhost the browser will not offer to install it.

---

## Automation

The lamp is driven by motion, light level and a day/night window.

| State         | When                                                    | Motion turns lamp on | Motion turns lamp off |
| :------------ | :------------------------------------------------------ | :------------------- | :-------------------- |
| **Automatic** | `AutomaticMode` on, outside the night window, no hold   | yes, below `LightThreshold` | yes |
| **Night**     | inside `[NightHour, MorningHour)`                        | no                   | yes, after `MotionOffMin` |
| **Manual**    | `AutomaticMode` off, or a lamp switched by hand recently | no                   | no |

- Switching the lamp by hand starts a **manual hold** lasting `ManualModeMinutes`. Setting
  `AutomaticMode` back on ends the hold immediately.
- The night window wraps midnight, so `NightHour: 23` / `MorningHour: 9` behaves as expected.
- At the night boundary, **if the computer is still on Cortana does not touch the lamp**: it sends a
  desktop notification instead and re-applies night as soon as the computer goes off.
- `POST /Devices/sleep` applies night behaviour immediately, computer or not.
- Lamp-off timeout is `MotionOffMax` when the computer is on, `MotionOffMin` at night or when it is off.
- Boundaries are scheduled onto the exact hour rather than polled.

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

redis-server --daemonize yes
sudo cp Cortana/CortanaKernel/Scripts/nginx /etc/nginx/sites-available/default
sudo systemctl enable nginx

# Environment
echo '﻿alias temp='/bin/vcgencmd measure_temp'' >> ~/.zshrc
echo 'export PATH=$PATH:/home/cortana/.local/bin' >> ~/.zshrc
```

`ffmpeg` is required for Discord voice playback.

### Discord voice encryption

**libdave is not required.** DAVE is Discord's optional end-to-end encryption for voice; without it
audio is still encrypted in transit to Discord's servers, exactly as every bot worked before DAVE
existed. Discord does not require bots to support it.

`Discord.Net.Dave` is a P/Invoke binding to a native `libdave` that ships for no runtime identifier,
and there is no Raspberry Pi OS build of it. Left at its default (`null` = "use it if available")
the library sees the managed assembly, tries to negotiate DAVE, and the voice session fails - hardest
in channels with more than one person, which is where the MLS group actually matters.

So the package reference is removed and `EnableVoiceDaveEncryption = false` is set explicitly in
`CortanaDiscordBot.ConfigureSocket`. Nothing else is needed. To adopt DAVE later, build libdave for
`linux-arm64`, put it next to the executable, re-add the package, and flip the flag.

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

`cortana-desktop.service` hardcodes its paths — edit them if the repo is not at
`~/Projects/Cortana`. The `cortana` CLI wraps the service, git, the API and desktop notifications:

```bash
cortana service restart
cortana api sensors
cortana api Devices/Lamp -act toggle
cortana api raspberry/temperature | cortana notify
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
