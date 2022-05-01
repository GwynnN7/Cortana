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
            string result = Utility.HardwareDriver.ToggleLamp();
            return new Dictionary<string, string>() { { "data", result } };
        }

        [HttpGet("pc-power")]
        public Dictionary<string, string> PCPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchPC(Utility.Functions.TriggerStateFromString(state));
            return new Dictionary<string, string>() { { "data", result } };
        }

        [HttpGet("led-power")]
        public Dictionary<string, string> LEDPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchLED(Utility.Functions.TriggerStateFromString(state));
            return new Dictionary<string, string>() { { "data", result } };
        }

        [HttpGet("oled-power")]
        public Dictionary<string, string> OledPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchOLED(Utility.Functions.TriggerStateFromString(state));
            return new Dictionary<string, string>() { { "data", result } };
        }

        [HttpGet("oled-power")]
        public Dictionary<string, string> OutletsPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchOutlets(Utility.Functions.TriggerStateFromString(state));
            return new Dictionary<string, string>() { { "data", result } };
        }
    }
}
