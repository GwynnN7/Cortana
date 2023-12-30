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

        [HttpGet("lamp/{state:string?}")]
        public string LightPower(string state = "toggle")
        {
            return Utility.HardwareDriver.SwitchLamp(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("general/{state:string?}")]
        public string GeneralPower(string state)
        {
            return Utility.HardwareDriver.SwitchGeneral(Utility.Functions.TriggerStateFromString(state));
        }


        [HttpGet("pc/{state:string?}")]
        public string PCPower(string state)
        {
            return Utility.HardwareDriver.SwitchPC(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("oled/{state:string?}")]
        public string OledPower(string state)
        {
            return Utility.HardwareDriver.SwitchOLED(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("plugs/{state:string?}")]
        public string OutletsPower(string state)
        {
            return Utility.HardwareDriver.SwitchOutlets(Utility.Functions.TriggerStateFromString(state));
        }

        [HttpGet("room/{state:string?}")]
        public string EverythingPower(string state)
        {
            var trigger = Utility.Functions.TriggerStateFromString(state);
            Utility.HardwareDriver.SwitchRoom(trigger);

            return "Done";
        }
    }
}