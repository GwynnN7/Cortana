using Microsoft.AspNetCore.Mvc;

namespace CortanaAPI.Controllers
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
            string res = Utility.HardwareDriver.NotifyPc("I am online");
            return res == "0" ? "Notification sent" : res;
        }
        
        [HttpGet("location")]
        public string Location()
        {
            return Utility.HardwareDriver.GetLocation();
        }
    }
}
