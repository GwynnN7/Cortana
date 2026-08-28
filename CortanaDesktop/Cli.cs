using CortanaLib;
using CortanaLib.Structures;

namespace CortanaDesktop;

internal static class Cli
{
	private const string Reset = "\e[0m";
	private const string Cyan = "\e[36m";
	private const string Dim = "\e[2m";
	private const string Bold = "\e[1m";
	private const string Red = "\e[31m";

	private static string Conversation => $"desktop:{Environment.MachineName}";

	public static async Task<int> Run(string[] args) => args[0] switch
	{
		"chat" => await Chat(),
		"ask" => await Ask(string.Join(' ', args[1..])),
		"pc" => await Pc(),
		"monitor" => await Monitor(args.Contains("--watch")),
		"status" => await Status(),
		"help" or "--help" or "-h" => Help(),
		_ => Unknown(args[0])
	};

	private static int Help()
	{
		Console.WriteLine($"""
			{Bold}cortana{Reset} - desktop agent and client

			  {Cyan}chat{Reset}              interactive conversation, /reset clears it
			  {Cyan}ask{Reset} <question>    one-shot question, left out of the conversation
			  {Cyan}monitor{Reset} [--watch] this machine's performance and temperatures
			  {Cyan}pc{Reset}                what the Kernel last heard from this machine
			  {Cyan}status{Reset}            house, sensors, services and this computer

			Run with no arguments to start the resident agent.
			{Dim}service, git, api, deploy and notify are handled by the shell wrapper.{Reset}
			""");
		return 0;
	}

	private static int Unknown(string command)
	{
		Console.Error.WriteLine($"{Red}Unknown command '{command}'{Reset}");
		Help();
		return 1;
	}

	private static async Task<int> Ask(string question)
	{
		if (string.IsNullOrWhiteSpace(question))
		{
			Console.Error.WriteLine($"{Red}Nothing to ask{Reset}");
			return 1;
		}

		Console.WriteLine(await Send(question, false));
		return 0;
	}

	private static async Task<int> Pc()
	{
		Console.WriteLine(await ApiHandler.Get($"{ERoute.Computer}/metrics"));
		return 0;
	}

	private static async Task<int> Chat()
	{
		Console.WriteLine($"{Cyan}Cortana{Reset} {Dim}- /reset clears the conversation, /exit or Ctrl-D quits{Reset}\n");

		while (true)
		{
			Console.Write($"{Bold}> {Reset}");
			string? line = Console.ReadLine();

			if (line is null or "/exit" or "/quit") return 0;
			if (string.IsNullOrWhiteSpace(line)) continue;

			if (line == "/reset")
			{
				await ApiHandler.Delete($"{ERoute.AI}/{Conversation}");
				Console.WriteLine($"{Dim}conversation cleared{Reset}\n");
				continue;
			}

			Console.WriteLine($"\n{Cyan}{await Send(line, true)}{Reset}\n");
		}
	}

	private static Task<string> Send(string message, bool remember) =>
		ApiHandler.Post($"{ERoute.AI}", new PostChat(message, Conversation, Environment.UserName, remember));

	private static async Task<int> Monitor(bool watch)
	{
		if (!watch)
		{
			SystemMonitor.Collect();
			await Task.Delay(500);
			Console.WriteLine(Render(SystemMonitor.Collect()));
			return 0;
		}

		Console.CancelKeyPress += (_, _) => Console.Write("\e[?25h");
		Console.Write("\e[?25l");

		try
		{
			while (true)
			{
				PostMetrics metrics = SystemMonitor.Collect();
				Console.Write($"\e[H\e[J{Render(metrics)}\n\n{Dim}Ctrl-C to stop{Reset}");
				await Task.Delay(1000);
			}
		}
		finally
		{
			Console.Write("\e[?25h");
		}
	}

	private static string Render(PostMetrics metrics)
	{
		var lines = new List<string>
		{
			$"{Bold}{metrics.Host}{Reset} {Dim}{metrics.Os}{Reset}",
			"",
			Bar("CPU", metrics.CpuLoad, metrics.CpuTemp),
			Bar("RAM", metrics.MemoryTotal > 0 ? metrics.MemoryUsed / metrics.MemoryTotal * 100 : 0, 0,
				$"{metrics.MemoryUsed:F1}/{metrics.MemoryTotal:F1} GB")
		};

		if (metrics.GpuLoad > 0 || metrics.GpuTemp > 0)
			lines.Add(Bar("GPU", metrics.GpuLoad, metrics.GpuTemp));

		lines.Add(Bar("Disk", metrics.DiskTotal > 0 ? metrics.DiskUsed / metrics.DiskTotal * 100 : 0, 0,
			$"{metrics.DiskUsed:F0}/{metrics.DiskTotal:F0} GB"));

		lines.Add("");
		lines.Add($"{Dim}up {TimeSpan.FromSeconds(metrics.Uptime):d\\d\\ hh\\:mm}{Reset}");

		return string.Join("\n", lines);
	}

	private static string Bar(string label, double percent, double temperature, string? detail = null)
	{
		const int width = 24;
		var filled = (int)Math.Round(Math.Clamp(percent, 0, 100) / 100 * width);

		string colour = percent >= 90 ? Red : percent >= 70 ? "\e[33m" : Cyan;
		string right = detail ?? $"{percent,3:F0}%";
		string temp = temperature > 0 ? $"  {temperature,3:F0}°C" : "";

		return $"{label,-5} {colour}{new string('█', filled)}{Dim}{new string('░', width - filled)}{Reset} {right,-14}{temp}";
	}

	private static async Task<int> Status()
	{
		Task<string> devices = ApiHandler.Get($"{ERoute.Devices}");
		Task<string> sensors = ApiHandler.Get($"{ERoute.Sensors}");
		Task<string> functions = ApiHandler.Get($"{ERoute.SubFunctions}");
		Task<string> raspberry = ApiHandler.Get($"{ERoute.Raspberry}/{ERaspberryInfo.Temperature}");

		await Task.WhenAll(devices, sensors, functions, raspberry);

		Console.WriteLine($"""
			{Bold}Devices{Reset}
			{Indent(devices.Result)}

			{Bold}Sensors{Reset}
			{Indent(sensors.Result)}

			{Bold}Services{Reset}
			{Indent(functions.Result)}

			{Bold}Raspberry{Reset}
			{Indent(raspberry.Result)}

			{Bold}This computer{Reset}
			{Indent(Strip(Render(Sample())))}
			""");

		return 0;
	}

	private static PostMetrics Sample()
	{
		SystemMonitor.Collect();
		Thread.Sleep(300);
		return SystemMonitor.Collect();
	}

	private static string Strip(string value) => value.Replace(Reset, "").Replace(Cyan, "").Replace(Dim, "").Replace(Bold, "").Replace(Red, "").Replace("\e[33m", "");

	private static string Indent(string value) =>
		string.Join("\n", value.Split('\n').Select(line => $"  {line.TrimEnd()}"));
}
