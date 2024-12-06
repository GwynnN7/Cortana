using System.Device.Gpio;
using Kernel.Hardware.Utility;

namespace Kernel.Hardware;

internal static class DeviceHandler
{
	private const int RelayPin0 = 25; //Lamp Orvieto
	private const int RelayPin1 = 23; //Generic (Lamp Pisa)
	private const int RelayPin2 = 24; //Computer Power
	
	internal static readonly Dictionary<EDevice, EPower> HardwareStates;
	private static int LampPin => NetworkAdapter.Location == ELocation.Orvieto ? RelayPin0 : RelayPin1;
	private static int ComputerPlugsPin => RelayPin2;
	private static int GenericPin => RelayPin1;

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
				if (NetworkAdapter.Location == ELocation.Orvieto)
					Task.Run(async () =>
					{
						UseGpio(LampPin, PinValue.High);
						await Task.Delay(200);
						UseGpio(LampPin, PinValue.Low);
					});
				else UseGpio(LampPin, PinValue.High);
				HardwareStates[EDevice.Lamp] = EPower.On;
				break;
			case EPowerAction.Off when HardwareStates[EDevice.Lamp] == EPower.On:
				if (NetworkAdapter.Location == ELocation.Orvieto)
					Task.Run(async () =>
					{
						UseGpio(LampPin, PinValue.High);
						await Task.Delay(200);
						UseGpio(LampPin, PinValue.Low);
					});
				else UseGpio(LampPin, PinValue.Low);
				HardwareStates[EDevice.Lamp] = EPower.Off;
				break;
			case EPowerAction.Toggle:
				return PowerLamp(Helper.ConvertToggle(EDevice.Lamp));
		}
		return HardwareStates[EDevice.Lamp];
	}

	internal static EPower PowerGeneric(EPowerAction state)
	{
		if (NetworkAdapter.Location == ELocation.Pisa) return PowerLamp(state);

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
				ComputerService.Boot();
				break;
			case EPowerAction.Off:
				ComputerService.Shutdown();
				break;
			case EPowerAction.Toggle:
			default:
				return PowerComputer(Helper.ConvertToggle(EDevice.Computer));
		}
		return HardwareStates[EDevice.Computer];
	}
	
	internal static EPower PowerComputerSupply(EPowerAction state)
	{
		switch (state)
		{
			case EPowerAction.On:
				UseGpio(ComputerPlugsPin, PinValue.High);
				HardwareStates[EDevice.Power] = EPower.On;
				break;
			case EPowerAction.Off:
				UseGpio(ComputerPlugsPin, PinValue.Low);
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
	}
}

