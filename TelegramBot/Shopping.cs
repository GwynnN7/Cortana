using Telegram.Bot.Types;

namespace TelegramBot
{
    public static class Shopping
    {
        public static Dictionary<long, List<Debts>> Debts;

        private static Dictionary<string, long> Usernames = new()
        {
            { "@Vasile78", 975535920 },
            { "@mattcheru", 327041645 },
            { "@coolermustacho", 168702626 }
        };

        static public void LoadDebts()
        {
            Dictionary<long, List<Debts>>? DataToLoad = Utility.Functions.LoadFile<Dictionary<long, List<Debts>>>("Data/Telegram/Debts.json");
            if (DataToLoad != null) Debts = DataToLoad;
        }

        static public void UpdateDebts()
        {
            Utility.Functions.WriteFile("Data/Telegram/Data.json", Debts);
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
                    if (!Usernames.ContainsKey(data[i])) continue;
                    buyers.Add(Usernames[data[i]]);
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

        static public string GetDebts(long from)
        {
            if (!Debts.ContainsKey(from) || Debts[from].Count == 0) return "Non devi soldi a nessuno";
            string result = "";
            foreach(var owns in Debts[from])
            {
                result += $"Devi {owns.Amount} a {Usernames.Where(x => x.Value == owns.To).First().Key}\n";
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
