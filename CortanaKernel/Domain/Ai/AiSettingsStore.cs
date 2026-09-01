using System.Globalization;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Ai;

public interface IAiSettingsRepository
{
	IReadOnlyDictionary<AiSettingKey, double> Load();
	string LoadModel();
	void Save(IReadOnlyDictionary<AiSettingKey, double> values, string model);
}

/// Assistant and telemetry tuning
public sealed class AiSettingsStore(IAiSettingsRepository repository)
{
	private static readonly IReadOnlyDictionary<AiSettingKey, (double Default, double Min, double Max)> Bounds =
		new Dictionary<AiSettingKey, (double, double, double)>
		{
			[AiSettingKey.Temperature] = (0.9, 0, 2),
			[AiSettingKey.RememberedExchanges] = (8, 1, 40),
			[AiSettingKey.DiscordSessionMinutes] = (1, 0.5, 120),
			[AiSettingKey.HistorySampleMinutes] = (5, 1, 60),
			[AiSettingKey.HistoryRetentionDays] = (180, 1, 3650),
			[AiSettingKey.PushEventSeconds] = (5, 1, 120)
		};

	private readonly Dictionary<AiSettingKey, double> _values = Initialise(repository);
	private readonly Lock _gate = new();
	private string _model = repository.LoadModel();

	public event Action<AiSettingKey, double>? Changed;

	private static Dictionary<AiSettingKey, double> Initialise(IAiSettingsRepository repository)
	{
		Dictionary<AiSettingKey, double> values = Bounds.ToDictionary(entry => entry.Key, entry => entry.Value.Default);
		foreach ((AiSettingKey key, double value) in repository.Load())
			if (Bounds.TryGetValue(key, out (double Default, double Min, double Max) bound) && value >= bound.Min && value <= bound.Max)
				values[key] = value;

		return values;
	}

	public double Number(AiSettingKey key)
	{
		lock (_gate) return _values[key];
	}

	public int Integer(AiSettingKey key) => (int)Math.Round(Number(key));

	public string Model
	{
		get { lock (_gate) return _model; }
	}

	public void SetModel(string model)
	{
		lock (_gate)
		{
			_model = model;
			repository.Save(_values, _model);
		}
	}

	public string Read(AiSettingKey key) => Number(key).ToString("0.##", CultureInfo.InvariantCulture);

	public IReadOnlyList<AiSettingView> All() =>
		[.. Bounds.Keys.Select(key => new AiSettingView(key, Read(key)))];

	public Result<string> Write(AiSettingKey key, double value)
	{
		if (!Bounds.TryGetValue(key, out (double Default, double Min, double Max) bound)) return Result.Fail<string>($"Unknown setting '{key}'");

		if (value < bound.Min || value > bound.Max)
			return Result.Fail<string>($"{key} must be between {bound.Min:0.##} and {bound.Max:0.##}");

		lock (_gate)
		{
			_values[key] = value;
			repository.Save(_values, _model);
		}

		Changed?.Invoke(key, value);
		return Result.Ok($"{key} set to {Read(key)}");
	}
}
