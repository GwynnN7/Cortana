using System.Device.Gpio;
using CortanaKernel.Domain.Devices;
using CortanaKernel.Domain.Settings;
using CortanaKernel.Infrastructure.Raspberry;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Gpio;

/// The relays on the Pi's header
public sealed class GpioDeviceController : ILocalDeviceController, IDisposable
{
	private const int PowerPin = 23;
	private const int GenericPin = 24;
	private const int LampPin = 25;

	private static readonly TimeSpan PulseWidth = TimeSpan.FromMilliseconds(100);

	private readonly IReadOnlyDictionary<DeviceId, int> _pins;
	private readonly SettingsStore _settings;
	private readonly Lock _gate = new();
	private readonly Lock _pulseGate = new();
	private readonly HashSet<int> _open = [];

	private GpioController? _controller;

	public GpioDeviceController(RaspberryHost host, SettingsStore settings)
	{
		_settings = settings;
		_pins = host.Location == Location.Orvieto
			? new Dictionary<DeviceId, int> { [DeviceId.Lamp] = LampPin, [DeviceId.Power] = PowerPin, [DeviceId.Generic] = GenericPin }
			: new Dictionary<DeviceId, int> { [DeviceId.Lamp] = GenericPin, [DeviceId.Power] = PowerPin, [DeviceId.Generic] = GenericPin };

		try
		{
			_controller = new GpioController();
		}
		catch (Exception ex)
		{
			Log.Write("Gpio", $"No controller available, running without hardware: {ex.Message}");
		}
	}

	public bool Controls(DeviceId device) => _pins.ContainsKey(device);

	public IReadOnlyList<DeviceId> Linked(DeviceId device) =>
		!_pins.TryGetValue(device, out int pin) ? [device] : [.. _pins.Where(entry => entry.Value == pin).Select(entry => entry.Key)];

	public Result<string> Apply(DeviceId device, PowerState state)
	{
		if (!_pins.TryGetValue(device, out int pin)) return Result.Fail<string>($"{device} has no output on this machine");

		if (device == DeviceId.Lamp && _settings.Flag(SettingKey.LampUsesPulseRelay)) return Pulse(pin);

		return Write(pin, state == PowerState.On ? PinValue.High : PinValue.Low);
	}

	private Result<string> Pulse(int pin)
	{
		_ = Task.Run(() =>
		{
			lock (_pulseGate)
			{
				Write(pin, PinValue.High);
				Thread.Sleep(PulseWidth);
				Write(pin, PinValue.Low);
			}
		});

		return Result.Ok("pulsed");
	}

	private Result<string> Write(int pin, PinValue value)
	{
		lock (_gate)
		{
			if (_controller == null) return Result.Ok("no hardware");

			try
			{
				if (_open.Add(pin)) _controller.OpenPin(pin, PinMode.Output);
				_controller.Write(pin, value);
				return Result.Ok("written");
			}
			catch (Exception ex)
			{
				_open.Remove(pin);
				Log.Error("Gpio", $"Writing pin {pin} failed: {ex.Message}");
				return Result.Fail<string>($"The output for pin {pin} did not respond");
			}
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			if (_controller == null) return;

			foreach (int pin in _open)
			{
				try { _controller.ClosePin(pin); } catch { /* closing on shutdown */ }
			}

			_open.Clear();
			_controller.Dispose();
			_controller = null;
		}
	}
}
