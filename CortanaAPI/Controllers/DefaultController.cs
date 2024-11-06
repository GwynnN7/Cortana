using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [Route("")]   
    [ApiController]
    public class DefaultController : ControllerBase
    {
        [HttpGet]
        public static string Get()
        {
            return  "Hi, I'm Cortana";
        }

        [HttpGet("notify")]
        public static string Notify()
        {
            string res = Utility.HardwareDriver.NotifyPC("I am online");
            return res == "0" ? "Notification sent" : res;
        }
        
        [HttpGet("location")]
        public static string Location()
        {
            return Utility.HardwareDriver.GetLocation();
        }
    }
}
