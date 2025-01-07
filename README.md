![Logo](https://github.com/GwynbleiddN7/Cortana/blob/main/Storage/Assets/cortana.jpg)

# Cortana

**Halo** inspired **Home Assistant** and *Artificial Intelligence*

My personal assistant in daily routines, such as **domotics**, **utility**, **video & music player/download**, **debts/note taker** and more

Currently living on **Raspberry Pi 4** running on **dotnet C#**

***
## Structure

### Core

- **Bootloader**
- **Kernel**
- **CortanaClient**

### Modules

- **Cortana API**
- **Discord Bot**
- **Telegram Bot**

Each module is booted by the **Bootloader** and runs in a thread. Then the **Kernel** waits for any call from the modules.

**CortanaClient** is a standalone program that handles the communication with a Desktop Computer.


The *user* will start an interaction with the **Kernel** through the **Input Interfaces**

### Input Interfaces

- **API HTTP Requests**
- **Discord Bot** 
- **Telegram Bot**

Each **Interface** will be only able to communicate with the **Kernel** to execute *user*'s requests, or handle them on its own if tasks are *interface-specific*


**Cortana** can start an interaction with the *user* through the **Output Interfaces**

### Output Interfaces

- **Client-Server**
- **GPIO**

These **Output interfaces** allow **Cortana** to *communicate* with **PC**, *read sensor data* from **ESP32** and interact with *electronic hardware* in the room [lamp, plugs and more to come]

***

## API Reference

Note: "**cortana-home.net**" is a placeholder for the actual address, which is private.

#### Home 

```http 
  http://cortana-home.net/
```

#### Routing

```http
  GET http://cortana-home.net/api/{route}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `route`      | `string` | Route for ***specific functions*** | **automation**, **raspberry**, **utility**, ***empty***  |

#### Automation

```http
  GET http://cortana-home.net/api/automation/{device}?t={trigger}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `device`      | `string` | **Device** to interact with | **computer**, **lamp**, **power**, **generic**, **room** |
| `trigger`      | `string` | **Action** for the device | **on**, **off**, **toggle** <=> ***empty***  |

```http
  GET http://cortana-home.net/api/automation/status/{device}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `device`      | `string` | **Device** to interact with | **computer**, **lamp**, **power**, **generic**, **room** |

#### Raspberry

```http
  GET http://cortana-home.net/api/raspberry/{action}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `action`      | `string` | **Function** to execute | **temperature**, **location**, **ip**, **gateway**, **shutdown**, **reboot**  |

#### Computer

```http
  GET http://cortana-home.net/api/computer/{action}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `action`      | `string` | **Function** | **notify** (?text=msg), **command** (?text=cmd), **shutdown**, **suspend**, **reboot**, **swap-os** |

---

## Raspberry Configuration

### Dotnet Installation
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel STS

echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.zshrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.zshrc
source ~/.zshrc

dotnet --version
```

### Dependencies
```bash
echo '﻿alias temp='/bin/vcgencmd measure_temp'' >> ~/.zshrc

sudo apt-get install zsh ffmpeg opus-tools libopus0 libopus-dev libsodium-dev
```

### Cortana Client Configuration
```bash
# Linux
echo 'CORTANA_PATH=path_to_cortana' >> /etc/environment
cp cortana /usr/local/bin 
cp cortana-client ~/.config/autostart

# Windows
# Add CORTANA_PATH environment variable through GUI
# Create a shortcut of cortana-client.vbs in autostart folder
# Download notify-send (https://vaskovsky.net/notify-send/) and add it to PATH
```
---

## Run Locally

### Manually Run

```bash
git clone https://github.com/GwynbleiddN7/Cortana.git

cd Cortana
dotnet build
dotnet run --project Bootloader/Bootloader.csproj
```

### Use Script [runs in background]

```bash
git clone https://github.com/GwynbleiddN7/Cortana.git
cd Cortana
chmod +x cortana && ./cortana --start

(run 'cortana-run --help' for more commands)
```
---
<b>Note</b>: this repo contains the source code of the project, and it's missing every <b>configuration file</b>, <b>api key</b> and <b>token</b> needed for the execution of <b>Cortana</b>.
## License

[GNU GPLv3 ](https://choosealicense.com/licenses/gpl-3.0/)
