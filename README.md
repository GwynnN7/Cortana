![Logo](CortanaWeb/wwwroot/favicon.png)

# Cortana

**Halo** inspired **Home Assistant** and *Artificial Intelligence*

My personal assistant in daily routines, integrated with **sensors**, **devices**, **applications** and **internet**

Currently living on **Raspberry Pi 4** running mostly on **C# .NET and ASP.NET CORE**

***
## Structure

### Kernel
- **Bootloader**
  - Builds, boots and stops **subfunctions** and check their status
- **Hardware API**
  - Gives an interface to the **Kernel** to interact with hardware devices and *GPIO* in the room
- **Cortana API**
  - Gives an interface through **REST-API** to interact with **Cortana**'s functions

### Subfunctions
- **Cortana Telegram**
  - Telegram bot to integrate **Cortana** with *Telegram*
- **Cortana Discord**
  - Discord bot to integrate **Cortana** with *Discord*
- **Cortana Web**
  - Blazor website to interact online with **Cortana**

### Modules

- **Cortana Desktop**
  - Computer software to handle PC through **Cortana** with **Client-Server** communication
- **Cortana Embedded**
  - Collection of scripts running on embedded devices with **Client-Server**  or **REST API** communications

Each **Subfunction** is handled by the **Bootloader**. It's built and then **started/killed** as a **UNIX** process. Then, they communicate with the **Kernel** through **Cortana API**

Each **Module** is a standalone software that runs on a different device and communicates with **Cortana's Kernel** through **Cortana API** or **Hardware API**

***

## API Reference

Note: "**cortana.net**" is a placeholder for the actual address, which is private.

#### Home 

```http 
  http://cortana.net/
```

#### Routing

```http
  GET http://cortana.net/api/{route}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `route`      | `string` | Route for ***specific functions*** | **devices**, **raspberry**, **sensors**, **computer**, **subfunctions**, **settings**, ***empty***  |

#### Devices

```http
  GET http://cortana.net/api/devices/{device}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `device`      | `string` | **Device** to get the status of | **computer**, **lamp**, **power**, **generic**, **room** |

```http
  POST http://cortana.net/api/device/{device}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `device`      | `string` | **Device** to interact with | **computer**, **lamp**, **power**, **generic**, **room** |
| `PostAction.action`      | `string` | **Action** for the device | **on**, **off**, **toggle**   |


```http
  POST http://cortana.net/api/devices/{command}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `command`      | `string` | **Command** related to devices  | **sleep** |

#### Raspberry

```http
  GET http://cortana.net/api/raspberry/{info}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `info`      | `string` | **Info** to retrieve | **temperature**, **location**, **ip**, **gateway**|

```http
  POST http://cortana.net/api/raspberry/
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `PostCommand.command`      | `string` | **Command** to execute |  **shutdown**, **reboot** |

#### Computer

```http
  POST http://cortana.net/api/computer/
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `PostCommand.command`      | `string` | **Command** to execute | **shutdown**, **suspend**, **reboot**, **system**, **command**, **notify**|
| `PostCommand.args`      | `string` | Optional text **argument** |  |

#### Sensors

```http
  GET http://cortana.net/api/sensors/{sensor}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `sensor`      | `string` | **Sensor** to get data of | **motion**, **temperature**, **light** |

#### Subfunctions

```http
  GET http://cortana.net/api/subfunctions/{subfunction}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `subfunction`      | `string` | **Subfunction** to get status of | **CortanaKernel**, **CortanaTelegram**, **CortanaDiscord**, **CortanaWeb** |

```http
  POST http://cortana.net/api/subfunctions/{subfunction}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `subfunction`      | `string` | **Subfunction** to get status of | **CortanaKernel**, **CortanaTelegram**, **CortanaDiscord**, **CortanaWeb** |
| `PostAction.action`      | `string` | **Action** to execute | **restart**, **reboot**, **update**, **stop** |

```http
  POST http://cortana.net/api/subfunctions/
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `PostAction.action`      | `string` | **Type of Message** to publish | **urgent**, **update** |
| `PostAction.args`      | `string` | **Message Text** to publish |  |

#### Settings

```http
  GET http://cortana.net/api/settings/{setting}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `setting`      | `string` | **Settings** to get | **LightThreshold**, **LimitMode**, **ControlMode**, **MorningHour**, **MotionOffMax**, **MotionOffMin** |

```http
  POST http://cortana.net/api/settings/{setting}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `setting`      | `string` | **Settings** to set | **LightThreshold**, **LimitMode**, **ControlMode**, **MorningHour**, **MotionOffMax**, **MotionOffMin** |
| `PostValue.value`   | `number` | **Value** to update |  |

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
### ESP32
```bash
sudo curl -fsSL https://raw.githubusercontent.com/arduino/arduino-cli/master/install.sh | BINDIR=~/.local/bin sh
arduino-cli config init --additional-urls https://espressif.github.io/arduino-esp32/package_esp32_index.json
arduino-cli core update-index
arduino-cli core install esp32:esp32
arduino-cli lib install OneWire DallasTemperature WiFi

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

### Use Script

```bash
git clone https://github.com/GwynnN7/Cortana.git
cd Cortana/CortanaKernel/Scripts
chmod +x cortana && ./cortana boot

(run 'cortana help' for more commands)
```
---
## CortanaDesktop Configuration
### Linux
```bash
sudo pacman -S dotnet-sdk
sudo echo 'CORTANA_PATH=path_to_cortana' >> /etc/environment
sudo echo 'CORTANA_API=cortana_api' >> /etc/environment
cp Scripts/cortana /usr/local/bin
cp Scripts/cortana-desktop ~/.config/autostart
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
