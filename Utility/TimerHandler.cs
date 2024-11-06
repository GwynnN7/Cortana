using System.Timers;

namespace Utility
{
    public abstract class TimerHandler : System.Timers.Timer
    {
        private static readonly Dictionary<ETimerLocation, List<TimerHandler>> TotalTimers = new();

        private readonly string _name;
        public string? Text { get; set; }
        
        public DateTime NextTargetTime;
        private ETimerLoop LoopType { get; }
        private readonly Action<object?, ElapsedEventArgs> _callback;
        protected ETimerLocation TimerLocation;


        protected TimerHandler(string name, string? text, int hours, int minutes, int seconds, Action<object?, ElapsedEventArgs> callback, ETimerLocation timerLocation, ETimerLoop loop, bool autoStart)
        {
            _name = name;
            Text = text;
            Interval = (hours * 3600 + minutes * 60 + seconds) * 1000;
            _callback = callback;
            TimerLocation = timerLocation;

            LoopType = loop;
            NextTargetTime = DateTime.Now.AddMilliseconds(Interval);

            Elapsed += TimerElapsed;
            AutoReset = false;
            if (autoStart) Start();

            AddTimer(timerLocation, this);
        }

        protected TimerHandler(string name, string? text, DateTime targetTime, Action<object?, ElapsedEventArgs> callback, ETimerLocation timerLocation, ETimerLoop loop, bool autoStart)
        {
            _name = name;
            Text = text;
            Interval = targetTime.Subtract(DateTime.Now).Minutes <= 5 ? targetTime.AddDays(1).Subtract(DateTime.Now).TotalMilliseconds : targetTime.Subtract(DateTime.Now).TotalMilliseconds;
            _callback = callback;
            TimerLocation = timerLocation;

            LoopType = loop;
            NextTargetTime = DateTime.Now.AddMilliseconds(Interval);

            Elapsed += TimerElapsed;
            AutoReset = false;
            if (autoStart) Start();

            AddTimer(timerLocation, this);
        }

        private void TimerElapsed(object? sender, ElapsedEventArgs args)
        {
            if (LoopType != ETimerLoop.No)
            {
                NextTargetTime = LoopType switch
                {
                    ETimerLoop.Daily => DateTime.Now.AddDays(1),
                    ETimerLoop.Weekly => DateTime.Now.AddDays(7),
                    _ => DateTime.Now.AddMilliseconds(Interval)
                };

                double newInterval = NextTargetTime.Subtract(DateTime.Now).TotalMilliseconds;

                Interval = newInterval;
                Enabled = true;
                Start();
            }

            _callback.Invoke(sender, args);

            if(LoopType == ETimerLoop.No) RemoveTimer(this);
        }

        private void Destroy()
        {
            Stop();
            Dispose();
        }

        private static void AddTimer(ETimerLocation timerLocation, TimerHandler timer)
        {
            if(!TotalTimers.TryAdd(timerLocation, [timer]))
            {
                TotalTimers[timerLocation].Add(timer);
            }
        }

        private static void RemoveTimer(TimerHandler timer)
        {
            foreach((ETimerLocation timerLocation, List<TimerHandler>? timerList) in TotalTimers)
            {
                if (!timerList.Contains(timer)) continue;
                TotalTimers[timerLocation].Remove(timer);
                timer.Destroy();
                break;
            }
        }

        public static void RemoveTimers(ETimerLocation location)
        {
            foreach ((ETimerLocation timerLocation, List<TimerHandler>? timerHandlers) in TotalTimers)
            {
                if(timerLocation != location) continue;
                foreach (TimerHandler listTimer in timerHandlers) RemoveTimer(listTimer);
            }
        }

        public static void RemoveTimerByName(string name)
        {
            foreach ((_, List<TimerHandler>? timerHandlers) in TotalTimers)
            {
                foreach (TimerHandler listTimer in timerHandlers.Where(listTimer => listTimer._name == name))
                {
                    RemoveTimer(listTimer);
                    return;
                }
            }
        }
    }

    public class DiscordUserTimer : TimerHandler
    {
        public object Guild { get; set; }
        public object User { get; }
        public object? TextChannel { get; }

        public DiscordUserTimer(object guild, object user, object? textChannel, string name, string? text, DateTime targetTime, Action<object?, ElapsedEventArgs> callback, ETimerLoop loop = ETimerLoop.No, bool autoStart = true) : base(name, text, targetTime, callback, ETimerLocation.DiscordBot, loop, autoStart)
        {
            Guild = guild;
            User = user;
            TextChannel = textChannel;
        }

        public DiscordUserTimer(object guild, object user, object? textChannel, string name, string? text, int hours, int minutes, int seconds, Action<object?, ElapsedEventArgs> callback, ETimerLoop loop = ETimerLoop.No, bool autoStart = true) : base(name, text, hours, minutes, seconds, callback, ETimerLocation.DiscordBot, loop, autoStart)
        {
            Guild = guild;
            User = user;
            TextChannel = textChannel;
        }
    }

    public class UtilityTimer : TimerHandler
    {
        public UtilityTimer(string name, DateTime targetTime, Action<object?, ElapsedEventArgs> callback, ETimerLocation timerLocation, ETimerLoop loop = ETimerLoop.No, bool autoStart = true) : base(name, null, targetTime, callback, timerLocation, loop, autoStart) { }
        public UtilityTimer(string name, int hours, int minutes, int seconds, Action<object?, ElapsedEventArgs> callback, ETimerLocation timerLocation, ETimerLoop loop = ETimerLoop.No, bool autoStart = true) : base(name, null, hours, minutes, seconds, callback, timerLocation, loop, autoStart) { }

    }
}
