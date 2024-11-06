using Microsoft.AspNetCore.Mvc;
using Utility;

namespace CortanaAPI.Controllers
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
            return HardwareDriver.GetCpuTemperature();
        }

        [HttpGet("ip")]
        public static string Ip()
        {
            return HardwareDriver.GetPublicIp().Result;
        }
        
        [HttpGet("gateway")]
        public static string Gateway()
        {
            return HardwareDriver.GetDefaultGateway();
        }

        [HttpGet("shutdown")]
        public static string Shutdown()
        {
            return HardwareDriver.PowerRaspberry(EPowerOption.Shutdown);
        }

        [HttpGet("reboot")]
        public static string Reboot()
        {
            return HardwareDriver.PowerRaspberry(EPowerOption.Reboot);
        }
    }
}