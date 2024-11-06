using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Modules
{
    enum EPurchaseSteps { BUYERS, AMOUNT }
    public static class ShoppingModule
    {
        private static List<string> AllowedChannels = new()
        {
            "ProcioneSpazialeMistico"
        };

        private static List<long> Users = new()
        {
            TelegramData.NameToID("@gwynn7"),
            TelegramData.NameToID("@Vasile76"),
            TelegramData.NameToID("@moostacho"),
        };
        
        private static Dictionary<long, List<Debts>> Debts;
        private static CurrentPurchase? currentPurchase;
        private static Dictionary<long, EPurchaseSteps> channelWaitingForText = new();
        
        public static void LoadDebts()
        {
            Debts = Utility.Functions.LoadFile<Dictionary<long, List<Debts>>>("Data/Telegram/Debts.json") ?? new();
        }

        private static void UpdateDebts()
        {
            Utility.Functions.WriteFile("Data/Telegram/Debts.json", Debts);
        }
        
        private static string UpdateCurrentPurchaseMessage()
        {
            if (currentPurchase == null) return "No purchase active";
            
            string text = $"Purchase {DateTime.Now.ToString("dd/MM/yyyy")}\n\n";
            double totalPrice = 0;
            foreach (var userId in currentPurchase.Purchases.Keys)
            {
                text += $"{TelegramData.IDToName(userId)}: {currentPurchase.Purchases[userId]}\u20ac\n";
                totalPrice += currentPurchase.Purchases[userId];
            }
            text += $"\n\nTotal: {totalPrice}";
            return text;
        }

        private static string UpdateBuyersMessage()
        {
            if (currentPurchase == null) return "No purchase active";
            
            string text = "Buyers of next sub-purchase [or list them in a text message]\n";
            foreach (var buyer in currentPurchase.History.Peek().Customers)
            {
                text += $"{TelegramData.IDToName(buyer)} ";
            }

            return text;
        }
        
         public static async void ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
        {
            switch (messageStats.Command)
            {
                case "purchase":
                    if (IsChannelAllowed(messageStats.ChatID))
                    {
                        if (currentPurchase != null)
                        {
                            await cortana.SendMessage(messageStats.ChatID, "Complete the current active purchase first!");
                            return;
                        }
                        currentPurchase = new();
                        currentPurchase.Buyer = messageStats.UserID;
                        
                        foreach (var userId in Users)
                        {
                            currentPurchase.Purchases.Add(userId, 0.0);
                        }
                        
                        var msg = await cortana.SendMessage(messageStats.ChatID, UpdateCurrentPurchaseMessage());
                        currentPurchase.MessageId = msg.MessageId;
                        await cortana.SendMessage(messageStats.ChatID, "Options", replyMarkup: CreateOrderButtons());
                    }
                    else await cortana.SendMessage(messageStats.ChatID, "Comando riservato a specifici gruppi");
                    break;
                case "debt":
                    if (IsChannelAllowed(messageStats.ChatID))
                    {
                        if (messageStats.TextList.Count > 0)
                        {
                            for (int i = 0; i < messageStats.TextList.Count; i++)
                            {
                                await cortana.SendMessage(messageStats.ChatID, GetDebts(messageStats.TextList[i]));
                            }
                        }
                        else await cortana.SendMessage(messageStats.ChatID, GetDebts(TelegramData.IDToName(messageStats.UserID)));
                    }
                    else await cortana.SendMessage(messageStats.ChatID, "Comando riservato a specifici gruppi");
                    break;
            }
        }
        
        private static bool IsChannelAllowed(long channelId)
        {
            return AllowedChannels.Contains(TelegramData.IDToGroupName(channelId));
        }

        private static string GetDebts(string username)
        {
            long id = TelegramData.NameToID(username);
            string result = "";

            if (!Debts.ContainsKey(id) || Debts[id].Count == 0) result += $"{username} non deve soldi a nessuno\n";
            else
            {
                foreach (var owns in Debts[id])
                {
                    result += $"{username} deve {owns.Amount} a {TelegramData.IDToName(owns.To)}\n";
                }
            }
            
            result += "\n";
            foreach(var ownance in Debts)
            {
                if (ownance.Key == id) continue;
                foreach(var owns in ownance.Value)
                {
                    if(owns.To == id && owns.Amount > 0)
                    {
                        result += $"{TelegramData.IDToName(ownance.Key)} deve {owns.Amount} a {username}\n";
                    }
                }
            }
            return result;
        }
        
        
        public static bool IsWaiting(long chatId)
        {
            return channelWaitingForText.ContainsKey(chatId);
        }
        
        public static async void HandleCallback(MessageStats messageStats, ITelegramBotClient cortana)
        {
            if(currentPurchase == null) return;
            if(!IsWaiting(messageStats.ChatID)) return;

            var subPurchase = currentPurchase.History.Pop();
            var thumbsUpReaction = new ReactionTypeEmoji { Emoji = "👍" };
            
            switch (channelWaitingForText[messageStats.ChatID])
            {
                case EPurchaseSteps.AMOUNT:
                    foreach (var amount in messageStats.FullMessage.Split())
                    {
                        subPurchase.TotalAmount += Double.Parse(amount); //ERROR CHECK
                    }
                    break;
                case EPurchaseSteps.BUYERS:
                    subPurchase.Customers.Clear();
                    foreach (var user in messageStats.FullMessage.Split())
                    {
                        subPurchase.Customers.Add(TelegramData.NameToID(user)); //ERROR CHECK
                    }
                    break;
            }
            await cortana.SetMessageReaction(messageStats.ChatID, messageStats.MessageID, [thumbsUpReaction]);
            subPurchase.MessagesToDelete.Add(messageStats.MessageID);
            currentPurchase.History.Push(subPurchase);
        }

        private static void AddReversedDebt(long buyer, long customer, double amount)
        {
            if(!Debts.ContainsKey(customer)) Debts.Add(customer, new());
            
            if(!Debts[customer].Exists(x => x.Towards == buyer)) Debts[customer].Add(new Debts() { Towards = buyer, Amount = Math.Round(amount, 2) });
            else
            {
                foreach (var debt in Debts[customer].Where(debt => debt.Towards == buyer))
                {
                    debt.Amount = Math.Round(debt.Amount + amount, 2);
                }
            }
        }
        
        private static void RemoveExistingDebts()
        {
            if(currentPurchase == null) return;
            var buyer = currentPurchase.Buyer;
            if(!Debts.ContainsKey(buyer)) Debts.Add(buyer, new());
            for (int i = Debts[buyer].Count - 1; i >= 0; i--)
            {
                foreach (var customer in currentPurchase.Purchases.Keys)
                {
                    if(customer == buyer) continue;
                    if (Debts[buyer][i].Towards == customer)
                    {
                        var newDebt = Math.Round(Debts[buyer][i].Amount - currentPurchase.Purchases[customer], 2);
                        
                        if(newDebt > 0) Debts[buyer][i].Amount = newDebt;
                        else Debts[buyer].RemoveAt(i);
                        
                        if(newDebt >= 0) currentPurchase.Purchases.Remove(customer);
                        else currentPurchase.Purchases[customer] = -newDebt;
                    }
                }
            }

            foreach (var newDebt in currentPurchase.Purchases)
            {
                if(newDebt.Key == buyer) continue;
                AddReversedDebt(buyer, newDebt.Key, newDebt.Value);
            }
        }
        
        public static async void ButtonCallback(ITelegramBotClient cortana, Update update)
        {
            if(currentPurchase == null) return;
            
            string data = update.CallbackQuery.Data;
            int messageId = update.CallbackQuery.Message.MessageId;

            await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);

            switch (data)
            {
                case "add":
                    var subPurchase = new SubPurchase()
                    {
                        Customers = Users,
                        TotalAmount = 0
                    };
                    currentPurchase.History.Push(subPurchase);
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, UpdateBuyersMessage(), replyMarkup: CreateAddItemButtons("confirm-customers"));
                    channelWaitingForText.Add(update.CallbackQuery.Message.Chat.Id, EPurchaseSteps.BUYERS);
                    break;
                
                case "pay":
                    RemoveExistingDebts();
                    
                    await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);
                    await cortana.DeleteMessage(update.CallbackQuery.Message.Chat.Id, messageId);
                    currentPurchase = null;
                    
                    UpdateDebts();
                    break;
                case "undo":
                    break;
                case "cancel":
                    await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);
                    await cortana.DeleteMessage(update.CallbackQuery.Message.Chat.Id, messageId);
                    await cortana.DeleteMessage(update.CallbackQuery.Message.Chat.Id, currentPurchase.MessageId);
                    currentPurchase = null;
                    break;
                case "confirm-customers":
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, "List the price of every item",  replyMarkup: CreateAddItemButtons("confirm-money"));
                    channelWaitingForText.Add(update.CallbackQuery.Message.Chat.Id, EPurchaseSteps.AMOUNT);
                    break;
                case "confirm-money":
                    var lastSubPurchase = currentPurchase.History.Peek();
                    await cortana.DeleteMessages(update.CallbackQuery.Message.Chat.Id, lastSubPurchase.MessagesToDelete);
                    lastSubPurchase.MessagesToDelete.Clear();
                    
                    var amount = lastSubPurchase.TotalAmount / lastSubPurchase.Customers.Count;
                    foreach (var customer in lastSubPurchase.Customers)
                    {
                        currentPurchase.Purchases[customer] += amount;
                    }

                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, "Options", replyMarkup: CreateOrderButtons());
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, currentPurchase.MessageId, UpdateCurrentPurchaseMessage());
                    break;
                case "return":
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, messageId, "Options", replyMarkup: CreateOrderButtons());
                    await cortana.EditMessageText(update.CallbackQuery.Message.Chat.Id, currentPurchase.MessageId, UpdateCurrentPurchaseMessage());
                    break;
            }
        }
        
        private static InlineKeyboardMarkup CreateOrderButtons()
        {
            InlineKeyboardButton[][] rows = new InlineKeyboardButton[4][];

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
            InlineKeyboardButton[][] rows = new InlineKeyboardButton[2][];

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
        public long Towards { get; set; }
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
        public List<long> Customers = new();
        public double TotalAmount = 0;
        public readonly List<int> MessagesToDelete = new();
    }
}
