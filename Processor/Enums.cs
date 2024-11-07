namespace Processor;

public enum ESubFunctions
{
    CortanaApi,
    DiscordBot,
    TelegramBot,
}

public enum ETimerLocation
{
    Global,
    CortanaApi,
    DiscordBot,
    TelegramBot,
    Utility
}

public enum EHardwareElements
{
    Computer,
    Lamp,
    Outlets,
    General
}

public enum EBooleanState
{
    On,
    Off
}

public enum EHardwareTrigger
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

public enum EAnswer
{
    Si,
    No
}

public enum EAction
{
    Crea,
    Elimina
}

public enum EAudioSource
{
    Youtube,
    Local
}

public enum ELocation
{
    Orvieto,
    Pisa
}

public enum ECortanaChannels
{
    Cortana,
    Log
}

public enum ETimerLoop
{
    No,
    Interval,
    Daily,
    Weekly
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