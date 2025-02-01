using System.Net.Http.Json;
using System.Text;
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

    public static async Task<string> Get(params string[] routes)
    {
        string route = string.Join("/", routes);
        try
        {
            return await ApiClient.GetFromJsonAsync<string>(route) ?? "Error reading response data";
        }
        catch
        {
            return "Error reading response data";
        }
    }
    
    public static async Task<string> Post(string? value, params string[] routes)
    {
        string route = string.Join("/", routes);
        HttpContent content = new StringContent(value ?? "", Encoding.UTF8, "text/plain");
        try
        {
            using HttpResponseMessage response = await ApiClient.PostAsync(route, content);
            return await response.Content.ReadFromJsonAsync<string>() ?? "Error reading response data";
        }
        catch
        {
            return "Error reading response data";
        }
    }
}