using System.ComponentModel;

public enum ESubFunctions
{
    CortanaAPI,
    DiscordBot,
    TelegramBot,
}

public enum ETimerLocation
{
    Global,
    CortanaAPI,
    DiscordBot,
    TelegramBot,
    Utility
}

public enum EHardwareElements
{
    LED,
    OLED,
    PC,
    Lamp,
    Outlets
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

public enum EActionExpanded
{
    Crea,
    Elimina,
    Modifica
}

public enum EAudioSource
{
    Youtube,
    Local
}

public enum ETimerLoop
{
    [Description("Niente loop")]
    No,
    [Description("In base all'intervallo messo")]
    Intervallo,
    [Description("Ogni giorno alla stessa ora")]
    Quotidiano,
    [Description("Ogni settimana allo stesso giorno")]
    Settimanalmente
}

public enum EWeek
{
    Today,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday
}