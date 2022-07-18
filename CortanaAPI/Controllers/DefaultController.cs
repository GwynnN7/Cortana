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
            var result = Utility.Functions.RequestPC("show");
            if(result) return new Dictionary<string, string>() { { "data", "Opening desktop application" } };
            else return new Dictionary<string, string>() { { "data", "Desktop application didn't respond" } };
        }
    }
}
