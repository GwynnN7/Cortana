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
            return "Automation API, specify device to toggle and specify action to perform a different one";
        }

        [HttpGet("{device}")]
        public string PowerDevice([FromRoute] string device)
        {
            return Utility.HardwareDriver.SwitchFromString(device, "toggle");
        }

        [HttpGet("{device}/{state}")]
        public string PowerDevice([FromRoute] string device, [FromRoute] string state)
        {
            return Utility.HardwareDriver.SwitchFromString(device, state);
        }
    }
}