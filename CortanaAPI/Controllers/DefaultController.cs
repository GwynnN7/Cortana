using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [ApiController]
    public class DefaultController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            return  "Hi, I'm Cortana";
        }

        [HttpGet("notify")]
        public string Notify()
        {
            return Utility.Functions.NotifyPC("I am online");
        }

        [HttpGet("temp")]
        public string Temperature()
        {
            return Utility.HardwareDriver.GetCPUTemperature();
        }
    }
}
