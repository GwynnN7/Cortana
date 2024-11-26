using Microsoft.AspNetCore.Mvc;
using Processor;

namespace CortanaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AutomationController : ControllerBase
{
	[HttpGet]
	public IActionResult Get()
	{
		return Redirect("http://cortana-api.ddns.net:8080");
	}

	[HttpGet("{device}")]
	public IActionResult PowerDevice([FromRoute] string device, [FromQuery] string? t)
	{
		Hardware.SwitchFromString(device, t ?? "toggle");
		return Redirect("http://cortana-api.ddns.net:8080");
	}
}