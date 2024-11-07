using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Modules
{
    internal enum EPurchaseSteps { Buyers, Amount }
    public static class ShoppingModule
    {
        private static readonly List<string> AllowedChannels = ["ProcioneSpazialeMistico"];

        private static readonly List<long> Users =
        [
            TelegramData.NameToId("@gwynn7"),
            TelegramData.NameToId("@Vasile76"),
            TelegramData.NameToId("@moostacho")
        ];
        
        private static Dictionary<long, List<Debts>> _debts = null!;
        private static readonly Dictionary<long, EPurchaseSteps> ChannelWaitingForText = new();
        private static CurrentPurchase? _currentPurchase;
        
        public static void LoadDebts()
        {
            _debts = Utility.Functions.LoadFile<Dictionary<long, List<Debts>>>("Config/Telegram/Debts.json") ?? _debts;
        }
        
        private static void UpdateDebts()
        {
            Utility.Functions.WriteFile("Config/Telegram/Debts.json", _debts);
        }
        
         public static async void ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
        {
            switch (messageStats.Command)
            {
                case "purchase":
                    if (IsChannelAllowed(messageStats.ChatID))
                    {
                        if (_currentPurchase != null)
                        {
                            await cortana.SendMessage(messageStats.ChatID, "Complete the current active purchase first!");
                            return;
                        }

                        _currentPurchase = new CurrentPurchase
                        {
                            Buyer = messageStats.UserID
                        };

                        foreach (long userId in Users) _currentPurchase.Purchases.Add(userId, 0.0);
                        
                        Message msg = await cortana.SendMessage(messageStats.ChatID, UpdateCurrentPurchaseMessage());
                        _currentPurchase.MessageId = msg.MessageId;
                        await cortana.SendMessage(messageStats.ChatID, "Options", replyMarkup: CreateOrderButtons());
                    }
                    else await cortana.SendMessage(messageStats.ChatID, "Comando riservato a specifici gruppi");
                    break;
                case "debt":
                    if (IsChannelAllowed(messageStats.ChatID)) await cortana.SendMessage(messageStats.ChatID, GetDebts());
                    else await cortana.SendMessage(messageStats.ChatID, "Comando riservato a specifici gruppi");
                    break;
            }
        }

        private static string GetDebts()
        {
            var debts = "Debts\n\n";
            foreach ((long userId, List<Debts> debtsList) in _debts)
            {
                debts = debtsList.Aggregate(debts, (current, debt) => current + $"{TelegramData.IdToName(userId)} owns {debt.Amount}\u20ac to {TelegramData.IdToName(debt.Towards)}\n");
                debts += "\n\n";
            }
            return debts;
        }
        
        public static async void ButtonCallback(ITelegramBotClient cortana, Update update)
        {
            if(_currentPurchase == null || update.CallbackQuery == null) return;
            
            string data = update.CallbackQuery.Data!;
            int messageId = update.CallbackQuery.Message!.MessageId;

            await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);

            switch (data)
            {
                case "add":
                    var subPurchase = new SubPurchase()
                    {
                        Customers = Users,
                        TotalAmount = 0
                    };
                    _currentPurchase.History.Push(subPurchase);
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, UpdateBuyersMessage(), replyMarkup: CreateAddItemButtons("confirm-customers"));
                    ChannelWaitingForText.Add(update.CallbackQuery.Message.Chat.Id, EPurchaseSteps.Buyers);
                    break;
                
                case "pay":
                    RemoveExistingDebts();
                    
                    await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);
                    await cortana.DeleteMessage(update.CallbackQuery.Message.Chat.Id, messageId);
                    _currentPurchase = null;
                    
                    UpdateDebts();
                    break;
                case "undo":
                    break;
                case "cancel":
                    await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);
                    await cortana.DeleteMessage(update.CallbackQuery.Message.Chat.Id, messageId);
                    await cortana.DeleteMessage(update.CallbackQuery.Message.Chat.Id, _currentPurchase.MessageId);
                    _currentPurchase = null;
                    break;
                case "confirm-customers":
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, "List the price of every item",  replyMarkup: CreateAddItemButtons("confirm-money"));
                    ChannelWaitingForText.Add(update.CallbackQuery.Message.Chat.Id, EPurchaseSteps.Amount);
                    break;
                case "confirm-money":
                    SubPurchase lastSubPurchase = _currentPurchase.History.Peek();
                    await cortana.DeleteMessages(update.CallbackQuery.Message.Chat.Id, lastSubPurchase.MessagesToDelete);
                    lastSubPurchase.MessagesToDelete.Clear();
                    
                    double amount = lastSubPurchase.TotalAmount / lastSubPurchase.Customers.Count;
                    foreach (long customer in lastSubPurchase.Customers)
                    {
                        _currentPurchase.Purchases[customer] += amount;
                    }

                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, "Options", replyMarkup: CreateOrderButtons());
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, _currentPurchase.MessageId, UpdateCurrentPurchaseMessage());
                    break;
                case "return":
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, "Options", replyMarkup: CreateOrderButtons());
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, _currentPurchase.MessageId, UpdateCurrentPurchaseMessage());
                    break;
            }
        }
        
        public static async void HandleCallback(MessageStats messageStats, ITelegramBotClient cortana)
        {
            if(_currentPurchase == null) return;
            if(!IsWaiting(messageStats.ChatID)) return;

            SubPurchase subPurchase = _currentPurchase.History.Pop();
            var thumbsUpReaction = new ReactionTypeEmoji { Emoji = "👍" };
            
            switch (ChannelWaitingForText[messageStats.ChatID])
            {
                case EPurchaseSteps.Amount:
                    foreach (string amount in messageStats.FullMessage.Split())
                    {
                        subPurchase.TotalAmount += double.Parse(amount); //ERROR CHECK
                    }
                    break;
                case EPurchaseSteps.Buyers:
                    subPurchase.Customers.Clear();
                    foreach (string user in messageStats.FullMessage.Split())
                    {
                        subPurchase.Customers.Add(TelegramData.NameToId(user)); //ERROR CHECK
                    }
                    break;
                default:
                    //Error
                    break;
            }
            await cortana.SetMessageReaction(messageStats.ChatID, messageStats.MessageID, [thumbsUpReaction]);
            subPurchase.MessagesToDelete.Add(messageStats.MessageID);
            _currentPurchase.History.Push(subPurchase);
        }
        
        private static void RemoveExistingDebts()
        {
            if(_currentPurchase == null) return;
            long buyerId = _currentPurchase.Buyer;
            if(!_debts.ContainsKey(buyerId)) _debts.Add(buyerId, []);
            for (int i = _debts[buyerId].Count - 1; i >= 0; i--)
            {
                foreach (long customerId in _currentPurchase.Purchases.Keys)
                {
                    if(customerId == buyerId || _debts[buyerId][i].Towards != customerId) continue;
 
                    double newDebt = Math.Round(_debts[buyerId][i].Amount - _currentPurchase.Purchases[customerId], 2);
                        
                    if(newDebt > 0) _debts[buyerId][i].Amount = newDebt;
                    else _debts[buyerId].RemoveAt(i);
                        
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
            if(!_debts.ContainsKey(customer)) _debts.Add(customer, []);
            
            if(!_debts[customer].Exists(x => x.Towards == buyer)) _debts[customer].Add(new Debts() { Towards = buyer, Amount = Math.Round(amount, 2) });
            else
            {
                foreach (Debts debt in _debts[customer].Where(debt => debt.Towards == buyer))
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
                text += $"{TelegramData.IdToName(userId)}: {_currentPurchase.Purchases[userId]}\u20ac\n";
                totalPrice += _currentPurchase.Purchases[userId];
            }
            text += $"\n\nTotal: {totalPrice}";
            return text;
        }

        private static string UpdateBuyersMessage()
        {
            return _currentPurchase == null ? "No purchase active" : _currentPurchase.History.Peek().Customers.Aggregate("Buyers of next sub-purchase [or list them in a text message]\n", (current, buyer) => current + $"{TelegramData.IdToName(buyer)} ");
        }
        
        public static bool IsWaiting(long chatId)
        {
            return ChannelWaitingForText.ContainsKey(chatId);
        }
        
        private static bool IsChannelAllowed(long channelId)
        {
            return AllowedChannels.Contains(TelegramData.IdToGroupName(channelId));
        }
        
        private static InlineKeyboardMarkup CreateOrderButtons()
        {
            var rows = new InlineKeyboardButton[3][];

            rows[0] = new InlineKeyboardButton[1];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("Add", "add");
            
            rows[1] = new InlineKeyboardButton[1];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("Pay", "pay");
            
            rows[2] = new InlineKeyboardButton[2];
            rows[2][0] = InlineKeyboardButton.WithCallbackData("Undo", "undo");
            rows[2][1] = InlineKeyboardButton.WithCallbackData("Cancel", "cancel");

            return new InlineKeyboardMarkup(rows);
        }
        
        private static InlineKeyboardMarkup CreateAddItemButtons(string confirmType)
        {
            var rows = new InlineKeyboardButton[2][];

            rows[0] = new InlineKeyboardButton[1];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("Confirm", confirmType);
            
            rows[1] = new InlineKeyboardButton[1];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("<<", "return");

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
