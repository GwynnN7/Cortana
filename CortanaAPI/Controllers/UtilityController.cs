using Microsoft.AspNetCore.Mvc;
using Processor;

namespace CortanaAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class UtilityController : ControllerBase
	{
		[HttpGet]
		public IActionResult Get()
		{
			return Redirect("http://cortana-api.ddns.net:8080");
		}
        
		[HttpGet("notify")]
		public IActionResult Notify([FromQuery] string? text)
		{
			Hardware.CommandPc(EComputerCommand.Notify, text ?? "Hi, I\'m Cortana");
			return Redirect("http://cortana-api.ddns.net:8080");
		}
	}
}