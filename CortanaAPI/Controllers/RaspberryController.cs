using Microsoft.AspNetCore.Mvc;
using Utility;

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
            return HardwareDriver.GetCpuTemperature();
        }

        [HttpGet("ip")]
        public string Ip()
        {
            return HardwareDriver.GetPublicIp().Result;
        }
        
        [HttpGet("gateway")]
        public string Gateway()
        {
            return HardwareDriver.GetDefaultGateway();
        }

        [HttpGet("shutdown")]
        public string Shutdown()
        {
            return HardwareDriver.PowerRaspberry(EPowerOption.Shutdown);
        }

        [HttpGet("reboot")]
        public string Reboot()
        {
            return HardwareDriver.PowerRaspberry(EPowerOption.Reboot);
        }
    }
}