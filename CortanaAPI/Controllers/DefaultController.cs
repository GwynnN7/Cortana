using Microsoft.AspNetCore.Mvc;

namespace CortanaAPI.Controllers
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
            string res = Utility.HardwareDriver.NotifyPc("I am online");
            return res == "0" ? "Notification sent" : res;
        }
        
        [HttpGet("location")]
        public static string Location()
        {
            return Utility.HardwareDriver.GetLocation();
        }
    }
}
