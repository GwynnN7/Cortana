using Microsoft.AspNetCore.Mvc;
using Processor;

namespace CortanaAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class RaspberryController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            return "Raspberry API, get temperature, netstats and console commands";
        }

        [HttpGet("temp")]
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

        [HttpGet("shutdown")]
        public string Shutdown()
        {
            return Hardware.PowerRaspberry(EPowerOption.Shutdown);
        }

        [HttpGet("reboot")]
        public string Reboot()
        {
            return Hardware.PowerRaspberry(EPowerOption.Reboot);
        }
    }
}