using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [Route("cortana-api")]
    [ApiController]
    public class DefaultController : ControllerBase
    {
        [HttpGet]
        public Dictionary<string, string> Get()
        {
            return new Dictionary<string, string>() { { "data", "Hi, I'm Cortana" } };
        }

        [HttpGet("open-desktop")]
        public Dictionary<string, string> OpenDesktop()
        {
            using var client = new HttpClient();
            var result = client.GetAsync("http://192.168.1.17:5000/cortana-pc/show").Result;
            if(result.IsSuccessStatusCode) return new Dictionary<string, string>() { { "data", "Opening desktop application" } };
            else return new Dictionary<string, string>() { { "data", "Desktop application didn't respond" } };

        }
    }
}
