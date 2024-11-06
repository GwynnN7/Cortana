using Microsoft.AspNetCore.Mvc;
using Utility;

namespace RequestsHandler.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class RaspberryController : ControllerBase
    {
        [HttpGet]
        public static string Get()
        {
            return "Raspberry API, get temperature, netstats and console commands";
        }

        [HttpGet("temp")]
        public static string Temperature()
        {
            return Utility.HardwareDriver.GetCPUTemperature();
        }

        [HttpGet("ip")]
        public static string Ip()
        {
            return Utility.HardwareDriver.GetPublicIP().Result;
        }
        
        [HttpGet("gateway")]
        public static string Gateway()
        {
            return Utility.HardwareDriver.GetDefaultGateway();
        }

        [HttpGet("shutdown")]
        public static string Shutdown()
        {
            return Utility.HardwareDriver.PowerRaspberry(EPowerOption.Shutdown);
        }

        [HttpGet("reboot")]
        public static string Reboot()
        {
            return Utility.HardwareDriver.PowerRaspberry(EPowerOption.Reboot);
        }
    }
}