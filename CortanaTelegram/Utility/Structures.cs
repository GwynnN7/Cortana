using CortanaLib;
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
	public Message Message;
	public string FullMessage;
	public string Command;
	public string Text;
	public List<string> TextList;
	public long ChatId;
	public long UserId;
	public int MessageId;
	public ChatType ChatType;
}

// Config Data Structure

internal readonly struct DataStruct //: DeserializedObject
{
	public Dictionary<long, string> Usernames { get; init; }
	public Dictionary<long, string> Groups { get; init; }
	public List<long> RootPermissions { get; init; }
	public List<long> DebtChats { get; init; }
	public List<long> DebtUsers { get; init; }
}

// Hardware data structure

internal readonly struct HardwareEmoji
{
	public const string Lamp = "\ud83d\udca1";
	public const string Generic = "\ud83d\udd0c";
	public const string Pc = "\u26a1\ufe0f";
	public const string Reboot = "\u2699\ufe0f";
	public const string SwapOs = "\ud83c\udfae";
	public const string On = "\ud83d\udd0b";
	public const string Off = "\ud83e\udeab";
	public const string Night = "\ud83c\udf19";
}

// Debts data structures

internal class Debts : Dictionary<long, List<Debt>> //: SerializedObject
{
	public static Debts Load(string path)
	{
		return FileHandler.DeserializeJson<Debts>(path) ?? new Debts();
	}
}

internal class Debt //: SerializedObject
{
	public double Amount { get; set; }
	public long Towards { get; init; }
}

internal class CurrentPurchase
{
	public readonly Stack<SubPurchase> History = new();
	public readonly List<int> MessagesToDelete = [];
	public readonly Dictionary<long, double> Purchases = new();
	public long Buyer;
}

internal class SubPurchase
{
	public List<long> Customers = [];
	public double TotalAmount;
}

