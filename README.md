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
  - Blazor website to interact online with **Cortana**

### Modules

- **Cortana Desktop**
  - Computer software to handle PC through **Cortana** with **Client-Server** communication
- **Cortana Embedded**
  - Collection of scripts running on embedded devices with **Client-Server** or **REST API** communications

Each **Subfunction** runs as a **systemd user service** managed by the **Bootloader**. The **Kernel** service auto-starts all sub-services on boot and cascades stop/restart to them. They communicate with the **Kernel** through **Cortana API** and **Redis IPC**

Each **Module** is a standalone software that runs on a different device and communicates with **Cortana's Kernel** through **Cortana API** or **Hardware API**

---

## Authentication

API requests can optionally be protected with an API key. Set `CORTANA_API_KEY` in `.env` to enable it.

| Header      | Value                        |
| :---------- | :--------------------------- |
| `X-Api-Key` | Your `CORTANA_API_KEY` value |

If the key is not set, the API is fully open. OpenAPI docs (`/scalar`) are accessible without authentication.

---

## API Reference

Note: "**cortana.net**" is a placeholder for the actual address, which is private. API docs are available at `http://cortana.net/scalar`.

#### Home

```http
  GET http://cortana.net/
```

#### Routing

```http
  GET http://cortana.net/{route}/
```

| Parameter | Type     | Description                        | Values                                                                                |
| :-------- | :------- | :--------------------------------- | :------------------------------------------------------------------------------------ |
| `route`   | `string` | Route for **_specific functions_** | **Devices**, **Raspberry**, **Sensors**, **Computer**, **SubFunctions**, **Settings** |

#### Devices

```http
  GET http://cortana.net/Devices/{device}
```

| Parameter | Type     | Description                     | Values                                         |
| :-------- | :------- | :------------------------------ | :--------------------------------------------- |
| `device`  | `string` | **Device** to get the status of | **Computer**, **Lamp**, **Power**, **Generic** |

```http
  POST http://cortana.net/Devices/{device}
```

| Parameter           | Type     | Description                 | Values                                                   |
| :------------------ | :------- | :-------------------------- | :------------------------------------------------------- |
| `device`            | `string` | **Device** to interact with | **Computer**, **Lamp**, **Power**, **Generic**, **room** |
| `PostAction.action` | `string` | **Action** for the device   | **On**, **Off**, **Toggle**                              |

```http
  POST http://cortana.net/Devices/sleep
```

#### Raspberry

```http
  GET http://cortana.net/Raspberry/{info}
```

| Parameter | Type     | Description          | Values                                             |
| :-------- | :------- | :------------------- | :------------------------------------------------- |
| `info`    | `string` | **Info** to retrieve | **Temperature**, **Location**, **Ip**, **Gateway** |

```http
  POST http://cortana.net/Raspberry/
```

| Parameter             | Type     | Description            | Values                                |
| :-------------------- | :------- | :--------------------- | :------------------------------------ |
| `PostCommand.command` | `string` | **Command** to execute | **Shutdown**, **Reboot**, **Command** |

#### Computer

```http
  POST http://cortana.net/Computer/
```

| Parameter             | Type     | Description                | Values                                                                     |
| :-------------------- | :------- | :------------------------- | :------------------------------------------------------------------------- |
| `PostCommand.command` | `string` | **Command** to execute     | **Shutdown**, **Suspend**, **Reboot**, **System**, **Command**, **Notify** |
| `PostCommand.args`    | `string` | Optional text **argument** |                                                                            |

#### Sensors

```http
  GET http://cortana.net/Sensors/{sensor}
```

| Parameter | Type     | Description               | Values                                 |
| :-------- | :------- | :------------------------ | :------------------------------------- |
| `sensor`  | `string` | **Sensor** to get data of | **Motion**, **Temperature**, **Light** |

#### Subfunctions

```http
  GET http://cortana.net/SubFunctions/{subfunction}
```

| Parameter     | Type     | Description                      | Values                                                                     |
| :------------ | :------- | :------------------------------- | :------------------------------------------------------------------------- |
| `subfunction` | `string` | **Subfunction** to get status of | **CortanaKernel**, **CortanaTelegram**, **CortanaDiscord**, **CortanaWeb** |

```http
  POST http://cortana.net/SubFunctions/{subfunction}
```

| Parameter           | Type     | Description                      | Values                                                                     |
| :------------------ | :------- | :------------------------------- | :------------------------------------------------------------------------- |
| `subfunction`       | `string` | **Subfunction** to interact with | **CortanaKernel**, **CortanaTelegram**, **CortanaDiscord**, **CortanaWeb** |
| `PostAction.action` | `string` | **Action** to execute            | **Start**, **Restart**, **Update**, **Stop**                               |

```http
  POST http://cortana.net/SubFunctions/
```

| Parameter             | Type     | Description                    | Values                    |
| :-------------------- | :------- | :----------------------------- | :------------------------ |
| `PostCommand.command` | `string` | **Type of Message** to publish | **Telegram**, **Discord** |
| `PostCommand.args`    | `string` | **Message Text** to publish    |                           |

#### Settings

```http
  GET http://cortana.net/Settings/{setting}
```

| Parameter | Type     | Description         | Values                                                                                     |
| :-------- | :------- | :------------------ | :----------------------------------------------------------------------------------------- |
| `setting` | `string` | **Settings** to get | **LightThreshold**, **AutomaticMode**, **MorningHour**, **MotionOffMax**, **MotionOffMin** |

```http
  POST http://cortana.net/Settings/{setting}
```

| Parameter         | Type     | Description         | Values                                                                                     |
| :---------------- | :------- | :------------------ | :----------------------------------------------------------------------------------------- |
| `setting`         | `string` | **Settings** to set | **LightThreshold**, **AutomaticMode**, **MorningHour**, **MotionOffMax**, **MotionOffMin** |
| `PostValue.value` | `number` | **Value** to update |                                                                                            |

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
