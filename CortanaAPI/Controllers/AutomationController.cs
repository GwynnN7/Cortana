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

        [HttpGet("lamp/{state?}")]
        public string LightPower(string state = "toggle")
        {
            return Utility.HardwareDriver.SwitchLamp(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("general/{state?}")]
        public string GeneralPower(string state = "toggle")
        {
            return Utility.HardwareDriver.SwitchGeneral(Utility.Functions.TriggerStateFromString(state));
        }


        [HttpGet("pc/{state?}")]
        public string PCPower(string state = "toggle")
        {
            return Utility.HardwareDriver.SwitchPC(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("oled/{state?}")]
        public string OledPower(string state = "toggle")
        {
            return Utility.HardwareDriver.SwitchOLED(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("plugs/{state?}")]
        public string OutletsPower(string state = "toggle")
        {
            return Utility.HardwareDriver.SwitchOutlets(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("room/{state?}")]
        public string EverythingPower(string state = "toggle")
        {
            var trigger = Utility.Functions.TriggerStateFromString(state);
            Utility.HardwareDriver.SwitchRoom(trigger);

            return "Done";
        }
    }
}