using Kernel.Software.DataStructures;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Utility;

namespace TelegramBot.Modules;

internal abstract class ShoppingModule : IModuleInterface
{
	private static readonly string SerializePath = Path.Combine(TelegramUtils.StoragePath, "Debts.json");
	
	private static CurrentPurchase? _currentPurchase;
	private static readonly Debts Debts;
	
	private static readonly List<long> DebtUsers;
	private static readonly List<long> DebtChats;

	static ShoppingModule()
	{
		DebtUsers = TelegramUtils.Data.DebtUsers;
		DebtChats = TelegramUtils.Data.DebtChats;
		Debts = Debts.Load(SerializePath);
	}

	private static void UpdateDebts() => Debts.Serialize(SerializePath);

	public static async Task ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
	{
		switch (messageStats.Command)
		{
			case "purchase":
				if (IsChannelAllowed(messageStats.ChatId))
				{
					if (_currentPurchase != null)
					{
						await cortana.SendMessage(messageStats.ChatId, "Complete the current active purchase first!");
						return;
					}

					await CreateMenu(cortana, messageStats.Message);
				}
				else
				{
					await cortana.SendMessage(messageStats.ChatId, "Comando riservato a specifici gruppi");
				}

				break;
		}
	}

	public static async Task CreateMenu(ITelegramBotClient cortana, Message message)
	{
		await cortana.SendMessage(message.Chat.Id, "Start a new purchase or check your current debts", replyMarkup: CreateButtons());
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
	{
		await cortana.AnswerCallbackQuery(callbackQuery.Id);

		Message message = callbackQuery.Message!;
		switch (command)
		{
			case "new-purchase":
				if (_currentPurchase != null)
				{
					await cortana.SendMessage(message.Chat.Id, "Complete the current active purchase first!");
					return;
				}

				_currentPurchase = new CurrentPurchase { Buyer = callbackQuery.From.Id };
				foreach (long userId in DebtUsers) _currentPurchase.Purchases.Add(userId, 0.0);
				await cortana.SendMessage(message.Chat.Id, UpdateCurrentPurchaseMessage(), replyMarkup: CreateOrderButtons());
				break;
			case "show-debts":
				await cortana.SendMessage(message.Chat.Id, GetDebts());
				break;
			default:
				await HandlePurchase(cortana, callbackQuery, command);
				break;
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats)
	{
		if (_currentPurchase == null) return;

		if (_currentPurchase.Buyer != messageStats.UserId)
		{
			await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
			return;
		}

		_currentPurchase.MessagesToDelete.Add(messageStats.MessageId);

		var reaction = new ReactionTypeEmoji { Emoji = "👍" };
		var failedReaction = new ReactionTypeEmoji { Emoji = "👎" };

		double newAmount = 0;
		foreach (string amount in messageStats.FullMessage.Split())
			try
			{
				newAmount += double.Parse(amount);
			}
			catch
			{
				await cortana.SetMessageReaction(messageStats.ChatId, messageStats.MessageId, [failedReaction]);
				return;
			}

		SubPurchase subPurchase = _currentPurchase.History.Peek();
		subPurchase.TotalAmount += newAmount;

		await cortana.SetMessageReaction(messageStats.ChatId, messageStats.MessageId, [reaction]);
	}

	private static string GetDebts()
	{
		var debts = "";
		foreach ((long userId, List<Debt> debtsList) in Debts)
		{
			if (debtsList.Count == 0) continue;
			debts += $"{TelegramUtils.IdToName(userId)} owes:\n";
			debts = debtsList.Aggregate(debts, (current, debt) => current + $"    - {Math.Round(debt.Amount, 2)}\u20ac to {TelegramUtils.IdToName(debt.Towards)}\n");
		}

		if (debts == "") debts = "No debts are owned";
		return debts;
	}

	private static async Task HandlePurchase(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
	{
		Message message = callbackQuery.Message!;
		int messageId = message.MessageId;

		if (_currentPurchase == null)
		{
			await cortana.DeleteMessage(message.Chat.Id, messageId);
			return;
		}

		switch (command)
		{
			case "add":
				var subPurchase = new SubPurchase
				{
					Customers = DebtUsers.ToList(),
					TotalAmount = 0
				};
				_currentPurchase.History.Push(subPurchase);
				await cortana.EditMessageText(message.Chat.Id, messageId, UpdateBuyersMessage(), replyMarkup: CreateAddCustomerButtons());
				break;
			case "list":
				if (TelegramUtils.TryAddChatArg(message.Chat.Id, new TelegramChatArg(ETelegramChatArg.Shopping, callbackQuery, message), callbackQuery))
					await cortana.EditMessageText(message.Chat.Id, messageId, $"Send me the cost of the products bought by {GetCurrentBuyers()}", replyMarkup: CreateAddItemButtons());
				break;
			case "confirm":
			{
				TelegramUtils.ChatArgs.Remove(message.Chat.Id);

				SubPurchase lastSubPurchase = _currentPurchase.History.Peek();
				double amount = Math.Round(lastSubPurchase.TotalAmount / lastSubPurchase.Customers.Count, 3);
				if (amount != 0)
					foreach (long customer in lastSubPurchase.Customers)
						_currentPurchase.Purchases[customer] = Math.Round(_currentPurchase.Purchases[customer] + amount, 3);
				else _currentPurchase.History.Pop();

				await cortana.EditMessageText(message.Chat.Id, messageId, UpdateCurrentPurchaseMessage(), replyMarkup: CreateOrderButtons());
				break;
			}
			case "pay":
				await cortana.EditMessageText(message.Chat.Id, messageId, UpdateCurrentPurchaseMessage());
				RemoveExistingDebts();
				UpdateDebts();
				if (_currentPurchase.MessagesToDelete.Count > 0) await cortana.DeleteMessages(message.Chat.Id, _currentPurchase.MessagesToDelete);
				_currentPurchase = null;
				break;
			case "undo":
			{
				if (!_currentPurchase.History.TryPop(out SubPurchase? lastSubPurchase)) return;

				double amount = Math.Round(lastSubPurchase.TotalAmount / lastSubPurchase.Customers.Count, 3);
				if (amount == 0) return;

				foreach (long customer in lastSubPurchase.Customers) _currentPurchase.Purchases[customer] = Math.Round(_currentPurchase.Purchases[customer] - amount, 3);

				await cortana.EditMessageText(message.Chat.Id, messageId, UpdateCurrentPurchaseMessage(), replyMarkup: CreateOrderButtons());
				break;
			}
			case "cancel":
				await cortana.DeleteMessage(message.Chat.Id, messageId);

				if (_currentPurchase.MessagesToDelete.Count > 0) await cortana.DeleteMessages(message.Chat.Id, _currentPurchase.MessagesToDelete);
				_currentPurchase = null;
				break;
			case "back":
				TelegramUtils.ChatArgs.Remove(message.Chat.Id);
				await cortana.EditMessageText(message.Chat.Id, messageId, UpdateCurrentPurchaseMessage(), replyMarkup: CreateOrderButtons());
				break;
			default:
			{
				if (!command.StartsWith("user:")) return;
				string user = command["user:".Length..];
				long userId;
				try
				{
					userId = TelegramUtils.NameToId(user);
				}
				catch(CortanaException)
				{
					return;
				}
				SubPurchase lastSubPurchase = _currentPurchase.History.Peek();
				if (!lastSubPurchase.Customers.Remove(userId)) lastSubPurchase.Customers.Add(userId);
				if (lastSubPurchase.Customers.Count == 0) lastSubPurchase.Customers = DebtUsers.ToList();
				await cortana.EditMessageText(message.Chat.Id, messageId, UpdateBuyersMessage(), replyMarkup: CreateAddCustomerButtons());
				break;
			}
		}
	}

	private static void RemoveExistingDebts()
	{
		if (_currentPurchase == null) return;
		long buyerId = _currentPurchase.Buyer;
		if (!Debts.ContainsKey(buyerId)) Debts.Add(buyerId, []);
		foreach (Debt debt in Debts[buyerId].ToList())
		{
			foreach ((long customerId, double amount) in _currentPurchase.Purchases)
			{
				if (customerId == buyerId || debt.Towards != customerId) continue;

				double newDebt = debt.Amount - amount;

				if (newDebt > 0) debt.Amount = newDebt;
				else Debts[buyerId].Remove(debt);

				if (newDebt >= 0) _currentPurchase.Purchases.Remove(customerId);
				else _currentPurchase.Purchases[customerId] = Math.Round(-newDebt, 3);
			}
		}

		foreach ((long userId, double debtAmount) in _currentPurchase.Purchases.Where(newDebt => newDebt.Key != buyerId)) AddReversedDebt(buyerId, userId, debtAmount);
	}

	private static void AddReversedDebt(long buyer, long customer, double amount)
	{
		if (!Debts.ContainsKey(customer)) Debts.Add(customer, []);

		if (!Debts[customer].Exists(x => x.Towards == buyer)) Debts[customer].Add(new Debt { Towards = buyer, Amount = Math.Round(amount, 3)});
		else
			foreach (Debt debt in Debts[customer].Where(debt => debt.Towards == buyer))
				debt.Amount = Math.Round(debt.Amount + amount, 3);
	}

	private static string UpdateCurrentPurchaseMessage()
	{
		if (_currentPurchase == null) return "No purchase active";

		var text = $"Purchase date: {DateTime.Now:dd/MM/yyyy}\n\n";
		double totalPrice = 0;
		foreach (long userId in _currentPurchase.Purchases.Keys)
		{
			text += $"{TelegramUtils.IdToName(userId)}: {Math.Round(_currentPurchase.Purchases[userId], 2)}\u20ac\n";
			totalPrice += _currentPurchase.Purchases[userId];
		}

		text += $"\nTotal: {Math.Round(totalPrice, 2)}\u20ac";

		return text;
	}

	private static string GetCurrentBuyers()
	{
		return _currentPurchase == null ? "" : _currentPurchase.History.Peek().Customers.Aggregate("", (current, buyer) => current + $"{TelegramUtils.IdToName(buyer)} ");
	}

	private static string UpdateBuyersMessage()
	{
		return _currentPurchase == null ? "No purchase active" : $"Customers of the new shopping list:\n{GetCurrentBuyers()}";
	}

	private static bool IsChannelAllowed(long channelId)
	{
		return DebtChats.Contains(channelId);
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("New Purchase", "shopping-new-purchase")
			.AddNewRow()
			.AddButton("Show Debts", "shopping-show-debts");
	}

	private static InlineKeyboardMarkup CreateOrderButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Add", "shopping-add")
			.AddNewRow()
			.AddButton("Pay", "shopping-pay")
			.AddNewRow()
			.AddButton("Undo", "shopping-undo")
			.AddButton("Cancel", "shopping-cancel");
	}

	private static InlineKeyboardMarkup CreateAddCustomerButtons()
	{
		if (_currentPurchase == null || _currentPurchase.History.Count == 0) throw new CortanaException("No purchase active");

		var inlineKeyboard = new InlineKeyboardMarkup();
		foreach (long user in DebtUsers)
		{
			SubPurchase subPurchase = _currentPurchase.History.Peek();
			string sign = subPurchase.Customers.Contains(user) ? "\u2705" : "\u274c";

			string customer = TelegramUtils.IdToName(user);
			inlineKeyboard.AddButton($"{sign} {customer}", $"shopping-user:{customer}");
			inlineKeyboard.AddNewRow();
		}

		inlineKeyboard.AddButton("Next", "shopping-list");
		return inlineKeyboard;
	}

	private static InlineKeyboardMarkup CreateAddItemButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Confirm", "shopping-confirm")
			.AddNewRow()
			.AddButton("Cancel", "shopping-back");
	}
}