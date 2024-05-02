using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [Route("")]   
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
            var res = Utility.HardwareDriver.NotifyPC("I am online");
            return res == "0" ? "Notification sent" : res;
        }
        
        [HttpGet("location")]
        public string Location()
        {
            return Utility.HardwareDriver.GetLocation();
        }
    }
}
