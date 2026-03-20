using CortanaKernel.Hardware.Devices;
using CortanaKernel.Hardware.SocketHandler;
using CortanaKernel.Hardware.Structures;
using CortanaKernel.Kernel;
using CortanaLib;
using CortanaLib.Structures;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaKernel.Hardware.Utility;

public static class Service
{
	public static readonly NetworkData NetworkData;
	public static readonly Settings Settings;

	private static Timer? _controllerTimer;
	private static Timer? _temporaryTimer;
	private static bool _morningMessage;

	static Service()
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
		ResetControllerTimer();
	}

	private static void ResetControllerTimer()
	{
		_controllerTimer?.Destroy();
		_controllerTimer = new Timer("controller-timer", null, ControllerCallback, ETimerType.Utility);
		DateTime time = DateTime.Now.Minute >= 15 && DateTime.Now.Minute < 45 ?
			DateTime.Today.AddHours(DateTime.Now.Hour + 1) :
			DateTime.Today.AddHours(DateTime.Now.Hour).AddMinutes(30);
		if (time < DateTime.Now) time = time.AddHours(1);
		_controllerTimer.Set(time);
	}

	private static void ResetTemporaryTimer()
	{
		_temporaryTimer?.Destroy();
		_temporaryTimer = new Timer("temporary-timer", null, EnableAutomaticMode, ETimerType.Utility);
		_temporaryTimer.Set(DateTime.Now.AddHours(1));
	}

	private static Task ControllerCallback(object? sender)
	{
		ResetControllerTimer();

		if (DateTime.Now.Hour < Settings.MorningHour)
		{
			_morningMessage = true;

			if (HardwareApi.Devices.GetPower(EDevice.Computer) == EPowerStatus.Off)
			{
				EnterSleepMode();
				return Task.CompletedTask;
			}

			if (DateTime.Now.Hour % 2 != 0 && DateTime.Now.Hour < 6)
			{
				IpcService.Publish(EMessageCategory.Telegram, "You should go to sleep");
			}
		}
		else
		{
			if (!_morningMessage) return Task.CompletedTask;
			IpcService.Publish(EMessageCategory.Telegram, "Good morning, enabling Automatic mode");
			Settings.AutomaticMode = EMotionDetection.On;
			_morningMessage = false;
		}

		return Task.CompletedTask;
	}

	public static void TemporaryManualMode()
	{
		if (Settings.AutomaticMode == EMotionDetection.Off) return;
		Settings.AutomaticMode = EMotionDetection.Off;
		ResetControllerTimer();
		IpcService.Publish(EMessageCategory.Telegram, "Manual mode temporary enabled");
	}

	private static Task EnableAutomaticMode(object? sender = null)
	{
		_temporaryTimer?.Destroy();
		_temporaryTimer = null;
		Settings.AutomaticMode = EMotionDetection.On;
		IpcService.Publish(EMessageCategory.Telegram, "Automatic mode re-enabled");

		return Task.CompletedTask;
	}

	public static void EnterSleepMode(bool userAction = false)
	{
		Settings.AutomaticMode = EMotionDetection.Off;

		switch (userAction)
		{
			case true:
				ResetControllerTimer();
				break;
			case false when HardwareApi.Devices.GetPower(EDevice.Lamp) == EPowerStatus.Off:
				return;
		}

		IpcService.Publish(EMessageCategory.Telegram, "Good night, enabling Manual mode until morning");
		HardwareApi.Devices.Switch(EDevice.Lamp, EPowerAction.Off);
	}

	public static void InterruptController()
	{
		_controllerTimer?.Destroy();
	}

	public static void ComputerStatusUpdated(EPowerStatus status)
	{
		Settings.AutomaticMode = EMotionDetection.On;
		ResetControllerTimer();
	}
}