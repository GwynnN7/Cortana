using System.Timers;

namespace Utility
{
    public abstract class TimerHandler : System.Timers.Timer
    {
        public static Dictionary<ETimerLocation, List<TimerHandler>> TotalTimers;
        public string? Text { get; set; }
        private string timerName;
        public DateTime TimerTargetTime;

        public TimerHandler(string Name, string? NewText, double NewInterval, Action<object?, ElapsedEventArgs> Callback, ETimerLocation TimerLocation, bool Loop, bool AutoStart)
        {
            timerName = Name;
            Interval = NewInterval;
            TimerTargetTime = DateTime.Now.AddMilliseconds(Interval);
            Text = NewText;
            Elapsed += new ElapsedEventHandler(Callback);
            AutoReset = Loop;
            if (AutoStart) Start();

            AddTimer(TimerLocation, this);
        }

        public TimerHandler(string Name, string? NewText, int Hours, int Minutes, int Seconds, Action<object?, ElapsedEventArgs> Callback, ETimerLocation TimerLocation, bool Loop, bool AutoStart)
        {
            timerName = Name;
            Interval = (Hours * 3600 + Minutes * 60 + Seconds) * 1000;
            TimerTargetTime = DateTime.Now.AddMilliseconds(Interval);
            Text = NewText;
            Elapsed += new ElapsedEventHandler(Callback);
            AutoReset = Loop;
            if (AutoStart) Start();

            AddTimer(TimerLocation, this);
        }

        public TimerHandler(string Name, string? NewText, DateTime TargetTime, Action<object?, ElapsedEventArgs> Callback, ETimerLocation TimerLocation, bool Loop, bool AutoStart)
        {
            timerName = Name;
            Interval = TargetTime.Subtract(DateTime.Now).Minutes <= 5 ? TargetTime.AddDays(1).Subtract(DateTime.Now).TotalMilliseconds : TargetTime.Subtract(DateTime.Now).TotalMilliseconds;
            TimerTargetTime = TargetTime;
            Text = NewText;
            Elapsed += new ElapsedEventHandler(Callback);
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

        public TimeSpan GetDeltaTime()
        {
            return TimerTargetTime.Subtract(DateTime.Now);
        }
    }

    public class DiscordUserTimer : TimerHandler
    {
        public object Guild { get; set; }
        public object User { get; set; }
        public object? TextChannel { get; set; }
        public DiscordUserTimer(object NewGuild, object NewUser, object? OptionalTextChannel, string Name, string? Text, double NewInterval, Action<object?, ElapsedEventArgs> Callback, bool Loop = true, bool AutoStart = true) : base(Name, Text, NewInterval, Callback, ETimerLocation.DiscordBot, Loop, AutoStart)
        {
            Guild = NewGuild;
            User = NewUser;
            TextChannel = OptionalTextChannel;
        }

        public DiscordUserTimer(object NewGuild, object NewUser, object? OptionalTextChannel, string Name, string? Text, DateTime TargetTime, Action<object?, ElapsedEventArgs> Callback, bool Loop = false, bool AutoStart = true) : base(Name, Text, TargetTime, Callback, ETimerLocation.DiscordBot, Loop, AutoStart)
        {
            Guild = NewGuild;
            User= NewUser;
            TextChannel = OptionalTextChannel;
        }

        public DiscordUserTimer(object NewGuild, object NewUser, object? OptionalTextChannel, string Name, string? Text, int Hours, int Minutes, int Seconds, Action<object?, ElapsedEventArgs> Callback, bool Loop = false, bool AutoStart = true) : base(Name, Text, Hours, Minutes, Seconds, Callback, ETimerLocation.DiscordBot, Loop, AutoStart)
        {
            Guild = NewGuild;
            User = NewUser;
            TextChannel = OptionalTextChannel;
        }
    }

    public class UtilityTimer : TimerHandler
    {
        public UtilityTimer(string Name, double NewInterval, Action<object?, ElapsedEventArgs> Callback, ETimerLocation TimerLocation, bool Loop = true, bool AutoStart = true) : base(Name, null, NewInterval, Callback, TimerLocation, Loop, AutoStart)
        {

        }

        public UtilityTimer(string Name, DateTime TargetTime, Action<object?, ElapsedEventArgs> Callback, ETimerLocation TimerLocation, bool Loop = false, bool AutoStart = true) : base(Name, null, TargetTime, Callback, TimerLocation, Loop, AutoStart)
        {

        }

        public UtilityTimer(string Name, int Hours, int Minutes, int Seconds, Action<object?, ElapsedEventArgs> Callback, ETimerLocation TimerLocation, bool Loop = false, bool AutoStart = true) : base(Name, null, Hours, Minutes, Seconds, Callback, TimerLocation, Loop, AutoStart)
        {

        }
    }
}
