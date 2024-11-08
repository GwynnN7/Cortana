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
    Lamp,
    Outlets,
    General
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


// TIMER RELATED
public enum ETimerLocation
{
    Global,
    CortanaApi,
    DiscordBot,
    TelegramBot,
    Utility
}
public enum ETimerLoop
{
    No,
    Interval,
    Daily,
    Weekly
}


// DISCORD RELATED
public enum EAnswer
{
    Si,
    No
}
public enum EAudioSource
{
    Youtube,
    Local
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