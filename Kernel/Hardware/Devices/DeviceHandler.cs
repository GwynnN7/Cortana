using System.Device.Gpio;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.SocketHandler;
using Kernel.Hardware.Utility;

namespace Kernel.Hardware.Devices;

internal static class DeviceHandler
{
	private const int Gpio23 = 23; //Lamp Pisa && Generic
	private const int Gpio24 = 24; //Computer Power
	private const int Gpio25 = 25; //Lamp Orvieto
	
	private static int LampPin => Service.NetworkData.Location == ELocation.Orvieto ? Gpio25 : Gpio23;
	private static int PowerPin => Gpio24;
	private static int GenericPin => Gpio23;
	
	internal static readonly Dictionary<EDevice, EPower> HardwareStates;
	private static readonly Lock LampLock = new();

	static DeviceHandler()
	{
		HardwareStates = new Dictionary<EDevice, EPower>();
        foreach (EDevice device in Enum.GetValues<EDevice>()) HardwareStates.Add(device, EPower.Off);
	}

	internal static EPower PowerLamp(EPowerAction state)
	{
		switch (state)
		{
			case EPowerAction.On when HardwareStates[EDevice.Lamp] == EPower.Off:
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
				HardwareStates[EDevice.Lamp] = EPower.On;
				break;
			case EPowerAction.Off when HardwareStates[EDevice.Lamp] == EPower.On:
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
				HardwareStates[EDevice.Lamp] = EPower.Off;
				break;
			case EPowerAction.Toggle:
				return PowerLamp(Helper.ConvertToggle(EDevice.Lamp));
		}
		if (LampPin == GenericPin) HardwareStates[EDevice.Generic] = HardwareStates[EDevice.Lamp];
		return HardwareStates[EDevice.Lamp];
	}

	internal static EPower PowerGeneric(EPowerAction state)
	{
		if (LampPin == GenericPin) return PowerLamp(state);

		switch (state)
		{
			case EPowerAction.On:
				UseGpio(GenericPin, PinValue.High);
				HardwareStates[EDevice.Generic] = EPower.On;
				break;
			case EPowerAction.Off:
				UseGpio(GenericPin, PinValue.Low);
				HardwareStates[EDevice.Generic] = EPower.Off;
				break;
			case EPowerAction.Toggle:
			default:
				return PowerGeneric(Helper.ConvertToggle(EDevice.Generic));
		}
		return HardwareStates[EDevice.Generic];
	}

	internal static EPower PowerComputer(EPowerAction state)
	{
		switch (state)
		{
			case EPowerAction.On:
				ComputerHandler.Boot();
				return EPower.On;
			case EPowerAction.Off:
				ComputerHandler.Shutdown();
				return EPower.Off;
			case EPowerAction.Toggle:
			default:
				return PowerComputer(Helper.ConvertToggle(EDevice.Computer));
		}
	}
	
	internal static EPower PowerComputerSupply(EPowerAction state)
	{
		switch (state)
		{
			case EPowerAction.On:
				UseGpio(PowerPin, PinValue.High);
				HardwareStates[EDevice.Power] = EPower.On;
				break;
			case EPowerAction.Off:
				UseGpio(PowerPin, PinValue.Low);
				HardwareStates[EDevice.Power] = EPower.Off;
				break;
			case EPowerAction.Toggle:
			default:
				return PowerComputerSupply(Helper.ConvertToggle(EDevice.Power));
		}
		return HardwareStates[EDevice.Power];
	}

	private static void UseGpio(int pin, PinValue value)
	{
		using var controller = new GpioController();
		controller.OpenPin(pin, PinMode.Output);
		controller.Write(pin, value);
		controller.ClosePin(pin);
	}
}

