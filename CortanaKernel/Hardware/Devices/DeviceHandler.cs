using System.Device.Gpio;
using CortanaKernel.Hardware.SocketHandler;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Devices;

public static class DeviceHandler
{
	private const int Gpio23 = 23; //Computer Power
	private const int Gpio24 = 24; //Lamp Pisa && Generic
	private const int Gpio25 = 25; //Lamp Orvieto

	private static int LampPin => Service.NetworkData.Location == ELocation.Orvieto ? Gpio25 : Gpio24;
	private static int PowerPin => Gpio23;
	private static int GenericPin => Gpio24;

	public static readonly Dictionary<EDevice, EStatus> DeviceStatus;
	private static readonly Lock LampLock = new();

	static DeviceHandler()
	{
		DeviceStatus = new Dictionary<EDevice, EStatus>();
	}

	public static void LoadDevices()
	{
		foreach (EDevice device in Enum.GetValues<EDevice>()) DeviceStatus.Add(device, EStatus.Off);
	}

	public static EStatus PowerLamp(ESwitchAction action)
	{
		switch (action)
		{
			case ESwitchAction.On when DeviceStatus[EDevice.Lamp] == EStatus.Off:
				if (Service.Settings.LampToggle == EStatus.On)
				{
					Task.Run(() =>
					{
						lock (LampLock)
						{
							UseGpio(LampPin, PinValue.High);
							Thread.Sleep(100);
							UseGpio(LampPin, PinValue.Low);
						}
					});
				}
				else UseGpio(LampPin, PinValue.High);
				DeviceStatus[EDevice.Lamp] = EStatus.On;
				break;
			case ESwitchAction.Off when DeviceStatus[EDevice.Lamp] == EStatus.On:
				if (Service.Settings.LampToggle == EStatus.On)
				{
					Task.Run(() =>
					{
						lock (LampLock)
						{
							UseGpio(LampPin, PinValue.High);
							Thread.Sleep(100);
							UseGpio(LampPin, PinValue.Low);
						}
					});
				}
				else UseGpio(LampPin, PinValue.Low);
				DeviceStatus[EDevice.Lamp] = EStatus.Off;
				break;
			case ESwitchAction.Toggle:
				return PowerLamp(Helper.ConvertToggle(EDevice.Lamp));
		}
		if (LampPin == GenericPin) DeviceStatus[EDevice.Generic] = DeviceStatus[EDevice.Lamp];
		return DeviceStatus[EDevice.Lamp];
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
		using var controller = new GpioController();
		controller.OpenPin(pin, PinMode.Output);
		controller.Write(pin, value);
		controller.ClosePin(pin);
	}
}

