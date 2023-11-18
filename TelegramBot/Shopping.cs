namespace TelegramBot
{
    public static class Shopping
    {
        public static Dictionary<long, List<Debts>> Debts;
        private static List<string> AllowedChannles = new List<string>()
        {
            "ProcioneSpazialeMistico"
        };

        static public void LoadDebts()
        {
            Debts = Utility.Functions.LoadFile<Dictionary<long, List<Debts>>>("Data/Telegram/Debts.json") ?? new();
        }

        static public void UpdateDebts()
        {
            Utility.Functions.WriteFile("Data/Telegram/Debts.json", Debts);
        }

        static public bool IsChannelAllowed(long channelId)
        {
            return AllowedChannles.Contains(TelegramData.IDToGroupName(channelId));
        }

        static public bool Buy(long user, string message)
        {
            List<string> data = message.Split(" ").ToList();
            List<long> buyers = new();
            double value;
            if (data.Count < 2) return false;
            try
            {
                value = double.Parse(data[0]);
                for (int i = 1; i < data.Count; i++)
                {
                    buyers.Add(TelegramData.NameToID(data[i]));
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

        static public void AddDebt(long user, double amount, long buyer)
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
                    Debts[buyer][GetOwnance(buyer, user)].Amount -= amount;
                }
            }
            else
            {
                
                int id = GetOwnance(user, buyer);
                if (id == -1)
                {
                    if(amount > 0) Debts[user].Add(new Debts() { Amount = amount, To = buyer });
                }
                else Debts[user][id].Amount += amount;
            }
            UpdateDebts();
        }

        static public double GetDebt(long from, long to)
        {
            if (!Debts.ContainsKey(from)) return 0;
            foreach (var ownance in Debts[from])
            {
                if (ownance.To == to) return ownance.Amount;
            }
            return 0;
        }

        static public int GetOwnance(long from, long to)
        {
            if(!Debts.ContainsKey(from)) return -1;
            for(int i=0; i< Debts[from].Count; i++)
            {
                if (Debts[from][i].To == to) return i;
            }
            return -1;
        }

        static public string GetDebts(string username)
        {
            long id = TelegramData.NameToID(username);
            string result = "";

            if (!Debts.ContainsKey(id) || Debts[id].Count == 0) result += $"{username} non deve soldi a nessuno\n";
            else
            {
                foreach (var owns in Debts[id])
                {
                    result += $"{username} deve {Math.Round(owns.Amount, 2)} a {TelegramData.IDToName(owns.To)}\n";
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
                        result += $"{TelegramData.IDToName(ownance.Key)} deve {Math.Round(owns.Amount, 2)} a {username}\n";
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
