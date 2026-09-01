using CortanaKernel.Domain.Activity;
using CortanaKernel.Domain.Automation;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Devices;
using CortanaKernel.Domain.Sensors;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Application;

public sealed class AutomationWorld(DeviceRegistry devices, SensorRegistry sensors, IComputerEndpoint computer, ActivityRegistry activity) : IAutomationWorld
{
	public PowerState DeviceState(DeviceId device) => devices.State(device);

	public bool ComputerConnected => computer.Connected;

	public int? Light => sensors.Light;

	public DateTimeOffset? LastMotionAt => sensors.LastMotionAt;

	public bool StationOnline => sensors.Online;

	public bool AirQualityWarning => sensors.AirQualityWarning;

	public bool DesktopBusy => activity.Current is { } current
		&& (current.Fullscreen || current.Category == ActivityCategory.Gaming);

	public bool DeskActive => computer.Connected && activity.Current is { Locked: false, IdleSeconds: 0 };
}

public sealed class AutomationEffects(
	Lazy<DeviceService> devices,
	NotificationService notifications,
	IEventBus bus) : IAutomationEffects
{
	public void SwitchDevice(DeviceId device, PowerState state, string reason) =>
		devices.Value.ApplyAutomatic(device, state, reason);

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
