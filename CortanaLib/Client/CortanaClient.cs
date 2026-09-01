using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaLib.Client;

/// The only way a client process talks to the Kernel. Wraps the public HTTP API and its two event streams.
public sealed class CortanaClient
{
	private const string Offline = "Cortana is offline";

	private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(40);
	private static readonly Lazy<CortanaClient> Shared = new(() => new CortanaClient(CortanaEnvironment.Require("CORTANA_API")));

	private readonly HttpClient _http;
	private readonly string? _apiKey;
	private CommandSurface _surface = CommandSurface.Api;

	public CortanaClient(string baseAddress, string? apiKey = null)
	{
		_http = new HttpClient
		{
			BaseAddress = new Uri(baseAddress.EndsWith('/') ? baseAddress : baseAddress + "/"),
			Timeout = Timeout.InfiniteTimeSpan
		};
		_apiKey = apiKey ?? CortanaEnvironment.Read("CORTANA_API_KEY");
	}

	public static CortanaClient Default => Shared.Value;

	/// Declares which client this is, so the Kernel can keep command origin meaningful
	public CortanaClient As(CommandSurface surface)
	{
		_surface = surface;
		return this;
	}

	// ---------- devices ----------

	public Task<Result<string>> SwitchDevice(DeviceId device, SwitchAction action) =>
		PostText($"devices/{device}", new SwitchRequest(action));

	public Task<Result<string>> SwitchRoom(SwitchAction action) =>
		PostText("devices/room", new SwitchRequest(action));

	public Task<Result<string>> Devices() => GetText("devices");

	// ---------- automation and sleep ----------

	public Task<Result<string>> SetAutomation(SwitchAction action) =>
		PostText("automation", new SwitchRequest(action));

	public Task<Result<string>> SetSleepMode(SwitchAction action) =>
		PostText("automation/sleep", new SwitchRequest(action));

	public Task<Result<string>> ReleaseHolds() => DeleteText("automation/holds");

	public Task<Result<AutomationDiagnostics>> Diagnostics() => GetJson<AutomationDiagnostics>("automation/diagnostics");

	// ---------- state ----------

	public Task<Result<CortanaSnapshot>> Snapshot(CancellationToken token = default) => GetJson<CortanaSnapshot>("snapshot", token);

	public IAsyncEnumerable<CortanaSnapshot> SnapshotStream(CancellationToken token = default) =>
		Stream<CortanaSnapshot>("events", token);

	public IAsyncEnumerable<NotificationEnvelope> NotificationStream(NotificationChannel channel, CancellationToken token = default) =>
		Stream<NotificationEnvelope>($"events/notifications?channel={channel}", token);

	// ---------- sensors and settings ----------

	public Task<Result<string>> Sensors() => GetText("sensors");

	public Task<Result<string>> Sensor(SensorId sensor) => GetText($"sensors/{sensor}");

	public Task<Result<string>> Settings() => GetText("settings");

	public Task<Result<string>> SetSetting(SettingKey setting, string value) =>
		PostText($"settings/{setting}", new SettingRequest(value));

	// ---------- machines ----------

	public Task<Result<string>> Computer(ComputerCommand command, string argument = "") =>
		PostText("computer", new ComputerRequest(command, argument));

	public Task<Result<string>> Raspberry(RaspberryCommand command, string argument = "") =>
		PostText("raspberry", new RaspberryRequest(command, argument));

	public Task<Result<string>> RaspberryInfo(RaspberryInfo info) => GetText($"raspberry/{info}");

	public Task<Result<MetricsView>> ComputerMetrics() => GetJson<MetricsView>("metrics/computer");

	public Task<Result<MetricsView>> RaspberryMetrics() => GetJson<MetricsView>("metrics/raspberry");

	public Task<Result<string>> PushMetrics(MachineSample sample) => PostText("metrics/computer", sample);

	// ---------- services ----------

	public Task<Result<string>> Services() => GetText("services");

	public Task<Result<string>> ControlService(ServiceId service, ServiceAction action) =>
		PostText($"services/{service}", new ServiceRequest(action));

	public Task<Result<string>> Journal(ServiceId service, int lines = 100) =>
		GetText($"services/{service}/journal?lines={lines}");

	// ---------- schedules ----------

	public Task<Result<ScheduleListResponse>> Schedules() => GetJson<ScheduleListResponse>("schedules");

	public Task<Result<string>> SchedulesText() => GetText("schedules");

	public Task<Result<string>> CreateSchedule(CreateScheduleRequest request) => PostText("schedules", request);

	public Task<Result<string>> CommandSchedule(string id, string command) =>
		PostText($"schedules/{id}", new ScheduleCommandRequest(command));

	public Task<Result<string>> DeleteSchedule(string id) => DeleteText($"schedules/{id}");

	// ---------- ai ----------

	public Task<Result<string>> Ask(string message, string conversation, string author = "", bool remember = true, bool trusted = true) =>
		PostText("ai", new AskRequest(message, conversation, author, remember, trusted));

	public Task<Result<string>> ResetConversation(string conversation) => DeleteText($"ai/{conversation}");

	public Task<Result<ModelListResponse>> Models() => GetJson<ModelListResponse>("ai/models");

	public Task<Result<string>> ModelsText() => GetText("ai/models");

	public Task<Result<string>> SetModel(string model) => PostText("ai/model", new ModelRequest(model));

	public Task<Result<string>> Prompt() => GetText("ai/prompt");

	public Task<Result<string>> SetPrompt(string prompt) => PostText("ai/prompt", new PromptRequest(prompt));

	public Task<Result<string>> ResetPrompt() => DeleteText("ai/prompt");

	public Task<Result<AiSettingListResponse>> AiSettings() => GetJson<AiSettingListResponse>("ai/settings");

	public Task<Result<string>> AiSettingsText() => GetText("ai/settings");

	public Task<Result<string>> AiSetting(AiSettingKey setting) => GetText($"ai/settings/{setting}");

	public Task<Result<string>> SetAiSetting(AiSettingKey setting, double value) =>
		PostText($"ai/settings/{setting}", new NumberRequest(value));

	// ---------- history ----------

	public Task<Result<HistorySeries>> History(string metric, int hours, DateTimeOffset? until = null) =>
		GetJson<HistorySeries>($"history/{metric}?hours={hours}" +
			(until is { } moment ? $"&until={Uri.EscapeDataString(moment.ToString("o"))}" : ""));

	public Task<Result<AnalysisResult>> Analyse(AnalysisRequest request) =>
		PostJson<AnalysisResult>("history/analysis", request);

	// ---------- notifications and push ----------

	public Task<Result<NotificationListResponse>> Notifications(int limit = 200) =>
		GetJson<NotificationListResponse>($"notifications?limit={limit}");

	public Task<Result<string>> ClearNotifications() => DeleteText("notifications");

	public Task<Result<string>> Notify(NotifyRequest request) => PostText("notifications", request);

	public Task<Result<string>> PushKey() => GetText("push/key");

	public Task<Result<string>> PushSubscribe(PushDeviceRequest device) => PostText("push", device);

	public Task<Result<string>> PushUnsubscribe(string endpoint) =>
		DeleteText("push", new PushDeviceRequest(endpoint, "", ""));

	public Task<Result<string>> PushTest() => PostText("push/test");

	// ---------- transport ----------

	public Task<Result<string>> GetText(string route, CancellationToken token = default) =>
		SendText(HttpMethod.Get, route, null, token);

	public Task<Result<string>> PostText(string route, object? body = null, CancellationToken token = default) =>
		SendText(HttpMethod.Post, route, body ?? new { }, token);

	public Task<Result<string>> DeleteText(string route, object? body = null, CancellationToken token = default) =>
		SendText(HttpMethod.Delete, route, body, token);

	private HttpRequestMessage Build(HttpMethod method, string route, string accept, object? body)
	{
		var request = new HttpRequestMessage(method, route);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
		request.Headers.Add("X-Cortana-Surface", _surface.ToString());
		if (!string.IsNullOrEmpty(_apiKey)) request.Headers.Add("X-Api-Key", _apiKey);
		if (body != null)
			request.Content = new StringContent(JsonSerializer.Serialize(body, CortanaEnvironment.WireJson), Encoding.UTF8, "application/json");
		return request;
	}

	private async Task<Result<string>> SendText(HttpMethod method, string route, object? body, CancellationToken token)
	{
		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
			timeout.CancelAfter(RequestTimeout);

			using HttpRequestMessage request = Build(method, route, "text/plain", body);
			using HttpResponseMessage response = await _http.SendAsync(request, timeout.Token);

			string content = (await response.Content.ReadAsStringAsync(timeout.Token)).Trim();
			if (response.IsSuccessStatusCode) return Result.Ok(content);

			return Result.Fail<string>(content.Length > 0 ? content : $"Error {(int)response.StatusCode}");
		}
		catch (Exception ex)
		{
			Log.Write("Client", $"{method} {route} failed: {ex.Message}");
			return Result.Fail<string>(Offline);
		}
	}

	public async Task<Result<T>> GetJson<T>(string route, CancellationToken token = default) =>
		await SendJson<T>(HttpMethod.Get, route, null, token);

	public async Task<Result<T>> PostJson<T>(string route, object? body, CancellationToken token = default) =>
		await SendJson<T>(HttpMethod.Post, route, body ?? new { }, token);

	private async Task<Result<T>> SendJson<T>(HttpMethod method, string route, object? body, CancellationToken token)
	{
		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
			timeout.CancelAfter(RequestTimeout);

			using HttpRequestMessage request = Build(method, route, "application/json", body);
			using HttpResponseMessage response = await _http.SendAsync(request, timeout.Token);

			string payload = await response.Content.ReadAsStringAsync(timeout.Token);
			if (!response.IsSuccessStatusCode) return Result.Fail<T>(Detail(payload, (int)response.StatusCode));

			T? parsed = JsonSerializer.Deserialize<T>(payload, CortanaEnvironment.WireJson);
			return parsed is null ? Result.Fail<T>("Empty response") : Result.Ok(parsed);
		}
		catch (Exception ex)
		{
			Log.Write("Client", $"{method} {route} failed: {ex.Message}");
			return Result.Fail<T>(Offline);
		}
	}

	private static string Detail(string payload, int status)
	{
		try
		{
			ProblemResponse? problem = JsonSerializer.Deserialize<ProblemResponse>(payload, CortanaEnvironment.WireJson);
			if (!string.IsNullOrWhiteSpace(problem?.Detail)) return problem.Detail;
		}
		catch (JsonException) { }

		return $"Error {status}";
	}

	/// Server-sent events
	private async IAsyncEnumerable<T> Stream<T>(string route, [EnumeratorCancellation] CancellationToken token)
	{
		using HttpRequestMessage request = Build(HttpMethod.Get, route, "text/event-stream", null);
		using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
		response.EnsureSuccessStatusCode();

		await using Stream body = await response.Content.ReadAsStreamAsync(token);
		using var reader = new StreamReader(body);

		while (!token.IsCancellationRequested)
		{
			string? line = await reader.ReadLineAsync(token);
			if (line == null) yield break;
			if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

			T? parsed;
			try
			{
				parsed = JsonSerializer.Deserialize<T>(line[5..].Trim(), CortanaEnvironment.WireJson);
			}
			catch (JsonException ex)
			{
				Log.Write("Client", $"Dropping malformed event: {ex.Message}");
				continue;
			}

			if (parsed != null) yield return parsed;
		}
	}
}
