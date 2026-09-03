using System.Device.Gpio;
using CortanaKernel.Domain.Fabric;
using CortanaKernel.Domain.Settings;
using CortanaKernel.Infrastructure.Raspberry;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Gpio;

/// The relays on the Pi's header
public sealed class GpioDeviceController : IChannelWriter, IDisposable
{
	private static readonly int[] HeaderPins = [23, 24, 25];

	private static readonly TimeSpan PulseWidth = TimeSpan.FromMilliseconds(100);

	private readonly IReadOnlyDictionary<string, int> _pins;
	private readonly SettingsStore _settings;
	private readonly Lock _gate = new();
	private readonly Lock _pulseGate = new();
	private readonly HashSet<int> _open = [];

	private GpioController? _controller;

	public GpioDeviceController(RaspberryHost host, SettingsStore settings)
	{
		_settings = settings;
		_pins = Declared() ?? HeaderPins.ToDictionary(pin => $"pin{pin}", pin => pin, StringComparer.OrdinalIgnoreCase);

		try
		{
			_controller = new GpioController();
		}
		catch (Exception ex)
		{
			Log.Write("Gpio", $"No controller available, running without hardware: {ex.Message}");
		}
	}

	private static IReadOnlyDictionary<string, int>? Declared()
	{
		string path = CortanaEnvironment.Path_(CortanaFolder.Config, "CortanaKernel/Pins.json");
		Dictionary<string, int>? declared = JsonStore.Read<Dictionary<string, int>>(path);

		if (declared is not { Count: > 0 }) return null;

		Log.Write("Gpio", $"Using the declared pin map: {string.Join(", ", declared.Select(entry => $"{entry.Key}={entry.Value}"))}");
		return new Dictionary<string, int>(declared, StringComparer.OrdinalIgnoreCase);
	}

	public bool Handles(string source) => source.Equals(SourceIds.Raspberry, StringComparison.OrdinalIgnoreCase);

	public bool Controls(string channel) => _pins.ContainsKey(channel);

	public IReadOnlyList<string> Linked(string channel) =>
		!_pins.TryGetValue(channel, out int pin) ? [channel] : [.. _pins.Where(entry => entry.Value == pin).Select(entry => entry.Key)];

	public Result<string> Apply(string channel, PowerState state, bool pulse)
	{
		if (!_pins.TryGetValue(channel, out int pin)) return Result.Fail<string>($"{channel} has no output on this machine");

		if (pulse) return Pulse(pin);

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

	/// Closing a pin releases the line, and a released line stops holding its relay. Shutting the
	/// Kernel down must not switch the house, so the outputs are left exactly as they were written
	public void Dispose()
	{
		lock (_gate)
		{
			_open.Clear();
			_controller = null;
		}
	}
}
