![Logo](CortanaWeb/wwwroot/favicon.png)

# Cortana

**Halo** inspired **Home Assistant** and *Artificial Intelligence*

My personal assistant in daily routines, such as **domotics**, **utility**, **video & music player/download**, **debts/note taker** and more

Currently living on **Raspberry Pi 4** running on **dotnet C#**

***
## Structure

### Cortana Kernel

- **Bootloader**
- **Hardware API** ~ Client/Server
- **Cortana API** ~ REST API

### Subfunctions

- **Cortana Telegram**
- **Cortana Discord**
- **Cortana Web**

### Modules

- **Cortana Desktop**
- **Cortana Embedded**

Each subfunction is built and started by the **Bootloader**. Then they communicate with the **Kernel** through **Cortana API**. Each **Module** communicate with the **Kernel** through **Cortana API** or **Hardware API**

**CortanaClient** is a standalone program that handles the communication with a Desktop Computer.


The *user* will start an interaction with the **Kernel** through the **Input Interfaces**

### Input Interfaces

- **REST API**
- **Discord Bot** 
- **Telegram Bot**

Each **Interface** will be only able to communicate with the **Kernel** to execute *user*'s requests, or handle them on its own if tasks are *interface-specific*


**Cortana** can start an interaction with the *user* through the **Output Interfaces**

### Output Interfaces

- **Client-Server**
- **GPIO**

These **Output interfaces** allow **Cortana** to *communicate* with **PC**, *read sensor data* from **ESP32** and interact with *electronic hardware* in the room

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

#### Device

```http
  GET http://cortana-home.net/api/device/{device}?t={trigger}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `device`      | `string` | **Device** to interact with | **computer**, **lamp**, **power**, **generic**, **room** |
| `trigger`      | `string` | **Action** for the device | **on**, **off**, **toggle** <=> ***empty***  |

```http
  GET http://cortana-home.net/api/device/{device}/status
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `device`      | `string` | **Device** to interact with | **computer**, **lamp**, **power**, **generic**, **room** |

```http
  GET http://cortana-home.net/api/device/{command}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `command`      | `string` | **Command** related to devices  | **sleep** |

#### Raspberry

```http
  GET http://cortana-home.net/api/raspberry/{action}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `action`      | `string` | **Function** to execute | **temperature**, **location**, **ip**, **gateway**, **shutdown**, **reboot** **update** |

#### Computer

```http
  GET http://cortana-home.net/api/computer/{action}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `action`      | `string` | **Function** | **notify** (?txt=msg), **command** (?txt=cmd), **shutdown**, **suspend**, **reboot**, **swap-os** |

#### Sensor

```http
  GET http://cortana-home.net/api/sensor/{sensor}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `sensor`      | `string` | **Sensor** to interact with | **motion** **temperature** **light** |

```http
  GET http://cortana-home.net/api/sensor/{option}?val={value}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `option`      | `string` | **Option** to interact with | **mode** **threshold** |
| `value`      | `number` | **Value** to update (*optional*) | **mode: 1/2/3** **threshold: 0 ~ 4096** |

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
# Dependencies
sudo apt install zsh ffmpeg opus-tools libopus0 libopus-dev libsodium-dev git

# Environment
echo '﻿alias temp='/bin/vcgencmd measure_temp'' >> ~/.zshrc
echo 'export PATH=$PATH:/home/cortana/.local/bin' >> ~/.zshrc

# Arduino ~ ESP32
sudo curl -fsSL https://raw.githubusercontent.com/arduino/arduino-cli/master/install.sh | BINDIR=~/.local/bin sh
arduino-cli config init --additional-urls https://espressif.github.io/arduino-esp32/package_esp32_index.json
arduino-cli core update-index
arduino-cli core install esp32:esp32
arduino-cli lib install OneWire DallasTemperature WiFi
```

### Cortana Client Configuration
```bash
# Linux
sudo pacman -S dotnet-sdk
echo 'CORTANA_PATH=path_to_cortana' >> /etc/environment
cp cortana /usr/local/bin 
cp cortana-client ~/.config/autostart

# Windows
# Install dotnet-sdk
# Add CORTANA_PATH environment variable through GUI
# Create a shortcut of cortana-client.vbs in autostart folder
# Download notify-send (https://vaskovsky.net/notify-send/) and add it to PATH
```
---

## Run Locally

### Manually Run

```bash
git clone https://github.com/GwynnN7/Cortana.git

cd Cortana
dotnet build
dotnet run --project Bootloader/Bootloader.csproj
```

### Use Script [runs in background]

```bash
git clone https://github.com/GwynnN7/Cortana.git
cd Cortana
chmod +x cortana && ./cortana --start

(run 'cortana --help' for more commands)
```
---
<b>Note</b>: this repo contains the source code of the project, and it's missing every <b>configuration file</b>, <b>api key</b> and <b>token</b> needed for the execution of <b>Cortana</b>.
## License

[GNU GPLv3](https://choosealicense.com/licenses/gpl-3.0/)
