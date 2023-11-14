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

        [HttpGet("notify-desktop")]
        public Dictionary<string, string> OpenDesktop()
        {
            var res = Utility.Functions.NotifyPC("Hi, I'm Cortana");
            return new Dictionary<string, string>() { { "data", res ? "Done" : "Error"} };
        }
    }
}
