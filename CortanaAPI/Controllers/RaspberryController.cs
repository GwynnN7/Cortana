using Microsoft.AspNetCore.Mvc;
using Processor;

namespace CortanaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RaspberryController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Redirect("http://cortana-api.ddns.net:8080");
        }

        [HttpGet("temperature")]
        public string Temperature()
        {
            return Hardware.GetCpuTemperature();
        }

        [HttpGet("ip")]
        public string Ip()
        {
            return Hardware.GetPublicIp().Result;
        }
        
        [HttpGet("gateway")]
        public string Gateway()
        {
            return Hardware.GetDefaultGateway();
        }
        
        [HttpGet("location")]
        public string Location()
        {
            return Hardware.GetLocation();
        }

        [HttpGet("shutdown")]
        public IActionResult Shutdown()
        {
            Hardware.PowerRaspberry(EPowerOption.Shutdown);
            return Redirect("http://cortana-api.ddns.net:8080");
        }

        [HttpGet("reboot")]
        public IActionResult Reboot()
        {
            Hardware.PowerRaspberry(EPowerOption.Reboot);
            return Redirect("http://cortana-api.ddns.net:8080");
        }
    }
}