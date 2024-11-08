using Microsoft.AspNetCore.Mvc;
using Processor;

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
            string res = Hardware.CommandPc(EComputerCommand.Notify, "I am online");
            return res == "0" ? "Notification sent" : res;
        }
        
        [HttpGet("location")]
        public string Location()
        {
            return Hardware.GetLocation();
        }
    }
}
