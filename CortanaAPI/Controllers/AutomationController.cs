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

        [HttpGet("lamp")]
        public Dictionary<string, string> LightToggle()
        {
            string result = Utility.HardwareDriver.SwitchLamp(EHardwareTrigger.Toggle);
            return new Dictionary<string, string>() { { "data", result } };
        }

        [HttpGet("amplifier")]
        public Dictionary<string, string> AmplifierPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchPC(Utility.Functions.TriggerStateFromString(state));
            return new Dictionary<string, string>() { { "data", result } };
        }


        [HttpGet("pc")]
        public Dictionary<string, string> PCPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchPC(Utility.Functions.TriggerStateFromString(state));
            return new Dictionary<string, string>() { { "data", result } };
        }

        [HttpGet("oled")]
        public Dictionary<string, string> OledPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchOLED(Utility.Functions.TriggerStateFromString(state));
            return new Dictionary<string, string>() { { "data", result } };
        }

        [HttpGet("outlets")]
        public Dictionary<string, string> OutletsPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchOutlets(Utility.Functions.TriggerStateFromString(state));
            return new Dictionary<string, string>() { { "data", result } };
        }

        [HttpGet("all")]
        public Dictionary<string, string> EverythingPower(string state)
        {
            var trigger = Utility.Functions.TriggerStateFromString(state);
            Utility.HardwareDriver.SwitchRoom(trigger);

            return new Dictionary<string, string>() { { "data", "Done" } };
        }
    }
}