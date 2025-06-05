using System.Net.Http.Json;
using System.Text;
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

    public static async Task<MessageResponse> Get(string route)
    {
        try
        {
            return await ApiClient.GetFromJsonAsync<MessageResponse>(route) ?? new MessageResponse("Bad Response");
        }
        catch
        {
            return new MessageResponse("Cortana Offline");
        }
    }

    public static async Task<IOption<string>> GetOption(string route)
    {
        try
        {
            var result = await ApiClient.GetFromJsonAsync<MessageResponse>(route);
            return result != null ? new Some<string>(result.Message) : new None<string>();
        }
        catch
        {
            return new None<string>();
        }
    }
    
    public static async Task<MessageResponse> Post(string route, object? body = null)
    {
        HttpContent content = new StringContent(body?.Serialize() ?? "{}", Encoding.UTF8, "application/json");
        try
        {
            using HttpResponseMessage response = await ApiClient.PostAsync(route, content);
            return await response.Content.ReadFromJsonAsync<MessageResponse>() ?? new MessageResponse("Bad Response");
        }
        catch
        {
            return new MessageResponse("Cortana Offline");
        }
    }
}

// Requests
public record PostCommand(string Command, string Args = "");
public record PostAction(string Action = "toggle");
public record PostValue(int Value);

// Responses
public record MessageResponse(string Message);
public record DeviceResponse(string Device, string Status);
public record SensorResponse(string Sensor, string Value, string Unit);
public record SettingsResponse(string Setting, string Value);
public record ErrorResponse(string Error);
