using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [Route("cortana-api/[controller]")]
    [ApiController]
    public class AutomationController : ControllerBase
    {
        [HttpGet]
        public string[] Get()
        {
            return new string[] { "light-toggle" };
        }

        [HttpGet("light-toggle")]
        public string LightToggle()
        {
            HardwareDriver.Driver.ToggleLight();
            return "Relay attivato";
        }

        [HttpGet("pc-power")]
        public string PCPower(string state)
        {
            HardwareDriver.Driver.SwitchPC(state);
            return "Relay attivato";
        }
    }
}
