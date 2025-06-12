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

    static ApiHandler()
    {
        var apiRoot = DataHandler.Env("CORTANA_API");
        ApiClient = new HttpClient();
        ApiClient.BaseAddress = new Uri(apiRoot);
    }

    private static void SetContentType(string contentType)
    {
        ApiClient.DefaultRequestHeaders.Accept.Clear();
        ApiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
    }

    public static async Task<string> Get(string route)
    {
        SetContentType("text/plain");
        
        try
        {
            return await ApiClient.GetStringAsync(route);
        }
        catch
        {
            return "Cortana Offline";
        }
    }

    public static async Task<IOption<string>> GetOption(string route)
    {
        SetContentType("text/plain");
        
        try
        {
            var result = await ApiClient.GetStringAsync(route);
            return result != "" ? new Some<string>(result) : new None<string>();
        }
        catch
        {
            return new None<string>();
        }
    }
    
    public static async Task<string> Post(string route, object? body = null)
    {
        SetContentType("text/plain");
        
        HttpContent content = new StringContent(body?.Serialize() ?? "{}", Encoding.UTF8, "application/json");
        try
        {
            using HttpResponseMessage response = await ApiClient.PostAsync(route, content);
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return "Cortana Offline";
        }
    }
    
    public static async Task<IOption<T>> Get<T>(string route) where T : IApiResponse
    {
        SetContentType("application/json");
        
        try
        {
            var result = await ApiClient.GetFromJsonAsync<T>(route);
            return result != null ? new Some<T>(result) : new None<T>();
        }
        catch
        {
            return new None<T>();
        }
    }

    public static async Task<IOption<T>> GetOption<T>(string route) where T : IApiResponse
    {
        SetContentType("application/json");
        
        try
        {
            var result = await ApiClient.GetFromJsonAsync<T>(route);
            return result != null ? new Some<T>(result) : new None<T>();
        }
        catch
        {
            return new None<T>();
        }
    }
    
    public static async Task<IOption<T>> Post<T>(string route, object? body = null) where T : IApiResponse
    {
        SetContentType("application/json");
        
        HttpContent content = new StringContent(body?.Serialize() ?? "{}", Encoding.UTF8, "application/json");
        try
        {
            using HttpResponseMessage response = await ApiClient.PostAsync(route, content);
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
