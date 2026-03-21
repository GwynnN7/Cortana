using Telegram.Bot.Types;

namespace CortanaTelegram.Utility;

// Chat and Message data structures

internal class ChatArgs(EArgsType type, CallbackQuery query, Message message)
{
	public readonly CallbackQuery Query = query;
	public readonly Message Message = message;
	public readonly EArgsType Type = type;
}
internal class ChatArgs<T>(EArgsType type, CallbackQuery query, Message message, T arg) : ChatArgs(type, query, message)
{
	public readonly T Arg = arg;
}

internal struct MessageData
{
	public required string Message { get; set; }
	public required string Command { get; set; }
	public required int MessageId { get; init; }
	public required int TopicId { get; init; }
}

// Config Data Structure

internal readonly struct DataStruct
{
	public Dictionary<long, string> Usernames { get; init; }
	public long HomeGroup { get; init; }
	public Topics Topics { get; init; }
}

internal readonly struct Topics
{
	public int Home { get; init; }
	public int Devices { get; init; }
	public int Sensors { get; init; }
	public int Raspberry { get; init; }
	public int Cortana { get; init; }
	public int Log { get; init; }
}

// Hardware data structure

internal readonly struct HardwareEmoji
{
	public const string Lamp = "💡";
	public const string Generic = "🔌";
	public const string Pc = "💻";
	public const string Reboot = "🔄";
	public const string System = "🎮";
	public const string On = "🟢";
	public const string Off = "🔴";
	public const string Night = "🌙";
}