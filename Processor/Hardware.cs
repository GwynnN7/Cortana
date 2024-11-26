using System.Device.Gpio;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;
using Iot.Device.CpuTemperature;
using Renci.SshNet;
using UnitsNet;

namespace Processor;

public static class Hardware
{
	private const int RelayPin0 = 25; //Lamp Orvieto
	private const int RelayPin1 = 23; //General/Lamp Pisa
	private const int RelayPin2 = 24; //Computer-OUTLET
	private static readonly Dictionary<EDevice, EStatus> HardwareStates;
	private static readonly NetworkStats NetStats;

	static Hardware()
	{
		NetStats = GetLocationNetworkData();
		HardwareStates = new Dictionary<EDevice, EStatus>();
		foreach (EDevice element in Enum.GetValues<EDevice>()) HardwareStates.Add(element, EStatus.Off);
		_ = new Timer("night-handler", null, new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 1, 0, 0), 
			HandleNightCallback, ETimerType.Utility, ETimerLoop.Daily);
	}

	private static int ComputerPlugsPin => RelayPin2;
	private static int LampPin => NetStats.Location == ELocation.Orvieto ? RelayPin0 : RelayPin1;
	private static int GenericPin => RelayPin1;

	public static string PowerRaspberry(EPowerOption option)
	{
		Task.Run(async () =>
		{
			await Task.Delay(1000);
			switch (option)
			{
				case EPowerOption.Shutdown:
					ScriptRunner("power", "shutdown");
					break;
				case EPowerOption.Reboot:
					ScriptRunner("power", "reboot");
					break;
			}
		});
		return "Command executed";
	}

	public static string HandleRoom(ETrigger state)
	{
		if (state == ETrigger.Off || DateTime.Now.Hour >= 18) PowerLamp(state);
		PowerComputer(state, true);

		return "Command executed";
	}

	public static string PowerLamp(ETrigger state)
	{
		switch (state)
		{
			case ETrigger.On when HardwareStates[EDevice.Lamp] == EStatus.Off:
				if (NetStats.Location == ELocation.Orvieto)
					Task.Run(async () =>
					{
						UseGpio(LampPin, PinValue.High);
						await Task.Delay(200);
						UseGpio(LampPin, PinValue.Low);
					});
				else UseGpio(LampPin, PinValue.High);
				HardwareStates[EDevice.Lamp] = EStatus.On;
				return "Lamp on";
			case ETrigger.Off when HardwareStates[EDevice.Lamp] == EStatus.On:
				if (NetStats.Location == ELocation.Orvieto)
					Task.Run(async () =>
					{
						UseGpio(LampPin, PinValue.High);
						await Task.Delay(200);
						UseGpio(LampPin, PinValue.Low);
					});
				else UseGpio(LampPin, PinValue.Low);
				HardwareStates[EDevice.Lamp] = EStatus.Off;
				return "Lamp off";
			case ETrigger.Toggle:
				return PowerLamp(HardwareStates[EDevice.Lamp] == EStatus.On ? ETrigger.Off : ETrigger.On);
			default:
				return "Already done";
		}
	}

	public static string PowerGeneric(ETrigger state)
	{
		if (NetStats.Location == ELocation.Pisa) return PowerLamp(state);

		switch (state)
		{
			case ETrigger.On:
				UseGpio(GenericPin, PinValue.High);
				HardwareStates[EDevice.Generic] = EStatus.On;
				return "Generic device on";
			case ETrigger.Off:
				UseGpio(GenericPin, PinValue.Low);
				HardwareStates[EDevice.Generic] = EStatus.Off;
				return "Generic device off";
			case ETrigger.Toggle:
			default:
				return PowerGeneric(HardwareStates[EDevice.Generic] == EStatus.On ? ETrigger.Off : ETrigger.On);
		}
	}

	public static string PowerComputer(ETrigger state, bool bPower = false)
	{
		switch (state)
		{
			case ETrigger.On:
				UseGpio(ComputerPlugsPin, PinValue.High);
				HardwareStates[EDevice.Power] = EStatus.On;
				return CommandPc(EComputerCommand.PowerOn);
			case ETrigger.Off:
				return bPower ? RemoveComputerPower() : CommandPc(EComputerCommand.Shutdown);
			case ETrigger.Toggle:
			default:
				return PowerComputer(HardwareStates[EDevice.Computer] == EStatus.On ? ETrigger.Off : ETrigger.On);
		}
	}

	public static string CommandPc(EComputerCommand command, string? args = null)
	{
		string result;
		switch (command)
		{
			case EComputerCommand.PowerOn:
			{
				result = HardwareStates[EDevice.Computer] == EStatus.On
					? "Computer already on"
					: "Computer booting up";
				ScriptRunner("wake-on-lan", NetStats.DesktopLanMac);
				HardwareStates[EDevice.Computer] = EStatus.On;
				break;
			}
			case EComputerCommand.Shutdown:
			{
				SendCommand("shutdown", true, out result);
				result = HardwareStates[EDevice.Computer] == EStatus.Off
					? "Computer already off"
					: result;
				HardwareStates[EDevice.Computer] = EStatus.Off;
				break;
			}
			case EComputerCommand.Reboot:
				SendCommand("reboot", true, out result);
				break;
			case EComputerCommand.Notify:
				SendCommand($"notify {args}", false, out result);
				break;
			default:
				result = "Command not found";
				break;
		}

		return result;
	}

	public static bool SendCommand(string command, bool asRoot, out string result)
	{
		var scriptPath = $"/home/{NetStats.DesktopUsername}/.config/cortana/cortana-script.sh";

		string usr = asRoot ? NetStats.DesktopRoot : NetStats.DesktopUsername;
		string pass = Software.Secrets.DesktopPassword;
		string addr = NetStats.DesktopIp;

		try
		{
			using var client = new SshClient(addr, usr, pass);
			client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(3);
			client.Connect();

			string cmd = $"{scriptPath} {command}".Trim();
			SshCommand r = client.RunCommand(cmd);

			if (r.ExitStatus == 0) result = r.Result.Trim().Length > 0 && !r.Result.Trim().Equals("0") ? r.Result : "Command executed successfully\n";
			else result = r.Error.Trim().Length > 0 ? r.Error : "There was an error executing the command\n";
			result = result.Trim();

			var log = $"Exit Status: {r.ExitStatus}\nResult: {r.Result}Error: {r.Error}\n----\n";
			Software.Log("SSH", log);

			client.Disconnect();

			return r.ExitStatus == 0;
		}
		catch
		{
			result = "Sorry, I couldn't send the command";
			return false;
		}
	}

	private static string RemoveComputerPower()
	{
		if (HardwareStates[EDevice.Computer] == EStatus.On)
		{
			Task.Run(async () =>
			{
				PowerComputer(ETrigger.Off);
				await Task.Delay(1000);

				DateTime start = DateTime.Now;
				while (PingPc() && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

				if ((DateTime.Now - start).Seconds < 3) await Task.Delay(25000);
				else await Task.Delay(5000);

				RemoveComputerPower();
			});
			return "Removing power after computer is off";
		}

		UseGpio(ComputerPlugsPin, PinValue.Low);
		HardwareStates[EDevice.Power] = EStatus.Off;

		return "Power removed";
	}

	public static async Task<string> GetPublicIp()
	{
		using var client = new HttpClient();
		return await client.GetStringAsync("https://api.ipify.org");
	}

	public static string GetCpuTemperature()
	{
		using var cpuTemperature = new CpuTemperature();
		List<(string Sensor, Temperature Temperature)> temperatures = cpuTemperature.ReadTemperatures();
		double average = temperatures.Sum(temp => temp.Temperature.DegreesCelsius);
		average /= temperatures.Count;
		var tempFormat = $"{Math.Round(average, 1).ToString(CultureInfo.InvariantCulture)}°C";
		return tempFormat;
	}

	public static string GetDefaultGateway()
	{
		IEnumerable<string> defaultGateway =
			from netInterfaces in NetworkInterface.GetAllNetworkInterfaces()
			from props in netInterfaces.GetIPProperties().GatewayAddresses
			where netInterfaces.OperationalStatus == OperationalStatus.Up
			select props.Address.ToString();
		return defaultGateway.First();
	}

	public static string GetLocation()
	{
		return NetStats.Location switch
		{
			ELocation.Orvieto => "Orvieto",
			ELocation.Pisa => "Pisa",
			_ => "Unknown"
		};
	}

	public static bool Ping(string ip)
	{
		using var pingSender = new Ping();
		try
		{
			PingReply reply = pingSender.Send(ip, 2000);
			return reply.Status == IPStatus.Success;
		}
		catch
		{
			return false;
		}
	}

	public static string SwitchFromEnum(EDevice device, ETrigger trigger)
	{
		return device switch
		{
			EDevice.Lamp => PowerLamp(trigger),
			EDevice.Computer => PowerComputer(trigger),
			EDevice.Power => PowerComputer(trigger, true),
			EDevice.Generic => PowerGeneric(trigger),
			_ => "Hardware device not listed"
		};
	}

	public static string SwitchFromString(string device, string trigger)
	{
		device = device.ToLower();
		trigger = trigger.ToLower();
		EDevice? elementResult = HardwareElementFromString(device);
		ETrigger? triggerResult = TriggerStateFromString(trigger);
		if (triggerResult == null) return "Invalid action";
		if (elementResult != null) return SwitchFromEnum(elementResult.Value, triggerResult.Value);
		return device == "room" ? HandleRoom(triggerResult.Value) : "Hardware device not listed";
	}


	private static bool PingPc()
	{
		return Ping(NetStats.DesktopIp);
	}

	private static void UseGpio(int pin, PinValue value)
	{
		using var controller = new GpioController();
		controller.OpenPin(pin, PinMode.Output);
		controller.Write(pin, value);
	}

	private static void ScriptRunner(string fileName, string args = "", bool stdRedirect = false)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = "zsh",
			Arguments = $"-c \"Scripts/{fileName}.sh {args}\"",
			RedirectStandardOutput = stdRedirect,
			UseShellExecute = false,
			CreateNoWindow = true
		});
	}

	private static NetworkStats GetLocationNetworkData()
	{
		var orvietoNet = Software.LoadFile<NetworkStats>("Storage/Config/Network/NetworkDataOrvieto.json");
		var pisaNet = Software.LoadFile<NetworkStats>("Storage/Config/Network/NetworkDataPisa.json");
		return GetDefaultGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
	}

	private static void HandleNightCallback(object? sender, EventArgs e)
	{
		if (HardwareStates[EDevice.Computer] == EStatus.Off) PowerLamp(ETrigger.Off);
		else CommandPc(EComputerCommand.Notify, "You should go to sleep");

		if (DateTime.Now.Hour < 6)
			_ = new Timer("safety-night-handler", null, (1, 0, 0), HandleNightCallback, ETimerType.Utility);
	}

	private static ETrigger? TriggerStateFromString(string state)
	{
		state = string.Concat(state[0].ToString().ToUpper(), state.AsSpan(1));
		bool res = Enum.TryParse(state, out ETrigger status);
		return res ? status : null;
	}

	private static EDevice? HardwareElementFromString(string device)
	{
		device = string.Concat(device[0].ToString().ToUpper(), device.AsSpan(1));
		bool res = Enum.TryParse(device, out EDevice status);
		return res ? status : null;
	}
}

[method: JsonConstructor]
public readonly struct NetworkStats(
	ELocation location,
	string cortanaIp,
	string cortanaLanMac,
	string cortanaWlanMac,
	string desktopIp,
	string desktopLanMac,
	string desktopWlanMac,
	string subnetMask,
	string gateway,
	string cortanaUsername,
	string desktopUsername,
	string desktopRoot)
{
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public ELocation Location { get; } = location;

	public string CortanaIp { get; } = cortanaIp;
	public string CortanaLanMac { get; } = cortanaLanMac;
	public string CortanaWlanMac { get; } = cortanaWlanMac;
	public string DesktopIp { get; } = desktopIp;
	public string DesktopLanMac { get; } = desktopLanMac;
	public string DesktopWlanMac { get; } = desktopWlanMac;
	public string SubnetMask { get; } = subnetMask;
	public string Gateway { get; } = gateway;
	public string CortanaUsername { get; } = cortanaUsername;
	public string DesktopUsername { get; } = desktopUsername;
	public string DesktopRoot { get; } = desktopRoot;
}