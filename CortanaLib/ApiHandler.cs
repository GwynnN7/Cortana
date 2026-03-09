using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CortanaLib.Extensions;
using CortanaLib.Structures;

namespace CortanaLib;

public static class ApiHandler
{
    private static readonly HttpClient ApiClient;
    private static readonly string? ApiKey;

    static ApiHandler()
    {
        var apiRoot = DataHandler.Env("CORTANA_API");
        ApiClient = new HttpClient();
        ApiClient.BaseAddress = new Uri(apiRoot);
        ApiKey = Environment.GetEnvironmentVariable("CORTANA_API_KEY");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string route, string accept, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, route);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        if (!string.IsNullOrEmpty(ApiKey)) request.Headers.Add("X-Api-Key", ApiKey);
        if (content != null) request.Content = content;
        return request;
    }

    public static async Task<string> Get(string route)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, route, "text/plain");
            using HttpResponseMessage response = await ApiClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return "Cortana Offline";
        }
    }

    public static async Task<IOption<string>> GetOption(string route)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, route, "text/plain");
            using HttpResponseMessage response = await ApiClient.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();
            return result != "" ? new Some<string>(result) : new None<string>();
        }
        catch
        {
            return new None<string>();
        }
    }

    public static async Task<string> Post(string route, object? body = null)
    {
        HttpContent content = new StringContent(body?.Serialize() ?? "{}", Encoding.UTF8, "application/json");
        try
        {
            using var request = CreateRequest(HttpMethod.Post, route, "text/plain", content);
            using HttpResponseMessage response = await ApiClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return "Cortana Offline";
        }
    }

    public static async Task<IOption<T>> Get<T>(string route) where T : IApiResponse
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, route, "application/json");
            using HttpResponseMessage response = await ApiClient.SendAsync(request);
            var result = await response.Content.ReadFromJsonAsync<T>();
            return result != null ? new Some<T>(result) : new None<T>();
        }
        catch
        {
            return new None<T>();
        }
    }

    public static async Task<IOption<T>> Post<T>(string route, object? body = null) where T : IApiResponse
    {
        HttpContent content = new StringContent(body?.Serialize() ?? "{}", Encoding.UTF8, "application/json");
        try
        {
            using var request = CreateRequest(HttpMethod.Post, route, "application/json", content);
            using HttpResponseMessage response = await ApiClient.SendAsync(request);
            var result = await response.Content.ReadFromJsonAsync<T>();
            return result != null ? new Some<T>(result) : new None<T>();
        }
        catch
        {
            return new None<T>();
        }
    }
}

// Requests
public record PostCommand(string Command, string Args = "");
public record PostAction(string Action = "toggle");
public record PostValue(int Value);


// Responses
public interface IApiResponse;
public record MessageResponse(string Message) : IApiResponse;
public record DeviceResponse(string Device, string Status) : IApiResponse;
public record SensorResponse(string Sensor, string Value, string Unit) : IApiResponse;
public record SettingsResponse(string Setting, string Value) : IApiResponse;
public record ErrorResponse(string Error) : IApiResponse;
