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

        [HttpGet("image")]
        public IActionResult GetImage()
        {
            byte[] b = System.IO.File.ReadAllBytes("../../Kernel/Storage/Assets/cortana.jpg");        
            return File(b, "image/jpeg");
        }

        [HttpGet("notify")]
        public IActionResult Notify([FromQuery] string? text)
        {
            Hardware.CommandPc(EComputerCommand.Notify, text ?? "Hi, I'm Cortana");
            return Redirect("http://cortana-api.ddns.net:8080/");
        }
    }
}
