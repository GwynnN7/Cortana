using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class RaspberryController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            return "Raspberry API, get temperature, ip and console commands";
        }

        [HttpGet("temp")]
        public string Temperature()
        {
            return Utility.HardwareDriver.GetCPUTemperature();
        }

        [HttpGet("ip")]
        public string IP()
        {
            return Utility.HardwareDriver.GetPublicIP().Result;
        }
        
        [HttpGet("gateway")]
        public string Gateway()
        {
            return Utility.HardwareDriver.GetDefaultGateway();
        }

        [HttpGet("shutdown")]
        public string Shutdown()
        {
            return Utility.HardwareDriver.PowerRaspberry(EPowerOption.Shutdown);
        }

        [HttpGet("reboot")]
        public string Reboot()
        {
            return Utility.HardwareDriver.PowerRaspberry(EPowerOption.Reboot);
        }
    }
}