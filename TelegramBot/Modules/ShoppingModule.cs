using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Processor;

namespace TelegramBot.Modules
{
    internal enum EPurchaseSteps { Buyers, Amount }
    public static class ShoppingModule
    {
        private static readonly List<string> AllowedChannels = ["ProcioneSpazialeMistico"];

        private static readonly List<long> Users =
        [
            TelegramUtils.NameToId("@gwynn7"),
            TelegramUtils.NameToId("@Vasile76"),
            TelegramUtils.NameToId("@moostacho")
        ];
        
        private static CurrentPurchase? _currentPurchase;
        private static readonly Dictionary<long, List<Debts>> Debts;
        private static readonly Dictionary<long, EPurchaseSteps> ChannelWaitingForText;

        static ShoppingModule()
        {
            Debts = Software.LoadFile<Dictionary<long, List<Debts>>>("Storage/Config/Telegram/Debts.json") ?? new Dictionary<long, List<Debts>>();
            ChannelWaitingForText = new Dictionary<long, EPurchaseSteps>();
        }
        
        private static void UpdateDebts()
        {
            Software.WriteFile("Storage/Config/Telegram/Debts.json", Debts);
        }
        
         public static async void ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
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
                        await cortana.SendMessage(messageStats.ChatId, "Start a new purchase or check your current debts", replyMarkup: CreatePurchaseButtons());
                    }
                    else await cortana.SendMessage(messageStats.ChatId, "Comando riservato a specifici gruppi");
                    break;
            }
        }

        private static string GetDebts()
        {
            var debts = "Debts\n\n";
            foreach ((long userId, List<Debts> debtsList) in Debts)
            {
                debts = debtsList.Aggregate(debts, (current, debt) => current + $"{TelegramUtils.IdToName(userId)} owns {debt.Amount}\u20ac to {TelegramUtils.IdToName(debt.Towards)}\n");
                debts += "\n";
            }
            return debts;
        }
        
        public static async void ButtonCallback(ITelegramBotClient cortana, Update update)
        {
            if(update.CallbackQuery == null) return;
            
            string data = update.CallbackQuery.Data!;
            int messageId = update.CallbackQuery.Message!.MessageId;
            
            if(!data.StartsWith("shopping-")) return;
            data = data["shopping-".Length..];

            await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);

            switch (data)
            {
                case "new-purchase":
                    if (_currentPurchase != null)
                    {
                        await cortana.SendMessage(update.CallbackQuery.Message.Chat.Id, "Complete the current active purchase first!");
                        return;
                    }

                    _currentPurchase = new CurrentPurchase
                    {
                        Buyer = update.CallbackQuery.From.Id
                    };
                    foreach (long userId in Users) _currentPurchase.Purchases.Add(userId, 0.0);
                    Message msg = await cortana.SendMessage(update.CallbackQuery.Message.Chat.Id, UpdateCurrentPurchaseMessage(), replyMarkup: CreateOrderButtons());
                    _currentPurchase.MessageId = msg.MessageId;
                    break;
                case "show-debts":
                    await cortana.SendMessage(update.CallbackQuery.Message.Chat.Id, GetDebts());
                    break;
                case "add":
                    if (_currentPurchase == null)
                    {
                        await cortana.DeleteMessage(update.CallbackQuery.Message.Chat.Id, messageId);
                        return;
                    }
                    var subPurchase = new SubPurchase()
                    {
                        Customers = Users,
                        TotalAmount = 0
                    };
                    _currentPurchase.History.Push(subPurchase);
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, UpdateBuyersMessage(), replyMarkup: CreateAddItemButtons("shopping-confirm-customers"));
                    ChannelWaitingForText.Add(update.CallbackQuery.Message.Chat.Id, EPurchaseSteps.Buyers);
                    break;
                case "confirm-customers":
                    ChannelWaitingForText.Remove(update.CallbackQuery.Message.Chat.Id);
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, $"List the price of every item for {GetCurrentBuyers()}",  replyMarkup: CreateAddItemButtons("shopping-confirm-money"));
                    ChannelWaitingForText.Add(update.CallbackQuery.Message.Chat.Id, EPurchaseSteps.Amount);
                    break;
                case "confirm-money":
                {
                    ChannelWaitingForText.Remove(update.CallbackQuery.Message.Chat.Id);
                    if (_currentPurchase == null)
                    {
                        await cortana.DeleteMessage(update.CallbackQuery.Message.Chat.Id, messageId);
                        return;
                    }
                    SubPurchase lastSubPurchase = _currentPurchase.History.Peek();
                    await cortana.DeleteMessages(update.CallbackQuery.Message.Chat.Id, lastSubPurchase.MessagesToDelete);
                    lastSubPurchase.MessagesToDelete.Clear();
                    
                    double amount = Math.Round(lastSubPurchase.TotalAmount / lastSubPurchase.Customers.Count, 2);
                    if (amount > 0) foreach (long customer in lastSubPurchase.Customers) _currentPurchase.Purchases[customer] += amount;
                    else _currentPurchase.History.Pop();
                    
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, UpdateCurrentPurchaseMessage(), replyMarkup: CreateOrderButtons());
                    break;
                }
                case "pay":
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, UpdateCurrentPurchaseMessage());
                    RemoveExistingDebts();
                    UpdateDebts();
                    _currentPurchase = null;
                    break;
                case "undo":
                {
                    if (_currentPurchase == null)
                    {
                        await cortana.DeleteMessage(update.CallbackQuery.Message.Chat.Id, messageId);
                        return;
                    }

                    if (!_currentPurchase.History.TryPop(out SubPurchase? lastSubPurchase)) return;
                    
                    double amount = Math.Round(lastSubPurchase.TotalAmount / lastSubPurchase.Customers.Count, 2);
                    if (amount == 0) return;
                    
                    foreach (long customer in lastSubPurchase.Customers) _currentPurchase.Purchases[customer] -= amount;

                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, UpdateCurrentPurchaseMessage(), replyMarkup: CreateOrderButtons());
                    break;
                }
                case "cancel":
                    await cortana.DeleteMessage(update.CallbackQuery.Message.Chat.Id, messageId);
                    _currentPurchase = null;
                    break;
                case "back":
                    ChannelWaitingForText.Remove(update.CallbackQuery.Message.Chat.Id);
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, UpdateCurrentPurchaseMessage(), replyMarkup: CreateOrderButtons());
                    break;
            }
        }
        
        public static async void HandleCallback(MessageStats messageStats, ITelegramBotClient cortana)
        {
            if(_currentPurchase == null) return;
            if(!IsWaiting(messageStats.ChatId)) return;

            if (_currentPurchase.Buyer != messageStats.UserId)
            {
                await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                return;
            }

            SubPurchase subPurchase = _currentPurchase.History.Peek();
            subPurchase.MessagesToDelete.Add(messageStats.MessageId);
            
            var reaction = new ReactionTypeEmoji { Emoji = "👍" };
            var failedReaction = new ReactionTypeEmoji { Emoji = "\u274c" };
            
            switch (ChannelWaitingForText[messageStats.ChatId])
            {
                case EPurchaseSteps.Amount:
                    double newAmount = 0;
                    foreach (string amount in messageStats.FullMessage.Split())
                    {
                        try {
                            newAmount += double.Parse(amount);
                        }
                        catch {
                            await cortana.SetMessageReaction(messageStats.ChatId, messageStats.MessageId, [failedReaction]);
                            return;
                        }
                    }
                    subPurchase.TotalAmount += newAmount;
                    break;
                case EPurchaseSteps.Buyers:
                    var newCustomers = new List<long>();
                    foreach (string user in messageStats.FullMessage.Split())
                    {
                        try {
                            newCustomers.Add(TelegramUtils.NameToId(user));
                        }
                        catch {
                            await cortana.SetMessageReaction(messageStats.ChatId, messageStats.MessageId, [failedReaction]);
                            return;
                        }
                    }
                    if(subPurchase.Customers.OrderBy(x => x).SequenceEqual(newCustomers.OrderBy(x => x))) break;
                    subPurchase.Customers.Clear();
                    subPurchase.Customers.AddRange(newCustomers);
                    await cortana.EditMessageText(messageStats.ChatId, _currentPurchase.MessageId, UpdateBuyersMessage(), replyMarkup: CreateAddItemButtons("shopping-confirm-customers"));
                    break;
                default:
                    reaction = failedReaction;
                    break;
            }
            await cortana.SetMessageReaction(messageStats.ChatId, messageStats.MessageId, [reaction]);
        }
        
        private static void RemoveExistingDebts()
        {
            if(_currentPurchase == null) return;
            long buyerId = _currentPurchase.Buyer;
            if(!Debts.ContainsKey(buyerId)) Debts.Add(buyerId, []);
            for (int i = Debts[buyerId].Count - 1; i >= 0; i--)
            {
                foreach (long customerId in _currentPurchase.Purchases.Keys)
                {
                    if(customerId == buyerId || Debts[buyerId][i].Towards != customerId) continue;
 
                    double newDebt = Math.Round(Debts[buyerId][i].Amount - _currentPurchase.Purchases[customerId], 2);
                        
                    if(newDebt > 0) Debts[buyerId][i].Amount = newDebt;
                    else Debts[buyerId].RemoveAt(i);
                        
                    if(newDebt >= 0) _currentPurchase.Purchases.Remove(customerId);
                    else _currentPurchase.Purchases[customerId] = -newDebt;
                }
            }

            foreach ((long userId, double debtAmount) in _currentPurchase.Purchases.Where(newDebt => newDebt.Key != buyerId))
            {
                AddReversedDebt(buyerId, userId, debtAmount);
            }
        }
        
        private static void AddReversedDebt(long buyer, long customer, double amount)
        {
            if(!Debts.ContainsKey(customer)) Debts.Add(customer, []);
            
            if(!Debts[customer].Exists(x => x.Towards == buyer)) Debts[customer].Add(new Debts() { Towards = buyer, Amount = Math.Round(amount, 2) });
            else
            {
                foreach (Debts debt in Debts[customer].Where(debt => debt.Towards == buyer))
                {
                    debt.Amount = Math.Round(debt.Amount + amount, 2);
                }
            }
        }
        
        private static string UpdateCurrentPurchaseMessage()
        {
            if (_currentPurchase == null) return "No purchase active";
            
            var text = $"Purchase {DateTime.Now:dd/MM/yyyy}\n\n";
            double totalPrice = 0;
            foreach (long userId in _currentPurchase.Purchases.Keys)
            {
                text += $"{TelegramUtils.IdToName(userId)}: {_currentPurchase.Purchases[userId]}\u20ac\n";
                totalPrice += _currentPurchase.Purchases[userId];
            }
            text += $"\n\nTotal: {totalPrice}";
            return text;
        }

        private static string GetCurrentBuyers()
        {
            return _currentPurchase == null ? "" : _currentPurchase.History.Peek().Customers.Aggregate("", (current, buyer) => current + $"{TelegramUtils.IdToName(buyer)} ");
        }
        private static string UpdateBuyersMessage()
        {
            return _currentPurchase == null ? "No purchase active" : $"Buyers of next items [or list them in a text message]{GetCurrentBuyers()}\n";
        }
        
        public static bool IsWaiting(long chatId)
        {
            return ChannelWaitingForText.ContainsKey(chatId);
        }
        private static bool IsChannelAllowed(long channelId)
        {
            return AllowedChannels.Contains(TelegramUtils.IdToGroupName(channelId));
        }

        private static InlineKeyboardMarkup CreatePurchaseButtons()
        {
            var rows = new InlineKeyboardButton[2][];
            rows[0] = new InlineKeyboardButton[1];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("New Purchase", "shopping-new-purchase");
            
            rows[1] = new InlineKeyboardButton[1];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("Show Debts", "shopping-show-debts");
            
            return new InlineKeyboardMarkup(rows);
        }
        
        private static InlineKeyboardMarkup CreateOrderButtons()
        {
            var rows = new InlineKeyboardButton[3][];

            rows[0] = new InlineKeyboardButton[1];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("Add", "shopping-add");
            
            rows[1] = new InlineKeyboardButton[1];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("Pay", "shopping-pay");
            
            rows[2] = new InlineKeyboardButton[2];
            rows[2][0] = InlineKeyboardButton.WithCallbackData("Undo", "shopping-undo");
            rows[2][1] = InlineKeyboardButton.WithCallbackData("Cancel", "shopping-cancel");

            return new InlineKeyboardMarkup(rows);
        }
        
        private static InlineKeyboardMarkup CreateAddItemButtons(string confirmType)
        {
            var rows = new InlineKeyboardButton[2][];

            rows[0] = new InlineKeyboardButton[1];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("Confirm", confirmType);
            
            rows[1] = new InlineKeyboardButton[1];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("<<", "shopping-back");

            return new InlineKeyboardMarkup(rows);
        }
    }

    public class Debts
    {
        public double Amount { get; set; }
        public long Towards { get; init; }
    }

    public class CurrentPurchase
    {
        public int MessageId;
        public long Buyer;
        public readonly Dictionary<long, double> Purchases = new();
        public readonly Stack<SubPurchase> History = new();
    }

    public class SubPurchase
    {
        public List<long> Customers = [];
        public double TotalAmount;
        public readonly List<int> MessagesToDelete = [];
    }
}
