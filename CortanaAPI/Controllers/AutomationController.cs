using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AutomationController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            return "Automation API => /[device]?state={on, off, toggle}";
        }

        [HttpGet("lamp")]
        public string LightToggle()
        {
            return Utility.HardwareDriver.SwitchLamp(EHardwareTrigger.Toggle);
        }

        [HttpGet("general")]
        public string GeneralPower(string state)
        {
            return Utility.HardwareDriver.SwitchGeneral(Utility.Functions.TriggerStateFromString(state));
        }


        [HttpGet("pc")]
        public string PCPower(string state)
        {
            return Utility.HardwareDriver.SwitchPC(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("oled")]
        public string OledPower(string state)
        {
            return Utility.HardwareDriver.SwitchOLED(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("plugs")]
        public string OutletsPower(string state)
        {
            return Utility.HardwareDriver.SwitchOutlets(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("room")]
        public string EverythingPower(string state)
        {
            var trigger = Utility.Functions.TriggerStateFromString(state);
            Utility.HardwareDriver.SwitchRoom(trigger);

            return "Done";
        }
    }
}