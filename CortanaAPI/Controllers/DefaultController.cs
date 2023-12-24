using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [Route("cortana-api")]
    [ApiController]
    public class DefaultController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            return  "Hi, I\'m Cortana";
        }

        [HttpGet("notify")]
        public string Notify()
        {
            return Utility.Functions.NotifyPC("Hi, I\'m Cortana");
        }

        [HttpGet("temp")]
        public string Temperature()
        {
            return Utility.HardwareDriver.GetCPUTemperature();
        }
    }
}
