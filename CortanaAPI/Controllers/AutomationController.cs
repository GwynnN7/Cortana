using Microsoft.AspNetCore.Mvc;
using Processor;

namespace CortanaAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AutomationController : ControllerBase
    {
        [HttpGet]
        public ContentResult Get()
        {
            string html = Software.LoadHtml("Documentation");
            html = html.Replace("{{route}}", "Automation API");
            return base.Content(html, "text/html");
        }

        [HttpGet("{device}")]
        public IActionResult PowerDevice([FromRoute] string device, [FromQuery] string? t)
        {
            Hardware.SwitchFromString(device, t ?? "toggle");
            return Redirect("http://cortana-api.ddns.net:8080/automation/");
        }
    }
}