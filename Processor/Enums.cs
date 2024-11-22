namespace Processor;

// KERNEL RELATED
public enum ESubFunctions
{
    CortanaApi,
    DiscordBot,
    TelegramBot,
}


// HARDWARE RELATED
public enum EGpio
{
    Computer,
    Power,
    Lamp,
    Generic
}
public enum EStatus
{
    On,
    Off
}
public enum ETrigger
{
    On,
    Off,
    Toggle
}
public enum EPowerOption
{
    Shutdown,
    Reboot
}
public enum EComputerCommand
{
    PowerOn,
    Shutdown,
    Reboot,
    Notify
}
public enum ELocation
{
    Orvieto,
    Pisa
}

public enum EHardwareInfo
{
    Location,
    Ip,
    Gateway,
    Temperature
}

// TIMER RELATED
public enum ETimerType
{
    Utility,
    Discord,
    Telegram
}
public enum ETimerLoop
{
    No,
    Interval,
    Daily,
    Weekly
}


// TELEGRAM RELATED

public enum ETelegramChatArg
{
    Qrcode, 
    Chat, 
    Notification,
    Ping,
    Shopping,
    AudioDownloader,
    VideoDownloader
}


// DISCORD RELATED
public enum EAnswer
{
    Si,
    No
}
public enum EListAction
{
    Crea,
    Elimina
}
public enum ECortanaChannels
{
    Cortana,
    Log
}
public enum EMemeCategory
{
    Games,
    MoreGames,
    Anime,
    Music,
    Sfx,
    Short,
    NotLong,
    Long,
    NotShort,
    Skidaddle,
    Default
}