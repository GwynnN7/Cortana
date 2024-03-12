using Telegram.Bot;

namespace TelegramBot.Modules
{
    public static class ShoppingModule
    {
        private static Dictionary<long, List<Debts>> Debts;
        private static List<string> AllowedChannles = new()
        {
            "ProcioneSpazialeMistico"
        };
        
        public static void LoadDebts()
        {
            Debts = Utility.Functions.LoadFile<Dictionary<long, List<Debts>>>("Data/Telegram/Debts.json") ?? new();
        }

        private static void UpdateDebts()
        {
            Utility.Functions.WriteFile("Data/Telegram/Debts.json", Debts);
        }
         public static async void ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
        {
            switch (messageStats.Command)
            {
                case "buy":
                    if (IsChannelAllowed(messageStats.ChatID))
                    {
                        bool result = Buy(messageStats.UserID, messageStats.Text);
                        if (result) await cortana.SendTextMessageAsync(messageStats.ChatID, "Debiti aggiornati");
                        else await cortana.SendTextMessageAsync(messageStats.ChatID, "Non è stato possibile aggiornare i debiti");
                    }
                    else await cortana.SendTextMessageAsync(messageStats.ChatID, "Comando riservato al gruppo");
                    break;
                case "debt":
                    if (IsChannelAllowed(messageStats.ChatID))
                    {
                        if (messageStats.TextList.Count > 0)
                        {
                            for (int i = 0; i < messageStats.TextList.Count; i++)
                            {
                                await cortana.SendTextMessageAsync(messageStats.ChatID, GetDebts(messageStats.TextList[i]));
                            }
                        }
                        else await cortana.SendTextMessageAsync(messageStats.ChatID, GetDebts(TelegramData.IDToName(messageStats.UserID)));
                    }
                    else await cortana.SendTextMessageAsync(messageStats.ChatID, "Comando riservato a specifici gruppi");
                    break;
            }
        }

        private static bool Buy(long user, string message)
        {
            List<string> data = message.Split(" ").ToList();
            List<long> buyers = new();
            double value;
            if (data.Count == 0) return false;
            try
            {
                value = double.Parse(data[0]);
                for (int i = 1; i < data.Count; i++)
                {
                    buyers.Add(TelegramData.NameToID(data[i].Replace(" ", "")));
                }
                if(buyers.Count == 0)
                {
                    buyers = new()
                    {
                        TelegramData.NameToID("@gwynn7"),
                        TelegramData.NameToID("@Vasile76"),
                        TelegramData.NameToID("@moostacho"),
                    };
                }
            }
            catch
            {
                return false;
            }
            
            double amount = value / buyers.Count;
            foreach(long buyer in buyers)
            {
                AddDebt(buyer, amount, user);
            }
            return true;
        }

        private static void AddDebt(long user, double amount, long buyer)
        {
            if (!Debts.ContainsKey(user)) Debts.Add(user, new());
            if (user == buyer) return;

            double oldDebt = GetDebt(buyer, user);
            if (oldDebt > 0)
            {
                if (amount >= oldDebt)
                {
                    Debts[buyer].RemoveAt(GetOwnance(buyer, user));
                    AddDebt(user, amount - oldDebt, buyer);
                }
                else if (amount < oldDebt)
                {
                    var currentDebt = Debts[buyer][GetOwnance(buyer, user)].Amount;
                    Debts[buyer][GetOwnance(buyer, user)].Amount = Math.Round(currentDebt - amount, 2);
                }
            }
            else
            {
                
                int id = GetOwnance(user, buyer);
                if (id == -1)
                {
                    if(amount > 0) Debts[user].Add(new Debts() { Amount = Math.Round(amount, 2), To = buyer });
                }
                else 
                {
                    var currentDebt = Debts[user][id].Amount;
                    Debts[user][id].Amount = Math.Round(currentDebt + amount, 2);
                }
            }
            UpdateDebts();
        }

        private static double GetDebt(long from, long to)
        {
            if (!Debts.ContainsKey(from)) return 0;
            foreach (var ownance in Debts[from])
            {
                if (ownance.To == to) return ownance.Amount;
            }
            return 0;
        }
        
        private static bool IsChannelAllowed(long channelId)
        {
            return AllowedChannles.Contains(TelegramData.IDToGroupName(channelId));
        }

        private static int GetOwnance(long from, long to)
        {
            if(!Debts.ContainsKey(from)) return -1;
            for(int i=0; i< Debts[from].Count; i++)
            {
                if (Debts[from][i].To == to) return i;
            }
            return -1;
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
    }

    public class Debts
    {
        public long To { get; set; }
        public double Amount { get; set; }
    }
}
