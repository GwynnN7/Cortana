using System.ComponentModel;
using CortanaKernel.Hardware;
using CortanaLib.Structures;
using Microsoft.Extensions.AI;

namespace CortanaKernel.Kernel;

public static class LlmTools
{
	private const string RoomAlias = "room";

	private const string UntrustedPrefix = "discord:";

	public static IReadOnlyDictionary<string, AIFunction> All { get; } = new Dictionary<string, AIFunction>
	{
		[nameof(GetDevices)] = AIFunctionFactory.Create(GetDevices),
		[nameof(SwitchDevice)] = AIFunctionFactory.Create(SwitchDevice),
		[nameof(EnterSleepMode)] = AIFunctionFactory.Create(EnterSleepMode),
		[nameof(GetSensors)] = AIFunctionFactory.Create(GetSensors),
		[nameof(GetSettings)] = AIFunctionFactory.Create(GetSettings),
		[nameof(SetSetting)] = AIFunctionFactory.Create(SetSetting),
		[nameof(GetComputerMetrics)] = AIFunctionFactory.Create(GetComputerMetrics),
		[nameof(GetRaspberryMetrics)] = AIFunctionFactory.Create(GetRaspberryMetrics),
		[nameof(GetHistory)] = AIFunctionFactory.Create(GetHistory),
		[nameof(RunOnComputer)] = AIFunctionFactory.Create(RunOnComputer),
		[nameof(LaunchOnComputer)] = AIFunctionFactory.Create(LaunchOnComputer),
		[nameof(RunOnRaspberry)] = AIFunctionFactory.Create(RunOnRaspberry)
	};

	private static readonly string[] Harmless =
	[
		nameof(GetDevices), nameof(GetSensors), nameof(GetSettings),
		nameof(GetComputerMetrics), nameof(GetRaspberryMetrics), nameof(GetHistory)
	];

	public static IReadOnlyDictionary<string, AIFunction> ReadOnly { get; } =
		All.Where(tool => Harmless.Contains(tool.Key)).ToDictionary(tool => tool.Key, tool => tool.Value);

	public static IReadOnlyDictionary<string, AIFunction> For(string conversation) =>
		conversation.StartsWith(UntrustedPrefix, StringComparison.OrdinalIgnoreCase) ? ReadOnly : All;

	private static string Names<T>() where T : struct, Enum => string.Join(", ", Enum.GetNames<T>());

	[Description("Performance and temperatures of the desktop computer: CPU, RAM, GPU, disk and uptime.")]
	private static string GetComputerMetrics() =>
		MetricsStore.Latest().Match(
			metrics => metrics.Stale
				? $"{MetricsStore.Render(metrics)}\nThese numbers are old, the computer stopped reporting."
				: MetricsStore.Render(metrics),
			() => "The computer has not reported any metrics yet");

	[Description("Performance and temperature of the Raspberry Pi that runs the house.")]
	private static string GetRaspberryMetrics() => MetricsStore.Render(MetricsStore.Local());

	[Description("How a sensor or machine reading changed over time. Use this for questions about the past, like how warm it was today.")]
	private static string GetHistory(
		[Description("One of: temperature, humidity, light, co2, tvoc, motion, lamp, computer, pi_cpu, pi_temp, pi_ram, pc_cpu, pc_temp, pc_ram")] string metric,
		[Description("How many hours back to look, 1 to 720")] int hours)
	{
		string wanted = metric.Trim().ToLowerInvariant();
		if (!HistoryService.Metrics.Contains(wanted))
			return $"Unknown metric '{metric}'. Valid metrics: {string.Join(", ", HistoryService.Metrics)}";

		DateTime to = DateTime.Now;
		DateTime from = to.AddHours(-Math.Clamp(hours, 1, 720));

		IReadOnlyList<HistoryPoint> points = HistoryService.Read(wanted, from, to);
		if (points.Count == 0) return $"Nothing recorded for {wanted} in the last {hours} hours";

		HistoryPoint coldest = points.MinBy(point => point.Value)!;
		HistoryPoint warmest = points.MaxBy(point => point.Value)!;

		return $"{wanted} over the last {hours}h: " +
			$"lowest {Math.Round(coldest.Value, 1)} at {coldest.At:HH:mm}, " +
			$"highest {Math.Round(warmest.Value, 1)} at {warmest.At:HH:mm}, " +
			$"average {Math.Round(points.Average(point => point.Value), 1)}, " +
			$"now {Math.Round(points[^1].Value, 1)} ({points.Count} samples)";
	}

	[Description("Run a shell command on the desktop computer and return its output. To start a graphical application use LaunchOnComputer instead.")]
	private static async Task<string> RunOnComputer([Description("Shell command, runs under bash on Arch Linux")] string command)
	{
		if (string.IsNullOrWhiteSpace(command)) return "No command given";

		return (await HardwareApi.Devices.CommandComputer(EComputerCommand.Command, command)).Match(output => output, error => error);
	}

	[Description("Start an application on the desktop computer, detached so it keeps running. Use this for things like steam, firefox or spotify.")]
	private static async Task<string> LaunchOnComputer([Description("Command that starts the application, for example 'steam'")] string application)
	{
		if (string.IsNullOrWhiteSpace(application)) return "No application given";

		return (await HardwareApi.Devices.CommandComputer(EComputerCommand.Launch, application))
			.Match(_ => $"Launched {application}", error => error);
	}

	[Description("Run a shell command on the Raspberry Pi that hosts this house and return its output.")]
	private static async Task<string> RunOnRaspberry([Description("Shell command, runs under bash on Debian")] string command)
	{
		if (string.IsNullOrWhiteSpace(command)) return "No command given";

		return (await HardwareApi.Raspberry.RunCommand(command)).Match(output => output, error => error);
	}

	[Description("List every device in the house with its current power state.")]
	private static string GetDevices() =>
		string.Join("\n", HardwareApi.Devices.GetAllPower().Select(device => $"{device.Device} is {device.Status}"));

	[Description("Turn a device on or off, or toggle it. Use this to actually change the state of the house. Note that Generic is usually the room speakers.")]
	private static string SwitchDevice(
		[Description("Lamp, Computer, Power, Generic, or Room to switch every light at once")] string device,
		[Description("On, Off or Toggle")] string action)
	{
		if (!Enum.TryParse(action, true, out ESwitchAction trigger))
			return $"Unknown action '{action}'. Valid actions: {Names<ESwitchAction>()}";

		if (device.Equals(RoomAlias, StringComparison.OrdinalIgnoreCase))
			return HardwareApi.Devices.SwitchRoom(trigger).Match(result => $"Room switched {result}", error => error);

		if (!Enum.TryParse(device, true, out EDevice parsed))
			return $"Unknown device '{device}'. Valid devices: {Names<EDevice>()}, Room";

		return HardwareApi.Devices.Switch(parsed, trigger).Match(result => $"{parsed} switched {result}", error => error);
	}

	[Description("Turn the lamp off and hold manual mode until morning. Only for when the user is going to sleep.")]
	private static string EnterSleepMode()
	{
		HardwareApi.Devices.EnterSleepMode();
		return "Sleep mode active, lamp off and automation held until morning";
	}

	[Description("Read every sensor in the house: temperature, humidity, light, motion, CO2 and TVOC.")]
	private static string GetSensors() =>
		string.Join("\n", HardwareApi.Sensors.GetAllData().Select(sensor => $"{sensor.Sensor}: {sensor.Value}{sensor.Unit}"));

	[Description("Read the automation settings: thresholds, automatic mode, morning and night hours.")]
	private static string GetSettings() =>
		string.Join("\n", HardwareApi.Sensors.GetAllSettings().Select(setting => $"{setting.Setting}: {setting.Value}"));

	[Description("Change one automation setting. Booleans use 1 for on and 0 for off.")]
	private static string SetSetting(
		[Description("Name of the setting, as returned by GetSettings")] string setting,
		[Description("New numeric value")] int value)
	{
		if (!Enum.TryParse(setting, true, out ESettings parsed))
			return $"Unknown setting '{setting}'. Valid settings: {Names<ESettings>()}";

		return HardwareApi.Sensors.SetSettings(parsed, value).Match(result => result, error => error);
	}
}
