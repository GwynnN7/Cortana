using CortanaKernel.Domain.Activity;
using CortanaKernel.Domain.Automation;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Fabric;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Application;

public sealed class AutomationWorld(Fabric fabric, PresenceState presence, WarningState warnings, BindStore binds, IComputerEndpoint computer, ActivityRegistry activity) : IAutomationWorld
{
	public PowerState DeviceState(string device) => fabric.State(device);

	public bool ComputerConnected => computer.Connected;

	public DateTimeOffset? LastMotionAt => presence.LastMotionAt;

	public bool SourcesOnline => fabric.Sources.All(source => fabric.IsOnline(source.Id));

	/// A computer going offline is routine, not a fault, so it does not count against the mood
	public bool CriticalSourcesOnline => fabric.Sources
		.Where(source => source.Kind != SourceKind.Computer)
		.All(source => fabric.IsOnline(source.Id));

	public bool WarningActive => warnings.Any;

	public bool DesktopBusy => activity.Current is { } current
		&& (current.Fullscreen || current.Category == ActivityCategory.Gaming);

	/// Somebody is here, said by a sensor allowed to say it
	public bool Reported => AnyHigh(sensor => sensor.Presence == PresenceRole.Reports);

	/// Whoever is here has not left: reporting counts, and so does the desk being in use
	public bool Sustained => AnyHigh(sensor => sensor.Presence != PresenceRole.None);

	private bool AnyHigh(Func<VirtualSensor, bool> counts) => fabric.Registered
		.Where(counts)
		.Any(sensor => fabric.Read(sensor.Id) is { Value: >= 0.5 });

	public Reading? Read(string sensor) => fabric.Read(sensor);

	public string DeviceName(string device) => fabric.Device(device)?.Name ?? device;

	public IReadOnlyList<Bind> Binds => binds.All();
}

public sealed class AutomationEffects(
	Lazy<DeviceService> devices,
	Fabric fabric,
	NotificationService notifications,
	IEventBus bus) : IAutomationEffects
{
	public void SwitchDevice(string device, PowerState state, string reason) =>
		devices.Value.ApplyAutomatic(device, state, reason);

	public void Observe(string sensor, double value) =>
		fabric.Observe(SourceIds.Kernel, new Dictionary<string, double> { [sensor] = value }, DateTimeOffset.Now);

	public void TellComputer(string message) =>
		_ = devices.Value.CommandComputer(ComputerCommand.Notify, message, CommandOrigin.Automation);

	public void Notify(NotificationSource source, string message, NotificationLevel level = NotificationLevel.Info, string? reason = null) =>
		notifications.Raise(source, message, level, reason);

	public void Publish(IDomainEvent domainEvent)
	{
		switch (domainEvent)
		{
			case SleepModeChanged sleep: bus.Publish(sleep); break;
			case TimeContextChanged context: bus.Publish(context); break;
			default: bus.Publish(domainEvent); break;
		}
	}
}

public sealed class AutomationService(
	AutomationEngine engine,
	SettingsStore settings,
	IEventBus bus) : BackgroundService
{
	private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

	public AutomationEngine Engine => engine;

	public AutomationView View() => engine.View();

	public Result<string> SetSleepMode(SwitchAction action, CommandOrigin origin) => engine.RequestSleepMode(action, origin);

	/// Hands control back to automation immediately, without waiting for the hold to time out
	public Result<string> ReleaseHolds(CommandOrigin origin)
	{
		if (engine.View().Status != AutomationStatus.Holding) return Result.Ok("Nothing is being held");

		engine.ReleaseHolds($"requested by {origin}");
		return Result.Ok("Automation resumed");
	}

	public Result<string> SetAutomation(SwitchAction action, CommandOrigin origin)
	{
		bool target = action switch
		{
			SwitchAction.On => true,
			SwitchAction.Off => false,
			_ => !settings.Flag(SettingKey.AutomationEnabled)
		};

		Result<string> written = settings.Write(SettingKey.AutomationEnabled, target ? "On" : "Off");
		if (!written.IsOk) return written;

		bus.Publish(new AutomationEnabledChanged(target, origin, DateTimeOffset.Now));
		return Result.Ok(target ? "Automation on" : "Automation off");
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		Subscribe();
		engine.Start();

		using var timer = new PeriodicTimer(TickInterval);
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				engine.Tick();
			}
			catch (Exception ex)
			{
				CortanaLib.Runtime.Log.Error("Automation", $"Tick failed: {ex.Message}");
			}
		}
	}

	private void Subscribe()
	{
		bus.Subscribe<SensorReadingReceived>(_ => engine.OnSensorReading());
		bus.Subscribe<ComputerConnectionChanged>(fact =>
		{
			if (fact.Connected) engine.OnComputerConnected();
			else engine.OnComputerDisconnected();
		});
		bus.Subscribe<UserDeviceActionPerformed>(fact => engine.OnUserDeviceAction(fact.Device));
		bus.Subscribe<AutomationEnabledChanged>(fact => engine.OnAutomationChanged(fact.Enabled, fact.Origin));
		bus.Subscribe<SettingChanged>(fact => engine.OnSettingsChanged(fact.Setting));
	}
}
