using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [Route("cortana-api/[controller]")]
    [ApiController]
    public class AutomationController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            return "Automation API";
        }

        [HttpGet("lamp")]
        public string LightToggle()
        {
            string result = Utility.HardwareDriver.SwitchLamp(EHardwareTrigger.Toggle);
            return result;
        }

        [HttpGet("pc")]
        public string PCPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchPC(Utility.Functions.TriggerStateFromString(state));
            return result;
        }

        [HttpGet("oled")]
        public string OledPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchOLED(Utility.Functions.TriggerStateFromString(state));
            return result;
        }

        [HttpGet("outlets")]
        public string OutletsPower(string state)
        {
            string result = Utility.HardwareDriver.SwitchOutlets(Utility.Functions.TriggerStateFromString(state));
            return result;
        }

        [HttpGet("all")]
        public string EverythingPower(string state)
        {
            var trigger = Utility.Functions.TriggerStateFromString(state);
            Utility.HardwareDriver.SwitchRoom(trigger);

            return "Done";
        }
    }
}