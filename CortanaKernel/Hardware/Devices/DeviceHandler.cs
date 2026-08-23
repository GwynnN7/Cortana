using System.Device.Gpio;
using CortanaKernel.Hardware.SocketHandler;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Devices;

public static class DeviceHandler
{
	private const int Gpio23 = 23; 
	private const int Gpio24 = 24; 
	private const int Gpio25 = 25; 

	private const int LampPulseMs = 100;

	private static int LampPin => AutomationService.NetworkData.Location == ELocation.Orvieto ? Gpio25 : Gpio24;
	private static int PowerPin => Gpio23;
	private static int GenericPin => Gpio24;

	public static readonly Dictionary<EDevice, EStatus> DeviceStatus = new();

	private static readonly Lock GpioLock = new();
	private static readonly Lock LampLock = new();
	private static GpioController? _controller;
	private static readonly HashSet<int> OpenPins = [];

	public static void LoadDevices()
	{
		foreach (EDevice device in Enum.GetValues<EDevice>()) DeviceStatus.TryAdd(device, EStatus.Off);

		try
		{
			_controller = new GpioController();
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[GPIO] Controller unavailable, running without hardware: {ex.Message}");
		}
	}

	public static void Shutdown()
	{
		lock (GpioLock)
		{
			if (_controller == null) return;
			foreach (int pin in OpenPins)
			{
				try { _controller.ClosePin(pin); } catch {  }
			}
			OpenPins.Clear();
			_controller.Dispose();
			_controller = null;
		}
	}

	public static EStatus PowerLamp(ESwitchAction action)
	{
		switch (action)
		{
			case ESwitchAction.On when DeviceStatus[EDevice.Lamp] == EStatus.Off:
				DriveLamp(PinValue.High);
				DeviceStatus[EDevice.Lamp] = EStatus.On;
				break;
			case ESwitchAction.Off when DeviceStatus[EDevice.Lamp] == EStatus.On:
				DriveLamp(PinValue.Low);
				DeviceStatus[EDevice.Lamp] = EStatus.Off;
				break;
			case ESwitchAction.Toggle:
				return PowerLamp(Helper.ConvertToggle(EDevice.Lamp));
		}

		if (LampPin == GenericPin) DeviceStatus[EDevice.Generic] = DeviceStatus[EDevice.Lamp];
		return DeviceStatus[EDevice.Lamp];
	}

		private static void DriveLamp(PinValue level)
	{
		if (AutomationService.Settings.LampToggle != EStatus.On)
		{
			UseGpio(LampPin, level);
			return;
		}

		int pin = LampPin;
		Task.Run(() =>
		{
			lock (LampLock)
			{
				UseGpio(pin, PinValue.High);
				Thread.Sleep(LampPulseMs);
				UseGpio(pin, PinValue.Low);
			}
		});
	}

	public static EStatus PowerGeneric(ESwitchAction state)
	{
		if (LampPin == GenericPin) return PowerLamp(state);

		switch (state)
		{
			case ESwitchAction.On:
				UseGpio(GenericPin, PinValue.High);
				DeviceStatus[EDevice.Generic] = EStatus.On;
				break;
			case ESwitchAction.Off:
				UseGpio(GenericPin, PinValue.Low);
				DeviceStatus[EDevice.Generic] = EStatus.Off;
				break;
			case ESwitchAction.Toggle:
			default:
				return PowerGeneric(Helper.ConvertToggle(EDevice.Generic));
		}
		return DeviceStatus[EDevice.Generic];
	}

	public static EStatus PowerComputer(ESwitchAction state)
	{
		switch (state)
		{
			case ESwitchAction.On:
				ComputerHandler.Boot();
				return EStatus.On;
			case ESwitchAction.Off:
				ComputerHandler.Shutdown();
				return EStatus.Off;
			case ESwitchAction.Toggle:
			default:
				return PowerComputer(Helper.ConvertToggle(EDevice.Computer));
		}
	}

		public static EStatus PowerComputerSupply(ESwitchAction state)
	{
		switch (state)
		{
			case ESwitchAction.On:
				UseGpio(PowerPin, PinValue.High);
				DeviceStatus[EDevice.Power] = EStatus.On;
				break;
			case ESwitchAction.Off:
				UseGpio(PowerPin, PinValue.Low);
				DeviceStatus[EDevice.Power] = EStatus.Off;
				break;
			case ESwitchAction.Toggle:
			default:
				return PowerComputerSupply(Helper.ConvertToggle(EDevice.Power));
		}
		return DeviceStatus[EDevice.Power];
	}

		private static void UseGpio(int pin, PinValue value)
	{
		lock (GpioLock)
		{
			if (_controller == null) return;
			try
			{
				if (OpenPins.Add(pin)) _controller.OpenPin(pin, PinMode.Output);
				_controller.Write(pin, value);
			}
			catch (Exception ex)
			{
				OpenPins.Remove(pin);
				DataHandler.Log($"[GPIO] Write to pin {pin} failed: {ex.Message}");
			}
		}
	}
}
