using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Processor;

namespace TelegramBot.Modules
{
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
        private static readonly List<long> ChannelWaitingForText;

        static ShoppingModule()
        {
            Debts = Software.LoadFile<Dictionary<long, List<Debts>>>("Storage/Config/Telegram/Debts.json") ?? new Dictionary<long, List<Debts>>();
            ChannelWaitingForText = [];
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
            var debts = "";
            foreach ((long userId, List<Debts> debtsList) in Debts)
            {
                if(debtsList.Count == 0) continue;
                debts += $"{TelegramUtils.IdToName(userId)} owes:\n";
                debts = debtsList.Aggregate(debts, (current, debt) => current + $"    - {Math.Round(debt.Amount, 2)}\u20ac to {TelegramUtils.IdToName(debt.Towards)}\n");
            }
            if(debts == "") debts = "No debts are owned";
            return debts;
        }
        
        public static async void ButtonCallback(ITelegramBotClient cortana, Update update, string command)
        {
            if (update.CallbackQuery == null) return;
            await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);
            
            Message message = update.CallbackQuery.Message!;
            switch (command)
            {
                case "new-purchase":
                    if (_currentPurchase != null)
                    {
                        await cortana.SendMessage(message.Chat.Id, "Complete the current active purchase first!");
                        return;
                    }
                    _currentPurchase = new CurrentPurchase{ Buyer = update.CallbackQuery.From.Id };
                    foreach (long userId in Users) _currentPurchase.Purchases.Add(userId, 0.0);
                    await cortana.SendMessage(message.Chat.Id, UpdateCurrentPurchaseMessage(), replyMarkup: CreateOrderButtons());
                    break;
                case "show-debts":
                    await cortana.SendMessage(message.Chat.Id, GetDebts());
                    break;
                default:
                    HandlePurchase(cortana, message, command);
                    break;
            }
        }

        private static async void HandlePurchase(ITelegramBotClient cortana, Message message, string command)
        {
            int messageId = message.MessageId;
            if (_currentPurchase == null)
            {
                await cortana.DeleteMessage(message.Chat.Id, messageId);
                return;
            }
            
            switch (command)
            {
                case "add":
                    var subPurchase = new SubPurchase()
                    {
                        Customers = Users.ToList(),
                        TotalAmount = 0
                    };
                    _currentPurchase.History.Push(subPurchase);
                    await cortana.EditMessageText(message.Chat.Id, messageId, UpdateBuyersMessage(), replyMarkup: CreateAddCustomerButtons());
                    break;
                case "list":
                    await cortana.EditMessageText(message.Chat.Id, messageId, $"Send me the cost of the products bought by {GetCurrentBuyers()}",  replyMarkup: CreateAddItemButtons());
                    ChannelWaitingForText.Add(message.Chat.Id);
                    break;
                case "confirm":
                {
                    ChannelWaitingForText.Remove(message.Chat.Id);

                    SubPurchase lastSubPurchase = _currentPurchase.History.Peek();
                    double amount = Math.Round(lastSubPurchase.TotalAmount / lastSubPurchase.Customers.Count, 3);
                    if (amount != 0) foreach (long customer in lastSubPurchase.Customers) _currentPurchase.Purchases[customer] = Math.Round(_currentPurchase.Purchases[customer] + amount, 3);
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
                    ChannelWaitingForText.Remove(message.Chat.Id);
                    await cortana.EditMessageText(message.Chat.Id, messageId, UpdateCurrentPurchaseMessage(), replyMarkup: CreateOrderButtons());
                    break;
                default:
                {
                    if (!command.StartsWith("user:")) return;
                    string user = command["user:".Length..];
                    long userId = TelegramUtils.NameToId(user);
                    SubPurchase lastSubPurchase = _currentPurchase.History.Peek();
                    if (!lastSubPurchase.Customers.Remove(userId)) lastSubPurchase.Customers.Add(userId);
                    if(lastSubPurchase.Customers.Count == 0) lastSubPurchase.Customers = Users.ToList();
                    await cortana.EditMessageText(message.Chat.Id, messageId, UpdateBuyersMessage(), replyMarkup: CreateAddCustomerButtons());
                    break;
                }
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
            
            _currentPurchase.MessagesToDelete.Add(messageStats.MessageId);
            
            var reaction = new ReactionTypeEmoji { Emoji = "👍" };
            var failedReaction = new ReactionTypeEmoji { Emoji = "👎" };
            
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
            
            SubPurchase subPurchase = _currentPurchase.History.Peek();
            subPurchase.TotalAmount += newAmount;
            
            await cortana.SetMessageReaction(messageStats.ChatId, messageStats.MessageId, [reaction]);
        }
        
        private static void RemoveExistingDebts()
        {
            if(_currentPurchase == null) return;
            long buyerId = _currentPurchase.Buyer;
            if(!Debts.ContainsKey(buyerId)) Debts.Add(buyerId, []);
            foreach(Debts debt in Debts[buyerId].ToList())
            {
                foreach ((long customerId, double amount) in _currentPurchase.Purchases)
                {
                    if(customerId == buyerId || debt.Towards != customerId) continue;
 
                    double newDebt = debt.Amount - amount;
                        
                    if(newDebt > 0) debt.Amount = newDebt;
                    else Debts[buyerId].Remove(debt);
                        
                    if(newDebt >= 0) _currentPurchase.Purchases.Remove(customerId);
                    else _currentPurchase.Purchases[customerId] = Math.Round(-newDebt, 3);
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
            
            if(!Debts[customer].Exists(x => x.Towards == buyer)) Debts[customer].Add(new Debts(towards: buyer, amount: Math.Round(amount, 3) ));
            else
            {
                foreach (Debts debt in Debts[customer].Where(debt => debt.Towards == buyer))
                {
                    debt.Amount = Math.Round(debt.Amount + amount, 3);
                }
            }
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
            text += $"\nTotal: {Math.Round(totalPrice,2)}\u20ac";
            
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
        
        public static bool IsWaiting(long chatId)
        {
            return ChannelWaitingForText.Contains(chatId);
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
        
        private static InlineKeyboardMarkup CreateAddCustomerButtons()
        {
            if (_currentPurchase == null || _currentPurchase.History.Count == 0) throw new CortanaException("No purchase active");
            
            var rows = new InlineKeyboardButton[Users.Count + 1][];
            for (var i = 0; i < Users.Count; i++)
            {
                SubPurchase subPurchase = _currentPurchase.History.Peek();
                string sign = subPurchase.Customers.Contains(Users[i]) ? "\u2705" : "\u274c";
                
                string customer = TelegramUtils.IdToName(Users[i]);
                rows[i] = new InlineKeyboardButton[1];
                rows[i][0] = InlineKeyboardButton.WithCallbackData($"{sign} {customer}", $"shopping-user:{customer}");
            }
            
            rows[Users.Count] = new InlineKeyboardButton[1];
            rows[Users.Count][0] = InlineKeyboardButton.WithCallbackData("Next", "shopping-list");

            return new InlineKeyboardMarkup(rows);
        }
        
        private static InlineKeyboardMarkup CreateAddItemButtons()
        {
            var rows = new InlineKeyboardButton[2][];

            rows[0] = new InlineKeyboardButton[1];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("Confirm", "shopping-confirm");
            
            rows[1] = new InlineKeyboardButton[1];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("Cancel", "shopping-back");

            return new InlineKeyboardMarkup(rows);
        }
    }

    [method: JsonConstructor]
    public class Debts(
        double amount, 
        long towards)
    {
        public double Amount { get; set; } = amount;
        public long Towards { get; } = towards;
    }

    public class CurrentPurchase
    {
        public long Buyer;
        public readonly Dictionary<long, double> Purchases = new();
        public readonly Stack<SubPurchase> History = new();
        public readonly List<int> MessagesToDelete = [];
    }

    public class SubPurchase
    {
        public List<long> Customers = [];
        public double TotalAmount;
    }
}
