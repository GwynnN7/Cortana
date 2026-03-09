using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CortanaTelegram.Utility;

// Chat and Message data structures

internal class TelegramChatArg(ETelegramChatArg type, CallbackQuery callbackQuery, Message interactionMessage)
{
	public readonly CallbackQuery CallbackQuery = callbackQuery;
	public readonly Message InteractionMessage = interactionMessage;
	public readonly ETelegramChatArg Type = type;
}
internal class TelegramChatArg<T>(ETelegramChatArg type, CallbackQuery callbackQuery, Message interactionMessage, T arg) : TelegramChatArg(type, callbackQuery, interactionMessage)
{
	public readonly T Arg = arg;
}

internal struct MessageStats
{
	public required Message Message { get; init; }
	public required string FullMessage { get; set; }
	public required string Command { get; set; }
	public required string Text { get; set; }
	public required List<string> TextList { get; set; }
	public required long ChatId { get; init; }
	public required long UserId { get; init; }
	public required int MessageId { get; init; }
	public required ChatType ChatType { get; init; }
}

// Config Data Structure

internal readonly struct DataStruct
{
	public Dictionary<long, string> Usernames { get; init; }
	public List<long> RootPermissions { get; init; }
}

// Hardware data structure

internal readonly struct HardwareEmoji
{
	public const string Lamp = "\ud83d\udca1";
	public const string Generic = "\ud83d\udd0c";
	public const string Pc = "\u26a1\ufe0f";
	public const string Reboot = "\u2699\ufe0f";
	public const string System = "\ud83c\udfae";
	public const string On = "\ud83d\udd0b";
	public const string Off = "\ud83e\udeab";
	public const string Night = "\ud83c\udf19";
}
