using System.Globalization;
using System.Text.RegularExpressions;
using CortanaLib.Client;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaDesktop;

internal static class Cli
{
	private const string Reset = "\e[0m";
	private const string Bold = "\e[1m";
	private const string Dim = "\e[2m";

	private const string Accent = "\e[38;2;81;209;246m";        // --accent          #51d1f6
	private const string AccentStrong = "\e[38;2;126;224;255m"; // --accent-strong   #7ee0ff
	private const string Muted = "\e[38;2;138;164;192m";        // --text-muted      #8aa4c0
	private const string Ok = "\e[38;2;79;214;232m";            // --ok              #4fd6e8
	private const string Warm = "\e[38;2;216;164;92m";          // .metric-warm      #d8a45c
	private const string Hot = "\e[38;2;255;122;107m";          // .metric-temp      #ff7a6b
	private const string Bad = "\e[38;2;255;107;107m";          // --bad             #ff6b6b

	private static readonly CortanaClient Client = CortanaClient.Default.As(CommandSurface.Desktop);

	private static string Conversation => $"desktop:{Environment.MachineName}";

	public static Task<int> Run(string[] args) => args[0] switch
	{
		"chat" => Chat(string.Join(' ', args[1..])),
		"ask" => Ask(string.Join(' ', args[1..])),
		"pc" => PrintMetrics(Client.ComputerMetricsText()),
		"monitor" => Monitor(args.Contains("--watch")),
		"status" => Status(),
		"help" or "--help" or "-h" => Task.FromResult(Help()),
		_ => Task.FromResult(Unknown(args[0]))
	};

	private static int Help()
	{
		Console.WriteLine($"""
			{AccentStrong}{Bold}cortana{Reset} {Muted}- desktop agent and client{Reset}

			  {Accent}chat{Reset}              interactive conversation, /reset clears it
			  {Accent}chat{Reset} <message>    one message through the same persistent conversation
			  {Accent}ask{Reset} <question>    one-shot question, never stored in any conversation
			  {Accent}monitor{Reset} [--watch] this machine's performance and temperatures
			  {Accent}pc{Reset}                what the Kernel last heard from this machine
			  {Accent}status{Reset}            house, sensors, services and this computer

			Run with no arguments to start the resident agent.
			{Dim}service, git, api, deploy, notify and flash are handled by the shell wrapper.{Reset}
			""");
		return 0;
	}

	private static int Unknown(string command)
	{
		Console.Error.WriteLine($"{Bad}Unknown command '{command}'{Reset}");
		Help();
		return 1;
	}

	/// Persistent conversation, interactive when given nothing and one-shot when given a message
	private static async Task<int> Chat(string message)
	{
		if (!string.IsNullOrWhiteSpace(message))
		{
			Console.WriteLine(await Send(message, remember: true));
			return 0;
		}

		Console.WriteLine($"{AccentStrong}{Bold}Cortana{Reset} {Dim}- /reset clears the conversation, /exit or Ctrl-D quits{Reset}\n");

		while (true)
		{
			Console.Write($"{Accent}{Bold}> {Reset}");
			string? line = Console.ReadLine();

			if (line is null or "/exit" or "/quit") return 0;
			if (string.IsNullOrWhiteSpace(line)) continue;

			if (line == "/reset")
			{
				await Client.ResetConversation(Conversation);
				Console.WriteLine($"{Dim}conversation cleared{Reset}\n");
				continue;
			}

			Console.WriteLine($"\n{Accent}{await Send(line, remember: true)}{Reset}\n");
		}
	}

	/// Fire and forget withouth history
	private static async Task<int> Ask(string question)
	{
		if (string.IsNullOrWhiteSpace(question))
		{
			Console.Error.WriteLine($"{Bad}Nothing to ask{Reset}");
			return 1;
		}

		Console.WriteLine(await Send(question, remember: false));
		return 0;
	}

	private static async Task<string> Send(string message, bool remember) =>
		(await Client.Ask(message, remember ? Conversation : $"ask:{Guid.NewGuid():N}", Environment.UserName, remember))
		.Match(reply => reply, error => $"{Bad}{error}{Reset}");

	private static readonly Regex TemperaturePattern = new(@"(\d+(?:\.\d+)?)°C", RegexOptions.Compiled);
	private static readonly Regex PercentPattern = new(@"(\d+(?:\.\d+)?)%", RegexOptions.Compiled);

	private static async Task<int> PrintMetrics(Task<string> call)
	{
		string text = await call;
		var rendered = new List<string>();
		var first = true;

		foreach (string line in text.ReplaceLineEndings("\n").Split('\n'))
		{
			if (line.Trim().Length == 0) continue;

			if (first)
			{
				first = false;
				int open = line.IndexOf('(');
				rendered.Add(open < 0
					? $"{AccentStrong}{Bold}{line}{Reset}"
					: $"{AccentStrong}{Bold}{line[..open].TrimEnd()}{Reset} {Muted}{line[open..]}{Reset}");
				continue;
			}

			int colon = line.IndexOf(':');
			if (colon < 0)
			{
				rendered.Add(line);
				continue;
			}

			rendered.Add($"  {Accent}{line[..colon] + ':',-8}{Reset} {Scale(line[(colon + 1)..].Trim())}");
		}

		Console.WriteLine(string.Join("\n", rendered));
		return 0;
	}

	private static string Scale(string value)
	{
		string coloured = TemperaturePattern.Replace(value, match =>
		{
			double degrees = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
			return $"{(degrees >= 75 ? Hot : degrees >= 60 ? Warm : Ok)}{match.Value}{Reset}";
		});

		return PercentPattern.Replace(coloured, match =>
		{
			double percent = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
			return $"{(percent >= 85 ? Bad : percent >= 60 ? Warm : Ok)}{match.Value}{Reset}";
		});
	}

	private static async Task<int> Print(Task<string> call)
	{
		Console.WriteLine(await call);
		return 0;
	}

	private static async Task<int> Monitor(bool watch)
	{
		if (!watch)
		{
			MachineMetrics.Collect();
			await Task.Delay(500);
			Console.WriteLine(Render(MachineMetrics.Collect()));
			return 0;
		}

		Console.CancelKeyPress += (_, _) => Console.Write("\e[?25h");
		Console.Write("\e[?25l");

		try
		{
			while (true)
			{
				Console.Write($"\e[H\e[J{Render(MachineMetrics.Collect())}\n\n{Dim}Ctrl-C to stop{Reset}");
				await Task.Delay(1000);
			}
		}
		finally
		{
			Console.Write("\e[?25h");
		}
	}

	private static async Task<int> Status()
	{
		Task<string> devices = Text(Client.Devices());
		Task<string> sensors = Text(Client.Sensors());
		Task<string> automation = Text(Client.GetText("automation"));
		Task<string> services = Text(Client.Services());

		await Task.WhenAll(devices, sensors, automation, services);

		Console.WriteLine($"""
			{Section("Devices")}
			{Indent(Highlight(devices.Result))}

			{Section("Sensors")}
			{Indent(Highlight(sensors.Result))}

			{Section("Automation")}
			{Indent(Highlight(automation.Result))}

			{Section("Services")}
			{Indent(Highlight(services.Result))}

			{Section("This computer")}
			{Indent(Strip(Render(Sample())))}
			""");

		return 0;
	}

	private static async Task<string> Text(Task<Result<string>> call) => (await call).Match(value => value, error => error);

	private static async Task<string> ComputerMetricsText(this CortanaClient client) =>
		(await client.ComputerMetrics()).Match(MachineMetrics.Render, error => error);

	private static MachineSample Sample()
	{
		MachineMetrics.Collect();
		Thread.Sleep(300);
		return MachineMetrics.Collect();
	}

	private static string Render(MachineSample metrics)
	{
		var lines = new List<string>
		{
			$"{AccentStrong}{Bold}{metrics.Host}{Reset} {Dim}{metrics.Os}{Reset}",
			"",
			Bar("CPU", metrics.CpuLoad, metrics.CpuTemp),
			Bar("RAM", Percent(metrics.MemoryUsed, metrics.MemoryTotal), 0, $"{metrics.MemoryUsed:F1}/{metrics.MemoryTotal:F1} GB")
		};

		if (metrics.GpuLoad > 0 || metrics.GpuTemp > 0) lines.Add(Bar("GPU", metrics.GpuLoad, metrics.GpuTemp));

		lines.Add(Bar("Disk", Percent(metrics.DiskUsed, metrics.DiskTotal), 0, $"{metrics.DiskUsed:F0}/{metrics.DiskTotal:F0} GB"));
		lines.Add("");
		lines.Add($"{Dim}up {TimeSpan.FromSeconds(metrics.Uptime):d\\d\\ hh\\:mm}{Reset}");

		return string.Join("\n", lines);
	}

	private static double Percent(double used, double total) => total > 0 ? used / total * 100 : 0;

	private static string Bar(string label, double percent, double temperature, string? detail = null)
	{
		const int width = 24;
		var filled = (int)Math.Round(Math.Clamp(percent, 0, 100) / 100 * width);

		string colour = percent >= 90 ? Bad : percent >= 70 ? Warm : Accent;
		string right = detail ?? $"{percent,3:F0}%";
		string temperatureText = temperature > 0 ? $"  {temperature,3:F0}°C" : "";

		string heat = temperature >= 85 ? Bad : temperature >= 65 ? Hot : Muted;
		string trailing = temperatureText.Length == 0 ? "" : $"{heat}{temperatureText}{Reset}";

		return $"{Muted}{label,-5}{Reset} {colour}{new string('█', filled)}{Dim}{new string('░', width - filled)}{Reset} {right,-14}{trailing}";
	}

	private static string Strip(string value)
	{
		foreach (string code in new[] { Reset, Bold, Dim, Accent, AccentStrong, Muted, Ok, Warm, Hot, Bad })
			value = value.Replace(code, "");

		return value;
	}

	private static string Section(string title) => $"{AccentStrong}{Bold}{title}{Reset}";

	private static readonly (string Word, string Colour)[] Highlights =
	[
		("is not running", Bad), ("is running", Ok), ("is Off", Muted), ("is On", Ok),
		("offline", Bad), ("online", Ok), ("detected", Ok), ("Active", Ok), ("Holding", Warm), ("Off", Muted)
	];

	private static readonly Regex HighlightPattern =
		new(string.Join("|", Highlights.Select(entry => Regex.Escape(entry.Word))), RegexOptions.Compiled);

	private static string Highlight(string value) =>
		HighlightPattern.Replace(value, match =>
		{
			string colour = Highlights.First(entry => entry.Word == match.Value).Colour;
			return $"{colour}{match.Value}{Reset}";
		});

	private static string Indent(string value) => string.Join("\n", value.Split('\n').Select(line => $"  {line.TrimEnd()}"));
}
