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
            var DataToLoad = Utility.Functions.LoadFile<Dictionary<long, List<Debts>>>("Data/Telegram/Debts.json");
            if (DataToLoad != null) Debts = DataToLoad;
        }

        static public void UpdateDebts()
        {
            Utility.Functions.WriteFile("Data/Telegram/Data.json", Debts);
        }

        static public bool IsChannelAllowed(long channelId)
        {
            return AllowedChannles.Contains(TelegramData.IDToGroupName(channelId));
        }

        static public bool Buy(long user, string message)
        {
            List<string> data = message.Split(" ").ToList();
            List<long> buyers = new();
            double value = 0;
            if (data.Count < 3) return false;
            try
            {
                value = double.Parse(data[1]);
                for (int i = 2; i < data.Count; i++)
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
                AddDebt(user, amount, buyer);
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
                if (id == -1) Debts[user].Add(new Debts() { Amount=amount, To=buyer });   
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
            if (!Debts.ContainsKey(id) || Debts[id].Count == 0) return $"{username} non deve soldi a nessuno\n";
            string result = "";
            foreach(var owns in Debts[id])
            {
                result += $"{username} deve {owns.Amount} a {TelegramData.IDToName(owns.To)}\n";
            }
            result += "\n";
            foreach(var ownance in Debts)
            {
                if (ownance.Key == id) continue;
                else
                {
                    foreach(var owns in ownance.Value)
                    {
                        if(owns.To == id)
                        {
                            result += $"{TelegramData.IDToName(ownance.Key)} deve {owns.Amount} a {username}\n";
                        }
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
