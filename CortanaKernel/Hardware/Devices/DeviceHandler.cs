using System.Device.Gpio;
using CortanaKernel.Hardware.SocketHandler;
using CortanaKernel.Hardware.Structures;
using CortanaKernel.Hardware.Utility;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Devices;

public static class DeviceHandler
{
	private const int Gpio23 = 23; //Lamp Pisa && Generic
	private const int Gpio24 = 24; //Computer Power
	private const int Gpio25 = 25; //Lamp Orvieto
	
	private static int LampPin => Service.NetworkData.Location == ELocation.Orvieto ? Gpio25 : Gpio23;
	private static int PowerPin => Gpio24;
	private static int GenericPin => Gpio23;
	
	public static readonly Dictionary<EDevice, EPowerStatus> DeviceStatus;
	private static readonly Lock LampLock = new();

	static DeviceHandler()
	{
		DeviceStatus = new Dictionary<EDevice, EPowerStatus>();
        foreach (EDevice device in Enum.GetValues<EDevice>()) DeviceStatus.Add(device, EPowerStatus.Off);
	}

	public static EPowerStatus PowerLamp(EPowerAction state)
	{
		switch (state)
		{
			case EPowerAction.On when DeviceStatus[EDevice.Lamp] == EPowerStatus.Off:
				if (Service.NetworkData.Location == ELocation.Orvieto)
					Task.Run(() =>
					{
						lock (LampLock)
						{
							UseGpio(LampPin, PinValue.High);
							Thread.Sleep(100);
							UseGpio(LampPin, PinValue.Low);
						}
					});
				else UseGpio(LampPin, PinValue.High);
				DeviceStatus[EDevice.Lamp] = EPowerStatus.On;
				break;
			case EPowerAction.Off when DeviceStatus[EDevice.Lamp] == EPowerStatus.On:
				if (Service.NetworkData.Location == ELocation.Orvieto)
					Task.Run(() =>
					{
						lock (LampLock)
						{
							UseGpio(LampPin, PinValue.High);
							Thread.Sleep(100);
							UseGpio(LampPin, PinValue.Low);
						}
					});
				else UseGpio(LampPin, PinValue.Low);
				DeviceStatus[EDevice.Lamp] = EPowerStatus.Off;
				break;
			case EPowerAction.Toggle:
				return PowerLamp(Helper.ConvertToggle(EDevice.Lamp));
		}
		if (LampPin == GenericPin) DeviceStatus[EDevice.Generic] = DeviceStatus[EDevice.Lamp];
		return DeviceStatus[EDevice.Lamp];
	}

	public static EPowerStatus PowerGeneric(EPowerAction state)
	{
		if (LampPin == GenericPin) return PowerLamp(state);

		switch (state)
		{
			case EPowerAction.On:
				UseGpio(GenericPin, PinValue.High);
				DeviceStatus[EDevice.Generic] = EPowerStatus.On;
				break;
			case EPowerAction.Off:
				UseGpio(GenericPin, PinValue.Low);
				DeviceStatus[EDevice.Generic] = EPowerStatus.Off;
				break;
			case EPowerAction.Toggle:
			default:
				return PowerGeneric(Helper.ConvertToggle(EDevice.Generic));
		}
		return DeviceStatus[EDevice.Generic];
	}

	public static EPowerStatus PowerComputer(EPowerAction state)
	{
		switch (state)
		{
			case EPowerAction.On:
				ComputerHandler.Boot();
				return EPowerStatus.On;
			case EPowerAction.Off:
				ComputerHandler.Shutdown();
				return EPowerStatus.Off;
			case EPowerAction.Toggle:
			default:
				return PowerComputer(Helper.ConvertToggle(EDevice.Computer));
		}
	}
	
	public static EPowerStatus PowerComputerSupply(EPowerAction state)
	{
		switch (state)
		{
			case EPowerAction.On:
				UseGpio(PowerPin, PinValue.High);
				DeviceStatus[EDevice.Power] = EPowerStatus.On;
				break;
			case EPowerAction.Off:
				UseGpio(PowerPin, PinValue.Low);
				DeviceStatus[EDevice.Power] = EPowerStatus.Off;
				break;
			case EPowerAction.Toggle:
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

