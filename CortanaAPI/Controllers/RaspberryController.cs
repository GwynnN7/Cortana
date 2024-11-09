using Microsoft.AspNetCore.Mvc;
using Processor;

namespace CortanaAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class RaspberryController : ControllerBase
    {
        [HttpGet]
        public ContentResult Get()
        {
            string html = Software.LoadHtml("Documentation");
            html = html.Replace("{{route}}", "Raspberry API");
            return base.Content(html, "text/html");
        }

        [HttpGet("temperature")]
        public ContentResult Temperature()
        {
            string html = Software.LoadHtml("Info");
            html = html.Replace("{{type}}", "Temperature");
            html = html.Replace("{{value}}", Hardware.GetCpuTemperature());
            return base.Content(html, "text/html");
        }

        [HttpGet("ip")]
        public ContentResult Ip()
        {
            string html = Software.LoadHtml("Info");
            html = html.Replace("{{type}}", "IP");
            html = html.Replace("{{value}}", Hardware.GetPublicIp().Result);
            return base.Content(html, "text/html");
        }
        
        [HttpGet("gateway")]
        public ContentResult Gateway()
        {
            string html = Software.LoadHtml("Info");
            html = html.Replace("{{type}}", "Gateway");
            html = html.Replace("{{value}}", Hardware.GetDefaultGateway());
            return base.Content(html, "text/html");
        }
        
        [HttpGet("location")]
        public ContentResult Location()
        {
            string html = Software.LoadHtml("Info");
            html = html.Replace("{{type}}", "Location");
            html = html.Replace("{{value}}", Hardware.GetLocation());
            return base.Content(html, "text/html");
        }

        [HttpGet("shutdown")]
        public IActionResult Shutdown()
        {
            Hardware.PowerRaspberry(EPowerOption.Shutdown);
            return Redirect("http://cortana-api.ddns.net:8080/raspberry");
        }

        [HttpGet("reboot")]
        public IActionResult Reboot()
        {
            Hardware.PowerRaspberry(EPowerOption.Reboot);
            return Redirect("http://cortana-api.ddns.net:8080/raspberry");
        }
    }
}