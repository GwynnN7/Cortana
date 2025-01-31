using CortanaLib.Structures;

namespace CortanaLib;

public static class ApiHandler
{
    private static readonly HttpClient ApiClient;

    static ApiHandler()
    {
        var apiRoot = Environment.GetEnvironmentVariable("API_ROOT") ?? throw new CortanaException("Cortana API not set in env");
        
        ApiClient = new HttpClient();
        ApiClient.BaseAddress = new Uri(apiRoot);
    }

    public static async Task<string> Get(params string[] routes)
    {
        string route = string.Join("/", routes);
        using HttpResponseMessage result = await ApiClient.GetAsync(route);
        return await result.Content.ReadAsStringAsync();
    }
    
    public static async Task<string> Post(string? value, params string[] routes)
    {
        string route = string.Join("/", routes);
        HttpContent content = new StringContent(value ?? "");
        using HttpResponseMessage response = await ApiClient.PostAsync(route, content);
        return await response.Content.ReadAsStringAsync();
    }
}