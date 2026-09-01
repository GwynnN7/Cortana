using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CortanaKernel.Application;
using CortanaKernel.Domain.Sensors;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Network;

/// The ESP32 station
public sealed class Esp32SensorSource(SensorService sensors)
{
	private const int MaxBuffer = 8192;

	private readonly Lock _gate = new();
	private StationConnection? _connection;

	private sealed record Frame(int Motion, int Light, double Temperature, double Humidity, int Eco2, int Tvoc, double? AirQualityTemperature);

	public bool Connected
	{
		get { lock (_gate) return _connection is { Alive: true }; }
	}

	public void Bind(Socket socket, string pending)
	{
		StationConnection? previous;
		var connection = new StationConnection(socket, pending, Accept, Dropped);

		lock (_gate)
		{
			previous = _connection;
			_connection = connection;
		}

		previous?.Close();
		sensors.SetStationOnline(true);
	}

	private void Accept(Frame frame) =>
		sensors.Observe(new SensorReading(
			frame.Motion == 1, frame.Light, frame.Temperature, frame.Humidity, frame.Eco2, frame.Tvoc, DateTimeOffset.Now)
		{
			AirQualityTemperature = frame.AirQualityTemperature
		});

	private void Dropped(StationConnection connection)
	{
		bool wasCurrent;
		lock (_gate)
		{
			wasCurrent = ReferenceEquals(_connection, connection);
			if (wasCurrent) _connection = null;
		}

		if (wasCurrent) sensors.SetStationOnline(false);
	}

	private sealed class StationConnection(Socket socket, string pending, Action<Frame> accept, Action<StationConnection> dropped)
		: SocketClient(socket, "Station", pending)
	{
		private readonly StringBuilder _buffer = new();

		protected override void OnData(string chunk)
		{
			var frames = new List<Frame>();

			lock (_buffer)
			{
				_buffer.Append(chunk);
				string text = _buffer.ToString();

				var depth = 0;
				var start = -1;
				var consumed = 0;

				for (var index = 0; index < text.Length; index++)
				{
					switch (text[index])
					{
						case '{':
							if (depth++ == 0) start = index;
							break;

						case '}':
							if (depth == 0 || --depth != 0) break;

							consumed = index + 1;
							try
							{
								Frame? frame = JsonSerializer.Deserialize<Frame>(text[start..(index + 1)], CortanaEnvironment.WireJson);
								if (frame != null) frames.Add(frame);
							}
							catch (JsonException ex)
							{
								Log.Write("Station", $"Dropping a malformed frame: {ex.Message}");
							}

							break;
					}
				}

				_buffer.Remove(0, consumed);
				if (_buffer.Length > MaxBuffer) _buffer.Clear();
			}

			foreach (Frame frame in frames) accept(frame);
		}

		protected override void OnClosed() => dropped(this);
	}
}
