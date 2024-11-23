![Logo](https://github.com/GwynbleiddN7/Cortana/blob/main/CortanaKernel/Storage/Assets/cortana.jpg)

# Cortana

**Halo** inspired **Home Assistant** and *Artificial Intelligence*

My personal assistant in daily routines, such as **domotics**, **utility**, **video & music player/download**, **debts/note taker** and more

Currently living on **Raspberry Pi 4** running on **dotnet C#**

***
## Structure

### Modules

- **CortanaKernel**
- **Processor**
- **Cortana API**
- **Discord Bot**
- **Telegram Bot**

Each module is started from the **Kernel** and run in a thread handled by the **Kernel**



The *user* will start an interaction with **Processor**'s functions through the **Input Interfaces**

### Input Interfaces

- **API HTTP Requests**
- **Discord Bot** 
- **Telegram Bot**

Each **Interface** will be only able to communicate with the **Processor** to execute *user*'s requests, or handle them on its own if tasks are *interface-specific*


**Cortana** can start an interaction with the *user* through the **Output Interfaces**

### Output Interfaces

- **SSH to PC**
- **GPIO**

These **Output interfaces** allow **Cortana** to *execute scripts* on **PC** and interact with *electronic hardware* in the room [lamp, plugs and more to come]

***

## API Reference

Note: "**cortana-api**" is a placeholder for the actual address, which is private.

#### Home 

```http 
  http://cortana-api/
```

#### Routing

```http
  GET cortana-api/api/{route}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `route`      | `string` | Route for ***specific functions*** | **automation**, **raspberry**, **utility**, ***empty***  |

#### Automation

```http
  GET cortana-api/api/automation/{device}?t={trigger}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `device`      | `string` | **Device** to interact with | **computer**, **lamp**, **outlets**, **general**, **room** |
| `trigger`      | `string` | **Action** for the device | **on**, **off**, **toggle** <=> ***empty***  |

#### Raspberry

```http
  GET cortana-api/api/raspberry/{action}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `action`      | `string` | **Function** to execute | **temperature**, **location**, **ip**, **gateway**, **shutdown**, **reboot**  |

#### Utility

```http
  GET cortana-api/api/utility/{action}
```

| Parameter | Type     | Description                       |  Values                       |
| :-------- | :------- | :-------------------------------- | :-------------------------------- |
| `action`      | `string` | **Function** to execute | **notify** (?text=message) |

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
echo '﻿alias temp='/bin/vcgencmd measure_temp'' >> ~/.bashrc

sudo apt-get install zsh ffmpeg opus-tools libopus0 libopus-dev libsodium-dev
```
---

## Run Locally

### Manually Run

```bash
git clone https://github.com/GwynbleiddN7/Cortana.git

cd Cortana
dotnet build

cd CortanaKernel
dotnet run
```

### Use Script [runs in background]

```bash
git clone https://github.com/GwynbleiddN7/Cortana.git
cd cortana-run
./cortana-run 

(run 'cortana-run --help' for more commands)
```
---

## License

[GNU AGPLv3 ](https://choosealicense.com/licenses/agpl-3.0/)
