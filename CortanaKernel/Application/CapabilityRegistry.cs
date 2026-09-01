using System.Globalization;
using CortanaKernel.Domain.Ai;
using CortanaKernel.Domain.Common;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

/// Every capability the AI can reach
public sealed class CapabilityRegistry
{
	private readonly Dictionary<string, AiCapability> _capabilities;

	public CapabilityRegistry(
		DeviceService devices,
		SensorService sensors,
		AutomationService automation,
		SettingsService settings,
		ScheduleService schedules,
		HistoryService history,
		MetricsService metrics,
		ServiceControlService services,
		SnapshotService snapshot,
		NotificationService notifications)
	{
		var list = new List<AiCapability>
		{
			// ---------- queries ----------
			Query("GetHouseState", "Everything at a glance: device power states, sensor readings, automation and sleep mode.",
				(_, _, _) => Task.FromResult(HouseState(devices, sensors, automation))),

			Query("GetDevices", "Power state of every device in the room.",
				(_, _, _) => Task.FromResult(string.Join("\n", devices.All().Select(view => $"{view.Device} is {view.State}")))),

			Query("GetSensors", "Latest reading from every sensor: temperature, humidity, light, motion, CO2 and TVOC.",
				(_, _, _) => Task.FromResult(string.Join("\n", sensors.All().Select(view =>
					$"{view.Sensor}: {(view.Available ? view.Value + view.Unit : "unavailable")}")))),

			Query("GetAutomationState", "Whether automation is on, whether Cortana thinks the user is asleep, the day/night context and the motion state.",
				(_, _, _) => Task.FromResult(AutomationText(automation.View()))),

			Query("GetSettings", "Every automation setting and its current value.",
				(_, _, _) => Task.FromResult(string.Join("\n", settings.All().Select(view => $"{view.Setting}: {view.Value}{view.Unit}")))),

			Query("GetComputerStatus", "Whether the desktop computer is reachable, plus its load and temperatures.",
				(_, _, _) => Task.FromResult(devices.ComputerConnected
					? metrics.Computer() is { } view ? MachineMetrics.Render(view) : "The computer is connected but has not reported metrics yet"
					: "The computer is off or not connected")),

			Query("GetRaspberryStatus", "Load, temperature and uptime of the Raspberry Pi that runs the house.",
				(_, _, _) => Task.FromResult(MachineMetrics.Render(metrics.Raspberry()))),

			Query("GetSchedules", "Every saved schedule with its next run time.",
				(_, _, _) => Task.FromResult(schedules.Views().Count == 0
					? "There are no schedules"
					: string.Join("\n", schedules.Views().Select(Describe)))),

			Query("GetServices", "Which Cortana services are running.",
				async (_, _, token) => string.Join("\n", (await services.All(token)).Select(view => $"{view.Service}: {(view.Running ? "running" : "stopped")}"))),

			Query("GetRecentEvents", "The most recent things Cortana logged or notified about.",
				(arguments, _, _) => Task.FromResult(string.Join("\n", notifications
					.Recent(arguments.Integer("count", 15))
					.Select(entry => $"{entry.Timestamp:HH:mm} [{entry.Source}] {entry.Message}"))),
				new AiToolParameter("count", "How many entries to return, 1 to 100", AiParameterType.Integer, false)),

			// ---------- analysis ----------
			Analysis("ExplainAutomation",
				"Why the lamp or sleep mode did or did not change: the current state, active overrides, timers and the recorded decisions.",
				(_, _, _) => Task.FromResult(DiagnosticsText(snapshot.Diagnostics(notifications.Recent(10))))),

			Analysis("SummariseHistory", "How a recorded value moved over the last hours: minimum, maximum, average and the latest sample.",
				(arguments, _, _) =>
				{
					string metric = arguments.Text("metric");
					int hours = Math.Clamp(arguments.Integer("hours", 24), 1, 24 * 30);

					return Task.FromResult(history.Series(metric, hours).Match(
						series => $"{series.Metric} over {hours}h: minimum {series.Min}{series.Unit}, maximum {series.Max}{series.Unit}, " +
							$"average {series.Average}{series.Unit}, now {series.Samples[^1].Value}{series.Unit} ({series.Points} samples)",
						error => error));
				},
				new AiToolParameter("metric", $"One of: {string.Join(", ", history.Metrics)}", AiParameterType.String, true),
				new AiToolParameter("hours", "How many hours back to look, 1 to 720", AiParameterType.Integer, false)),

			Analysis("AnalyseHistory",
				"Run an exact calculation over recorded data instead of estimating it. Use this for averages, extremes, trends, how long something stayed in a state, the worst period, or comparing two windows.",
				(arguments, _, _) =>
				{
					if (!arguments.TryEnum("function", out AnalysisFunction function))
						return Task.FromResult($"Unknown function. Valid functions: {string.Join(", ", Enum.GetNames<AnalysisFunction>())}");

					DateTimeOffset now = DateTimeOffset.Now;
					var request = new AnalysisRequest(
						function,
						arguments.Text("metric"),
						now.AddHours(-Math.Abs(arguments.Number("fromHoursAgo", 24))),
						now.AddHours(-Math.Abs(arguments.Number("toHoursAgo", 0))),
						arguments.ContainsKey("atHoursAgo") ? now.AddHours(-Math.Abs(arguments.Number("atHoursAgo", 0))) : null,
						arguments.ContainsKey("compareFromHoursAgo") ? now.AddHours(-Math.Abs(arguments.Number("compareFromHoursAgo", 48))) : null,
						arguments.ContainsKey("compareToHoursAgo") ? now.AddHours(-Math.Abs(arguments.Number("compareToHoursAgo", 24))) : null,
						arguments.ContainsKey("state") ? arguments.Number("state", 1) : null,
						arguments.Integer("windowMinutes", 60));

					return Task.FromResult(history.Analyse(request).Match(result => result.Summary, error => error));
				},
				new AiToolParameter("function", $"One of: {string.Join(", ", Enum.GetNames<AnalysisFunction>())}", AiParameterType.String, true),
				new AiToolParameter("metric", $"One of: {string.Join(", ", history.Metrics)}", AiParameterType.String, true),
				new AiToolParameter("fromHoursAgo", "Start of the window, in hours before now", AiParameterType.Number, false),
				new AiToolParameter("toHoursAgo", "End of the window, in hours before now. 0 means now", AiParameterType.Number, false),
				new AiToolParameter("atHoursAgo", "For ValueAt: the moment of interest, in hours before now", AiParameterType.Number, false),
				new AiToolParameter("compareFromHoursAgo", "For Compare: start of the second window", AiParameterType.Number, false),
				new AiToolParameter("compareToHoursAgo", "For Compare: end of the second window", AiParameterType.Number, false),
				new AiToolParameter("state", "For DurationInState: the value to measure, 1 for on and 0 for off", AiParameterType.Number, false),
				new AiToolParameter("windowMinutes", "For WorstPeriod: the length of the window to score", AiParameterType.Integer, false)),

			// ---------- actions ----------
			Action("SwitchDevice", "Turn a device on or off, or toggle it. Lamp is the room light, Power is the desktop's mains supply, Generic is the extra socket (a speaker in Orvieto, the light in Pisa).",
				(arguments, origin, _) =>
				{
					if (!arguments.TryEnum("device", out DeviceId device))
						return Task.FromResult($"Unknown device. Valid devices: {string.Join(", ", Enum.GetNames<DeviceId>())}");
					if (!arguments.TryEnum("action", out SwitchAction action))
						return Task.FromResult($"Unknown action. Valid actions: {string.Join(", ", Enum.GetNames<SwitchAction>())}");

					return Task.FromResult(devices.Switch(device, action, origin).Match(result => $"{device}: {result}", error => error));
				},
				new AiToolParameter("device", $"One of: {string.Join(", ", Enum.GetNames<DeviceId>())}", AiParameterType.String, true),
				new AiToolParameter("action", "On, Off or Toggle", AiParameterType.String, true)),

			Action("SwitchRoom", "Switch the whole room at once. Off turns the lamp and the mains supply off; On brings the supply and the computer up.",
				(arguments, origin, _) =>
				{
					if (!arguments.TryEnum("action", out SwitchAction action)) return Task.FromResult("Use On or Off");
					return Task.FromResult(devices.SwitchRoom(action, origin).Match(result => result, error => error));
				},
				new AiToolParameter("action", "On or Off", AiParameterType.String, true)),

			Action("SetSleepMode", "Tell Cortana whether the user is going to sleep. Sleep mode changes the automation rules; it does not turn automation off.",
				(arguments, origin, _) =>
				{
					if (!arguments.TryEnum("action", out SwitchAction action)) return Task.FromResult("Use On, Off or Toggle");
					return Task.FromResult(automation.SetSleepMode(action, origin).Match(result => result, error => error));
				},
				new AiToolParameter("action", "On, Off or Toggle", AiParameterType.String, true)),

			Action("ResumeAutomation",
				"Cancel a manual hold and let automation take control of the lamp again straight away, instead of waiting for the hold to expire.",
				(_, origin, _) => Task.FromResult(automation.ReleaseHolds(origin).Match(result => result, error => error))),

			Action("SetAutomation", "Turn autonomous automation on or off. Off means Cortana stops controlling devices by itself until told otherwise.",
				(arguments, origin, _) =>
				{
					if (!arguments.TryEnum("action", out SwitchAction action)) return Task.FromResult("Use On, Off or Toggle");
					return Task.FromResult(automation.SetAutomation(action, origin).Match(result => result, error => error));
				},
				new AiToolParameter("action", "On, Off or Toggle", AiParameterType.String, true)),

			Action("CommandComputer",
				"Do something on the desktop computer: Shutdown, Suspend, Reboot, Notify (argument is the text), BootIntoOtherOperatingSystem (reboot into the other installed OS, for example Windows), RunShellCommand (argument is the command), LaunchApplication (argument is the application name), CloseApplication (argument is the application name).",
				async (arguments, origin, token) =>
				{
					if (!arguments.TryEnum("command", out ComputerCommand command))
						return $"Unknown command. Valid commands: {string.Join(", ", Enum.GetNames<ComputerCommand>())}";

					return (await devices.CommandComputer(command, arguments.Text("argument"), origin, token)).Match(result => result, error => error);
				},
				new AiToolParameter("command", $"One of: {string.Join(", ", Enum.GetNames<ComputerCommand>())}", AiParameterType.String, true),
				new AiToolParameter("argument", "The text, command or application name the chosen command needs", AiParameterType.String, false)),

			Action("RunShellCommandOnRaspberry", "Run a shell command on the Raspberry Pi that hosts Cortana and return its output.",
				async (arguments, _, token) =>
					(await devices.CommandRaspberry(RaspberryCommand.RunShellCommand, arguments.Text("command"), token)).Match(result => result, error => error),
				new AiToolParameter("command", "The shell command, running under bash on the Pi", AiParameterType.String, true)),

			Action("SendNotification", "Send the user a message through the dashboard, Telegram or Discord.",
				(arguments, _, _) =>
				{
					NotificationChannel? channel = arguments.TryEnum("channel", out NotificationChannel parsed) ? parsed : null;
					return Task.FromResult(notifications
						.Send(new NotifyRequest(arguments.Text("message"), NotificationSource.Ai, NotificationLevel.Info, channel))
						.Match(result => result, error => error));
				},
				new AiToolParameter("message", "What to say", AiParameterType.String, true),
				new AiToolParameter("channel", "Web, Telegram or Discord. Leave empty to use the configured channels", AiParameterType.String, false)),

			// ---------- management ----------
			Management("ChangeSetting", "Change one automation setting. On/Off settings accept On, Off or Toggle; the others take a number.",
				(arguments, _, _) =>
				{
					if (!arguments.TryEnum("setting", out SettingKey setting))
						return Task.FromResult($"Unknown setting. Valid settings: {string.Join(", ", Enum.GetNames<SettingKey>())}");

					return Task.FromResult(settings.Write(setting, arguments.Text("value")).Match(result => $"{setting} is now {result}", error => error));
				},
				new AiToolParameter("setting", $"One of: {string.Join(", ", Enum.GetNames<SettingKey>())}", AiParameterType.String, true),
				new AiToolParameter("value", "The new value", AiParameterType.String, true)),

			Management("CreateSchedule",
				"Save an action to run later, repeatedly, or when something happens. Use trigger Event with event ComputerTurnedOn to chain an action to the computer coming up.",
				(arguments, _, _) =>
				{
					if (!arguments.TryEnum("trigger", out ScheduleTrigger trigger))
						return Task.FromResult($"Unknown trigger. Valid triggers: {string.Join(", ", Enum.GetNames<ScheduleTrigger>())}");
					if (!arguments.TryEnum("actionType", out ScheduleActionType actionType))
						return Task.FromResult($"Unknown action type. Valid types: {string.Join(", ", Enum.GetNames<ScheduleActionType>())}");

					ScheduleEvent? scheduleEvent = arguments.TryEnum("event", out ScheduleEvent parsedEvent) ? parsedEvent : null;
					DayOfWeek? day = arguments.TryEnum("day", out DayOfWeek parsedDay) ? parsedDay : null;
					DateTimeOffset? at = arguments.ContainsKey("inMinutes")
						? DateTimeOffset.Now.AddMinutes(arguments.Number("inMinutes", 0))
						: DateTimeOffset.TryParse(arguments.Text("at"), CultureInfo.InvariantCulture, out DateTimeOffset parsedAt) ? parsedAt : null;

					var request = new CreateScheduleRequest(
						arguments.Text("name", "Scheduled action"),
						trigger,
						actionType,
						arguments.Text("target"),
						arguments.Text("value"),
						at,
						arguments.Integer("intervalSeconds", 0),
						arguments.Integer("hour", 0),
						arguments.Integer("minute", 0),
						day,
						scheduleEvent,
						trigger != ScheduleTrigger.Event || arguments.Text("repeat").ToLowerInvariant() is not ("true" or "yes"),
						arguments.Integer("minimumIntervalSeconds", 0),
						"ai");

					return Task.FromResult(schedules.Create(request).Match(
						schedule => $"Saved '{schedule.Name}' with id {schedule.Id}",
						error => error));
				},
				new AiToolParameter("name", "A short name for the schedule", AiParameterType.String, true),
				new AiToolParameter("trigger", $"One of: {string.Join(", ", Enum.GetNames<ScheduleTrigger>())}", AiParameterType.String, true),
				new AiToolParameter("actionType", $"One of: {string.Join(", ", Enum.GetNames<ScheduleActionType>())}", AiParameterType.String, true),
				new AiToolParameter("target", "What the action applies to, for example a device name or a computer command", AiParameterType.String, false),
				new AiToolParameter("value", "The value the action needs, for example On, Off or a message", AiParameterType.String, false),
				new AiToolParameter("inMinutes", "For a Once trigger: how many minutes from now", AiParameterType.Number, false),
				new AiToolParameter("at", "For a Once trigger: an absolute date and time", AiParameterType.String, false),
				new AiToolParameter("intervalSeconds", "For an Interval trigger: how many seconds between runs", AiParameterType.Integer, false),
				new AiToolParameter("hour", "For Daily and Weekly triggers: the hour", AiParameterType.Integer, false),
				new AiToolParameter("minute", "For Daily and Weekly triggers: the minute", AiParameterType.Integer, false),
				new AiToolParameter("day", "For a Weekly trigger: the day of the week", AiParameterType.String, false),
				new AiToolParameter("event", $"For an Event trigger, one of: {string.Join(", ", Enum.GetNames<ScheduleEvent>())}", AiParameterType.String, false),
				new AiToolParameter("repeat", "For an Event trigger: true to run on every matching event. Leave empty for a one-time hook, which is what chaining one action onto another needs", AiParameterType.String, false),
				new AiToolParameter("minimumIntervalSeconds", "For a repeating Event trigger: ignore matching events this soon after the last run", AiParameterType.Integer, false)),

			Management("DeleteSchedule", "Delete a saved schedule by its id.",
				(arguments, _, _) => Task.FromResult(schedules.Delete(arguments.Text("id")) ? "Deleted" : "No schedule with that id"),
				new AiToolParameter("id", "The schedule id, as shown by GetSchedules", AiParameterType.String, true)),

			Management("ControlService", "Start, stop, restart or update one of Cortana's own services.",
				async (arguments, _, token) =>
				{
					if (!arguments.TryEnum("service", out ServiceId service))
						return $"Unknown service. Valid services: {string.Join(", ", Enum.GetNames<ServiceId>())}";
					if (!arguments.TryEnum("action", out ServiceAction action))
						return $"Unknown action. Valid actions: {string.Join(", ", Enum.GetNames<ServiceAction>())}";

					return (await services.Control(service, action, token)).Match(result => result, error => error);
				},
				new AiToolParameter("service", $"One of: {string.Join(", ", Enum.GetNames<ServiceId>())}", AiParameterType.String, true),
				new AiToolParameter("action", $"One of: {string.Join(", ", Enum.GetNames<ServiceAction>())}", AiParameterType.String, true))
		};

		_capabilities = list.ToDictionary(capability => capability.Name);
	}

	public IReadOnlyCollection<AiCapability> All => _capabilities.Values;

	public IReadOnlyCollection<AiCapability> ReadOnly => [.. _capabilities.Values.Where(capability => capability.IsReadOnly)];

	public IReadOnlyCollection<AiCapability> For(bool trusted) => trusted ? All : ReadOnly;

	public AiCapability? Find(string name) => _capabilities.GetValueOrDefault(name);

	// ---------- helpers ----------

	private static AiCapability Query(string name, string description,
		Func<IReadOnlyDictionary<string, string>, CommandOrigin, CancellationToken, Task<string>> execute, params AiToolParameter[] parameters) =>
		new(name, description, CapabilityKind.Query, parameters, execute);

	private static AiCapability Analysis(string name, string description,
		Func<IReadOnlyDictionary<string, string>, CommandOrigin, CancellationToken, Task<string>> execute, params AiToolParameter[] parameters) =>
		new(name, description, CapabilityKind.Analysis, parameters, execute);

	private static AiCapability Action(string name, string description,
		Func<IReadOnlyDictionary<string, string>, CommandOrigin, CancellationToken, Task<string>> execute, params AiToolParameter[] parameters) =>
		new(name, description, CapabilityKind.Action, parameters, execute);

	private static AiCapability Management(string name, string description,
		Func<IReadOnlyDictionary<string, string>, CommandOrigin, CancellationToken, Task<string>> execute, params AiToolParameter[] parameters) =>
		new(name, description, CapabilityKind.Management, parameters, execute);

	private static string HouseState(DeviceService devices, SensorService sensors, AutomationService automation)
	{
		string deviceText = string.Join(", ", devices.All().Select(view => $"{view.Device} {view.State}"));
		string sensorText = string.Join(", ", sensors.All()
			.Where(view => view.Available)
			.Select(view => $"{view.Sensor} {view.Value}{view.Unit}"));

		return $"Devices: {deviceText}\nSensors: {(sensorText.Length == 0 ? "the station is offline" : sensorText)}\n{AutomationText(automation.View())}";
	}

	private static string AutomationText(AutomationView view)
	{
		var lines = new List<string>
		{
			$"Automation: {view.Status}{(view.HoldingUntil is { } holding ? $", holding until {holding:HH:mm}" : "")}",
			$"Time context: {view.TimeContext}",
			$"Sleep mode: {(view.SleepMode ? "active" : "inactive")}{(view.SleepModeUntil is { } until ? $" until {until:HH:mm}" : "")}",
			$"Motion: {(view.MotionActive ? "detected" : "none")}{(view.LastMotionAt is { } last ? $", last at {last:HH:mm:ss}" : "")}",
			$"Station: {(view.StationOnline ? "online" : "offline")}"
		};

		if (view.SleepHold) lines.Add($"Sleep hold until {view.SleepHoldUntil:HH:mm}");
		if (view.SleepEntryAt is { } entry) lines.Add($"Sleep will start at {entry:HH:mm}");
		if (view.AirQualityWarning) lines.Add("Air quality warning is active");

		return string.Join("\n", lines);
	}

	private static string DiagnosticsText(AutomationDiagnostics diagnostics)
	{
		var lines = new List<string> { AutomationText(diagnostics.Automation), $"Last decision: {diagnostics.LastDecision}" };

		IEnumerable<DeviceView> overrides = diagnostics.Devices.Where(view => view.OverrideUntil != null);
		lines.Add(overrides.Any()
			? "Active overrides: " + string.Join(", ", overrides.Select(view => $"{view.Device} until {view.OverrideUntil:HH:mm}"))
			: "No manual overrides are active");

		lines.Add("Recent decisions:");
		lines.AddRange(diagnostics.RecentDecisions.Take(10)
			.Select(record => $"  {record.At:HH:mm:ss} {record.Subject} -> {record.Outcome} ({record.Reason})"));

		lines.Add("Recent events:");
		lines.AddRange(diagnostics.RecentEvents.Take(10)
			.Select(entry => $"  {entry.Timestamp:HH:mm:ss} [{entry.Source}] {entry.Message}"));

		return string.Join("\n", lines);
	}

	private static string Describe(ScheduleView view)
	{
		Schedule schedule = view.Schedule;
		string when = schedule.Trigger switch
		{
			ScheduleTrigger.Once => $"once at {schedule.At:dd MMM HH:mm}",
			ScheduleTrigger.Interval => $"every {schedule.IntervalSeconds}s",
			ScheduleTrigger.Daily => $"daily at {schedule.Hour:00}:{schedule.Minute:00}",
			ScheduleTrigger.Weekly => $"{schedule.Day} at {schedule.Hour:00}:{schedule.Minute:00}",
			ScheduleTrigger.Event => $"on {schedule.Event}",
			_ => "unknown"
		};

		string next = view.NextRun == null ? "" : $" -> {view.NextRun:dd MMM HH:mm}";
		string state = schedule.Enabled ? "" : " [disabled]";
		return $"[{schedule.Id}] {schedule.Name}: {when}{next}{state} ({schedule.Action.Type} {schedule.Action.Target} {schedule.Action.Value})".TrimEnd();
	}
}
