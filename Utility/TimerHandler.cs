namespace Utility
{
    public class TimerHandler : System.Timers.Timer
    {
        public static Dictionary<ETimerLocation, List<TimerHandler>> TotalTimers;
        public string? Data { get; set; }
        private string timerName;

        public TimerHandler(string Name, double NewInterval, string? NewData, Action<object?, System.Timers.ElapsedEventArgs> Callback, ETimerLocation TimerLocation, bool Loop = true, bool AutoStart = true)
        {
            timerName = Name;
            Interval = NewInterval;
            Data = NewData;
            Elapsed += new System.Timers.ElapsedEventHandler(Callback);
            AutoReset = Loop;
            if (AutoStart) Start();

            AddTimer(TimerLocation, this);
        }

        public TimerHandler(string Name, int Hours, int Minutes, int Seconds, string? NewData, Action<object?, System.Timers.ElapsedEventArgs> Callback, ETimerLocation TimerLocation, bool Loop = false, bool AutoStart = true)
        {
            timerName = Name;
            Interval = (Hours * 3600 + Minutes * 60 + Seconds) * 1000;
            Data = NewData;
            Elapsed += new System.Timers.ElapsedEventHandler(Callback);
            AutoReset = Loop;
            if (AutoStart) Start();

            AddTimer(TimerLocation, this);
        }

        public TimerHandler(string Name, DateTime TargetTime, string? NewData, Action<object?, System.Timers.ElapsedEventArgs> Callback, ETimerLocation TimerLocation, bool Loop = false, bool AutoStart = true)
        {
            timerName = Name;
            Interval = TargetTime.Subtract(DateTime.Now).Minutes <= 5 ? TargetTime.AddDays(1).Subtract(DateTime.Now).TotalMilliseconds : TargetTime.Subtract(DateTime.Now).TotalMilliseconds;
            Data = NewData;
            Elapsed += new System.Timers.ElapsedEventHandler(Callback);
            AutoReset = Loop;
            if (AutoStart) Start();

            AddTimer(TimerLocation, this);
        }

        private void Destroy()
        {
            Stop();
            Dispose();
        }

        private static void AddTimer(ETimerLocation TimerLocation, TimerHandler Timer)
        {
            if (TotalTimers == null) TotalTimers = new();

            if(TotalTimers.ContainsKey(TimerLocation))
            {
                TotalTimers[TimerLocation].Add(Timer);
            }
            else
            {
                TotalTimers.Add(TimerLocation, new List<TimerHandler> { Timer });
            }
        }

        public static void RemoveTimer(TimerHandler Timer)
        {
            foreach(var TimerList in TotalTimers)
            {
                foreach(var ListTimer in TimerList.Value)
                {
                    if(ListTimer.timerName == Timer.timerName)
                    {
                        TotalTimers[TimerList.Key].Remove(Timer);
                        Timer.Destroy();
                        return;
                    }
                }
            }
        }

        public static void RemoveTimers(ETimerLocation Location)
        {
            if (TotalTimers == null) return;
            if (!TotalTimers.ContainsKey(Location)) return;
            
            foreach(var LocationTimer in TotalTimers[Location])
            {
                RemoveTimer(LocationTimer);
            }
        }

        public static void RemoveTimerByName(string name, ETimerLocation? Location = null)
        {
            if (TotalTimers == null) return;
            if (Location != null)
            {
                if (!TotalTimers.ContainsKey(Location.Value)) return;

                foreach (var LocationTimer in TotalTimers[Location.Value])
                {
                    if (LocationTimer.timerName == name)
                    {
                        RemoveTimer(LocationTimer);
                        return;
                    }
                }
            }
            else
            {
                foreach (var TimerList in TotalTimers)
                {
                    foreach (var ListTimer in TimerList.Value)
                    {
                        if (ListTimer.timerName == name)
                        {
                            RemoveTimer(ListTimer);
                            return;
                        }
                    }
                }
            }
        }
    }
}
