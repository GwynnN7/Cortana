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
        public void OpenDesktop()
        {
            Utility.Functions.NotifyPC("Hi");
        }
    }
}
