using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CortanaKernel.Application;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Network;

/// Every announced sensor station, whatever hardware it runs on
public sealed class StationSource(SensorService sensors)
{
	private const int MaxBuffer = 8192;

	private readonly Lock _gate = new();
	private readonly Dictionary<string, StationConnection> _connections = new(StringComparer.OrdinalIgnoreCase);

	/// Switching an output that lives on a station rather than on the Pi's header
	public Result<string> Command(string source, string channel, PowerState state)
	{
		StationConnection? connection;
		lock (_gate) _connections.TryGetValue(source, out connection);

		if (connection is not { Alive: true }) return Result.Fail<string>($"{source} is not connected");

		string message = JsonSerializer.Serialize(
			new SourceCommand(Wire.Command, source, channel, state.ToString()), CortanaEnvironment.WireJson);

		return connection.Write(message + "\n")
			? Result.Ok($"{channel} {state.ToString().ToLowerInvariant()}")
			: Result.Fail<string>($"{source} did not accept the command");
	}

	public void Bind(Socket socket, string pending, string source)
	{
		StationConnection? previous;
		var connection = new StationConnection(socket, pending, source, json => Accept(source, json), Dropped);

		lock (_gate)
		{
			_connections.TryGetValue(source, out previous);
			_connections[source] = connection;
		}

		previous?.Close();
		sensors.SetSourceOnline(source, true);
	}

	private void Accept(string source, string json)
	{
		if (Facts(json) is { Count: > 0 } facts)
		{
			sensors.Describe(source, facts);
			return;
		}

		if (Readings(json) is not { Count: > 0 } readings) return;

		sensors.Observe(source, readings, DateTimeOffset.Now);
	}

	private static Dictionary<string, string>? Facts(string json)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(json);
			JsonElement root = document.RootElement;

			if (!root.TryGetProperty("type", out JsonElement type) || type.GetString() != Wire.Facts) return null;
			if (!root.TryGetProperty("values", out JsonElement values)) return null;

			var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (JsonProperty entry in values.EnumerateObject())
				facts[entry.Name] = entry.Value.ToString();

			return facts;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static Dictionary<string, double>? Readings(string json)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(json);
			JsonElement root = document.RootElement;

			if (!root.TryGetProperty("type", out JsonElement type) || type.GetString() != Wire.Reading) return null;
			if (!root.TryGetProperty("values", out JsonElement values)) return null;

			var readings = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

			foreach (JsonProperty entry in values.EnumerateObject())
				if (entry.Value.ValueKind == JsonValueKind.Number) readings[entry.Name] = entry.Value.GetDouble();
				else if (entry.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) readings[entry.Name] = entry.Value.GetBoolean() ? 1 : 0;

			return readings;
		}
		catch (JsonException ex)
		{
			Log.Write("Station", $"Dropping a malformed frame: {ex.Message}");
			return null;
		}
	}

	private void Dropped(StationConnection connection)
	{
		bool wasCurrent;
		lock (_gate)
		{
			wasCurrent = _connections.TryGetValue(connection.Source, out StationConnection? current) && ReferenceEquals(current, connection);
			if (wasCurrent) _connections.Remove(connection.Source);
		}

		if (wasCurrent) sensors.SetSourceOnline(connection.Source, false);
	}

	private sealed class StationConnection(Socket socket, string pending, string source, Action<string> accept, Action<StationConnection> dropped)
		: SocketClient(socket, "Station", pending)
	{
		private readonly StringBuilder _buffer = new();

		public string Source { get; } = source;

		public bool Write(string message) => Send(message);

		protected override void OnData(string chunk)
		{
			var frames = new List<string>();

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
							frames.Add(text[start..(index + 1)]);
							break;
					}
				}

				_buffer.Remove(0, consumed);
				if (_buffer.Length > MaxBuffer) _buffer.Clear();
			}

			foreach (string frame in frames) accept(frame);
		}

		protected override void OnClosed() => dropped(this);
	}
}
