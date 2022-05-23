using System.Timers;

namespace Utility
{
    public abstract class TimerHandler : System.Timers.Timer
    {
        public static Dictionary<ETimerLocation, List<TimerHandler>> TotalTimers;

        protected string Name;
        public string? Text { get; set; }
        
        public DateTime NextTargetTime;
        public TimeSpan NextDeltaTime
        {
            get{ return NextTargetTime.Subtract(DateTime.Now); }
        }
        public ETimerLoop LoopType { get; set; }
        protected Action<object?, ElapsedEventArgs> Callback;
        protected ETimerLocation TimerLocation;
        

        public TimerHandler(string Name, string? Text, int Hours, int Minutes, int Seconds, Action<object?, ElapsedEventArgs> Callback, ETimerLocation TimerLocation, ETimerLoop Loop, bool AutoStart)
        {
            this.Name = Name;
            this.Text = Text;
            this.Interval = (Hours * 3600 + Minutes * 60 + Seconds) * 1000;
            this.Callback = Callback;
            this.TimerLocation = TimerLocation;

            LoopType = Loop;
            NextTargetTime = DateTime.Now.AddMilliseconds(Interval);

            Elapsed += new ElapsedEventHandler(TimerElapsed);
            AutoReset = false;
            if (AutoStart) Start();

            AddTimer(TimerLocation, this);
        }

        public TimerHandler(string Name, string? Text, DateTime TargetTime, Action<object?, ElapsedEventArgs> Callback, ETimerLocation TimerLocation, ETimerLoop Loop, bool AutoStart)
        {
            this.Name = Name;
            this.Text = Text;
            this.Interval = TargetTime.Subtract(DateTime.Now).Minutes <= 5 ? TargetTime.AddDays(1).Subtract(DateTime.Now).TotalMilliseconds : TargetTime.Subtract(DateTime.Now).TotalMilliseconds;
            this.Callback = Callback;
            this.TimerLocation = TimerLocation;

            LoopType = Loop;
            NextTargetTime = DateTime.Now.AddMilliseconds(Interval);

            Elapsed += new ElapsedEventHandler(TimerElapsed);
            AutoReset = false;
            if (AutoStart) Start();

            AddTimer(TimerLocation, this);
        }

        private void TimerElapsed(object? sender, ElapsedEventArgs args)
        {
            if (LoopType != ETimerLoop.No)
            {
                if (LoopType == ETimerLoop.Quotidiano) NextTargetTime = DateTime.Now.AddDays(1);
                else if (LoopType == ETimerLoop.Settimanalmente) NextTargetTime = DateTime.Now.AddDays(7);
                else NextTargetTime = DateTime.Now.AddMilliseconds(Interval);

                var newInterval = NextTargetTime.Subtract(DateTime.Now).TotalMilliseconds;

                Interval = newInterval;
                Enabled = true;
                Start();
            }

            Callback.Invoke(sender, args);

            if(LoopType == ETimerLoop.No) RemoveTimer(this);
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
                if (TimerList.Value.Contains(Timer))
                {
                    TotalTimers[TimerList.Key].Remove(Timer);
                    Timer.Destroy();
                }
            }
        }

        public static void RemoveTimers(ETimerLocation Location)
        {
            if (TotalTimers == null) return;
            if (!TotalTimers.ContainsKey(Location)) return;
            
            foreach(var LocationTimer in TotalTimers[Location].ToList())
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
                    if (LocationTimer.Name == name)
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
                        if (ListTimer.Name == name)
                        {
                            RemoveTimer(ListTimer);
                            return;
                        }
                    }
                }
            }
        }
    }

    public class DiscordUserTimer : TimerHandler
    {
        public object Guild { get; set; }
        public object User { get; set; }
        public object? TextChannel { get; set; }

        public DiscordUserTimer(object Guild, object User, object? TextChannel, string Name, string? Text, DateTime TargetTime, Action<object?, ElapsedEventArgs> Callback, ETimerLoop Loop = ETimerLoop.No, bool AutoStart = true) : base(Name, Text, TargetTime, Callback, ETimerLocation.DiscordBot, Loop, AutoStart)
        {
            this.Guild = Guild;
            this.User = User;
            this.TextChannel = TextChannel;
        }

        public DiscordUserTimer(object Guild, object User, object? TextChannel, string Name, string? Text, int Hours, int Minutes, int Seconds, Action<object?, ElapsedEventArgs> Callback, ETimerLoop Loop = ETimerLoop.No, bool AutoStart = true) : base(Name, Text, Hours, Minutes, Seconds, Callback, ETimerLocation.DiscordBot, Loop, AutoStart)
        {
            this.Guild = Guild;
            this.User = User;
            this.TextChannel = TextChannel;
        }
    }

    public class UtilityTimer : TimerHandler
    {
        public UtilityTimer(string Name, DateTime TargetTime, Action<object?, ElapsedEventArgs> Callback, ETimerLocation TimerLocation, ETimerLoop Loop = ETimerLoop.No, bool AutoStart = true) : base(Name, null, TargetTime, Callback, TimerLocation, Loop, AutoStart) { }
        public UtilityTimer(string Name, int Hours, int Minutes, int Seconds, Action<object?, ElapsedEventArgs> Callback, ETimerLocation TimerLocation, ETimerLoop Loop = ETimerLoop.No, bool AutoStart = true) : base(Name, null, Hours, Minutes, Seconds, Callback, TimerLocation, Loop, AutoStart) { }

    }
}
