using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CortanaLib.Structures;

namespace CortanaLib;

public static class ApiHandler
{
	public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(40);
	private const string Offline = "Cortana Offline";

	private static readonly HttpClient ApiClient;
	private static readonly string? ApiKey;

	static ApiHandler()
	{
		ApiClient = new HttpClient
		{
			BaseAddress = new Uri(DataHandler.Env("CORTANA_API")),
			Timeout = System.Threading.Timeout.InfiniteTimeSpan
		};
		ApiKey = Environment.GetEnvironmentVariable("CORTANA_API_KEY");
	}

	private static HttpRequestMessage CreateRequest(HttpMethod method, string route, string accept, object? body = null)
	{
		var request = new HttpRequestMessage(method, route);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
		if (!string.IsNullOrEmpty(ApiKey)) request.Headers.Add("X-Api-Key", ApiKey);
		if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body, DataHandler.SerializerOptions), Encoding.UTF8, "application/json");
		return request;
	}

	private static async Task<string> SendText(HttpMethod method, string route, object? body, CancellationToken token)
	{
		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
			timeout.CancelAfter(Timeout);

			using HttpRequestMessage request = CreateRequest(method, route, "text/plain", body);
			using HttpResponseMessage response = await ApiClient.SendAsync(request, timeout.Token);
			string content = await response.Content.ReadAsStringAsync(timeout.Token);
			return string.IsNullOrWhiteSpace(content) && !response.IsSuccessStatusCode ? $"Error {(int)response.StatusCode}" : content;
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[ApiHandler] {method} {route} failed: {ex.Message}");
			return Offline;
		}
	}

	private static async Task<IOption<T>> SendJson<T>(HttpMethod method, string route, object? body, CancellationToken token) where T : IApiResponse
	{
		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
			timeout.CancelAfter(Timeout);

			using HttpRequestMessage request = CreateRequest(method, route, "application/json", body);
			using HttpResponseMessage response = await ApiClient.SendAsync(request, timeout.Token);
			if (!response.IsSuccessStatusCode) return new None<T>();

			var result = await response.Content.ReadFromJsonAsync<T>(DataHandler.SerializerOptions, timeout.Token);
			return result != null ? new Some<T>(result) : new None<T>();
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[ApiHandler] {method} {route} failed: {ex.Message}");
			return new None<T>();
		}
	}

	public static Task<string> Get(string route, CancellationToken token = default) =>
		SendText(HttpMethod.Get, route, null, token);

	public static Task<string> Post(string route, object? body = null, CancellationToken token = default) =>
		SendText(HttpMethod.Post, route, body ?? new { }, token);

	public static Task<string> Delete(string route, CancellationToken token = default) =>
		SendText(HttpMethod.Delete, route, null, token);

	public static Task<IOption<T>> Get<T>(string route, CancellationToken token = default) where T : IApiResponse =>
		SendJson<T>(HttpMethod.Get, route, null, token);

	public static Task<IOption<T>> Post<T>(string route, object? body = null, CancellationToken token = default) where T : IApiResponse =>
		SendJson<T>(HttpMethod.Post, route, body ?? new { }, token);

		public static async IAsyncEnumerable<T> Stream<T>(string route, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token = default) where T : IApiResponse
	{
		using HttpRequestMessage request = CreateRequest(HttpMethod.Get, route, "text/event-stream");

		using HttpResponseMessage response = await ApiClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
		response.EnsureSuccessStatusCode();

		await using Stream body = await response.Content.ReadAsStreamAsync(token);
		using var reader = new StreamReader(body);

		while (!token.IsCancellationRequested)
		{
			string? line = await reader.ReadLineAsync(token);
			if (line == null) yield break;
			if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

			T? parsed = JsonSerializer.Deserialize<T>(line[5..].Trim(), DataHandler.SerializerOptions);
			if (parsed != null) yield return parsed;
		}
	}

	public static async Task<IOption<string>> GetOption(string route, CancellationToken token = default)
	{
		string result = await SendText(HttpMethod.Get, route, null, token);
		return result is "" or Offline ? new None<string>() : new Some<string>(result);
	}
}
