using CortanaKernel.Hardware.Devices;
using CortanaKernel.Hardware.SocketHandler;
using CortanaKernel.Hardware.Structures;
using CortanaKernel.Kernel;
using CortanaLib;
using CortanaLib.Structures;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaKernel.Hardware.Utility;

public static class AutomationService
{
	public static readonly NetworkData NetworkData;
	public static readonly Settings Settings;

	private static readonly Lock StateLock = new();

	private static Timer? _boundaryTimer;
	private static Timer? _manualTimer;

		private static DateTime? _manualUntil;

		private static bool _nightApplied;
		private static bool _sleeping;

		private static bool _nightDeferred;

	static AutomationService()
	{
		var orvietoNet = DataHandler.DeserializeJson<NetworkData>(DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/NetworkDataOrvieto.json"));
		var pisaNet = DataHandler.DeserializeJson<NetworkData>(DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/NetworkDataPisa.json"));

		NetworkData = RaspberryHandler.GetNetworkGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
		Settings = Settings.Load();
	}

	public static void Start()
	{
		Task.Run(ServerHandler.StartListening)
			.ContinueWith(t => DataHandler.Log($"Server listener error: {t.Exception}"), TaskContinuationOptions.OnlyOnFaulted);

		_nightApplied = IsNight(DateTime.Now);
		ScheduleNextBoundary();
	}

	public static void Interrupt()
	{
		lock (StateLock)
		{
			_boundaryTimer?.Destroy();
			_boundaryTimer = null;
			_manualTimer?.Destroy();
			_manualTimer = null;
		}
	}

		public static bool IsNight(DateTime now)
	{
		int hour = now.Hour;
		int night = Settings.NightHour;
		int morning = Settings.MorningHour;

		if (night == morning) return false;
		return night < morning ? hour >= night && hour < morning : hour >= night || hour < morning;
	}

	public static EAutomationState State
	{
		get
		{
			if (Settings.AutomaticMode == EStatus.Off) return EAutomationState.Manual;
			lock (StateLock)
			{
				if (_manualUntil.HasValue && DateTime.Now < _manualUntil.Value) return EAutomationState.Manual;
			}
			return _sleeping || IsNight(DateTime.Now) ? EAutomationState.Night : EAutomationState.Automatic;
		}
	}

	public static int ManualMinutesLeft
	{
		get
		{
			lock (StateLock)
			{
				if (!_manualUntil.HasValue) return 0;

				double left = (_manualUntil.Value - DateTime.Now).TotalMinutes;
				return left <= 0 ? 0 : (int)Math.Ceiling(left);
			}
		}
	}

		public static bool CanAutoLight => State == EAutomationState.Automatic;

		public static bool CanAutoExtinguish => State != EAutomationState.Manual;

		public static int MotionOffSeconds =>
		IsNight(DateTime.Now) || HardwareApi.Devices.GetPower(EDevice.Computer) == EStatus.Off
			? Settings.MotionOffMin
			: Settings.MotionOffMax;

	private static void ScheduleNextBoundary()
	{
		lock (StateLock)
		{
			_boundaryTimer?.Destroy();
			_boundaryTimer = new Timer("automation-boundary", null, BoundaryReached, ETimerType.Utility);
			_boundaryTimer.Set(NextBoundary(DateTime.Now));
		}
	}

		private static DateTime NextBoundary(DateTime now)
	{
		DateTime night = NextOccurrenceOf(now, Settings.NightHour);
		DateTime morning = NextOccurrenceOf(now, Settings.MorningHour);
		return night < morning ? night : morning;
	}

	private static DateTime NextOccurrenceOf(DateTime now, int hour)
	{
		DateTime candidate = now.Date.AddHours(hour);
		return candidate <= now ? candidate.AddDays(1) : candidate;
	}

	private static Task BoundaryReached(object? sender)
	{
		ScheduleNextBoundary();

		bool night = IsNight(DateTime.Now);
		if (night == _nightApplied) return Task.CompletedTask;

		_nightApplied = night;
		ScheduleService.RaiseEvent(night ? EScheduleEvent.NightStart : EScheduleEvent.MorningStart);

		if (night) ApplyNight("Good night, holding the lamp until morning", force: false);
		else ApplyMorning();

		return Task.CompletedTask;
	}

	private static void ApplyNight(string message, bool force)
	{
		ClearManualHold();

		if (!force && HardwareApi.Devices.GetPower(EDevice.Computer) == EStatus.On)
		{
			_nightDeferred = true;
			ComputerHandler.Notify("It's late, you should go to sleep.");
			Notifier.Send(ELogSource.Automation, "Night hour reached, but the computer is still on");
			return;
		}

		_nightDeferred = false;

		if (HardwareApi.Devices.GetPower(EDevice.Lamp) == EStatus.Off) return;

		Notifier.Send(ELogSource.Automation, message);
		HardwareApi.Devices.Switch(EDevice.Lamp, ESwitchAction.Off, automatic: true);
	}

	private static void ApplyMorning()
	{
		ClearManualHold();
		_nightDeferred = false;
		_sleeping = false;

		Settings.AutomaticMode = EStatus.On;
		Settings.Save();
		Notifier.Send(ELogSource.Automation, "Good morning, automatic mode is back on");
	}

		public static void TemporaryManualMode()
	{
		if (Settings.AutomaticMode == EStatus.Off) return;

		lock (StateLock)
		{
			bool wasActive = _manualUntil.HasValue && DateTime.Now < _manualUntil.Value;
			_manualUntil = DateTime.Now.AddMinutes(Settings.ManualModeMinutes);

			_manualTimer?.Destroy();
			_manualTimer = new Timer("automation-manual", null, ManualExpired, ETimerType.Utility);
			_manualTimer.Set((0, Settings.ManualModeMinutes, 0));

			if (wasActive) return;
		}

		Notifier.Send(ELogSource.Automation, $"Manual mode for {Settings.ManualModeMinutes} minutes");
	}

	private static Task ManualExpired(object? sender)
	{
		lock (StateLock)
		{
			_manualUntil = null;
			_manualTimer = null;
		}

		if (IsNight(DateTime.Now)) return Task.CompletedTask;

		Notifier.Send(ELogSource.Automation, "Automatic mode re-enabled");
		return Task.CompletedTask;
	}

	public static void WakeUp() => _sleeping = false;

	public static void ClearManualHold()
	{
		lock (StateLock)
		{
			_manualTimer?.Destroy();
			_manualTimer = null;
			_manualUntil = null;
		}
	}

	public static void EnterSleepMode()
	{
		_nightApplied = true;
		_sleeping = true;
		ApplyNight("Good night, switching everything off", force: true);
	}

	public static void ComputerStatusUpdated()
	{
		ClearManualHold();

		if (!_nightDeferred || HardwareApi.Devices.GetPower(EDevice.Computer) == EStatus.On) return;
		if (!IsNight(DateTime.Now)) { _nightDeferred = false; return; }

		ApplyNight("Computer off and it's night, switching the lamp off", force: true);
	}
}
