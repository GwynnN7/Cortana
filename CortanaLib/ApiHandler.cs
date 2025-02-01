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
        var apiRoot = Environment.GetEnvironmentVariable("CORTANA_API") ?? throw new CortanaException("Cortana API not set in env");
        
        ApiClient = new HttpClient();
        ApiClient.BaseAddress = new Uri(apiRoot);
    }

    public static async Task<ResponseMessage> Get(string route)
    {
        try
        {
            return await ApiClient.GetFromJsonAsync<ResponseMessage>(route) ?? new ResponseMessage("Bad Response");
        }
        catch
        {
            return new ResponseMessage("Cortana Offline");
        }
    }
    
    public static async Task<ResponseMessage> Post(string route, object? body = null)
    {
        HttpContent content = new StringContent(body?.Serialize() ?? "{}", Encoding.UTF8, "application/json");
        try
        {
            using HttpResponseMessage response = await ApiClient.PostAsync(route, content);
            return await response.Content.ReadFromJsonAsync<ResponseMessage>() ?? new ResponseMessage("Bad Response");
        }
        catch
        {
            return new ResponseMessage("Cortana Offline");
        }
    }
}

// Requests
public record PostCommand(string Command, string Args = "");
public record PostAction(string Action = "toggle");
public record PostValue(int Value);

// Responses
public record ResponseMessage(string Response);
