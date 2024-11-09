using Microsoft.AspNetCore.Mvc;
using Processor;

namespace CortanaAPI.Controllers
{
    [Route("")]   
    [ApiController]
    public class DefaultController : ControllerBase
    {
        [HttpGet]
        public ContentResult Get()
        {
            return base.Content(Software.LoadHtml("Home"), "text/html");
        }

        [HttpGet("notify")]
        public IActionResult Notify([FromQuery] string? text)
        {
            Hardware.CommandPc(EComputerCommand.Notify, text ?? "Hi, I'm Cortana");
            return Redirect("http://cortana-api.ddns.net:8080/");
        }
    }
}
