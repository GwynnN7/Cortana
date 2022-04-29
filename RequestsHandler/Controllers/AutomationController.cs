using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [Route("cortana-api/[controller]")]
    [ApiController]
    public class AutomationController : ControllerBase
    {
        [HttpGet]
        public Dictionary<string, string> Get()
        {
            return new Dictionary<string, string>() { { "data", "Automation API" } };
        }

        [HttpGet("light-toggle")]
        public Dictionary<string, string> LightToggle()
        {
            HardwareDriver.Driver.ToggleLight();
            return new Dictionary<string, string>() { { "data", "Relay Attivato" } };
        }

        [HttpGet("pc-power")]
        public Dictionary<string, string> PCPower(string state)
        {
            string result = HardwareDriver.Driver.SwitchPC(state);
            return new Dictionary<string, string>() { { "data", result } };
        }

        [HttpGet("led")]
        public Dictionary<string, string> LED(string state)
        {
            HardwareDriver.Driver.SwitchLED(state);
            return new Dictionary<string, string>() { { "data", "Fatto" } };
        }
    }
}
