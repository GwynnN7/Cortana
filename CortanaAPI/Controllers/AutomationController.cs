using Microsoft.AspNetCore.Mvc;
using Processor;

namespace CortanaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AutomationController : ControllerBase
{
	[HttpGet]
	public string Get()
	{
		return "Automation route: specify the device and the action to perform";
	}

	[HttpGet("{device}")]
	public string PowerDevice([FromRoute] string device, [FromQuery] string? t)
	{
		return Hardware.SwitchFromString(device, t ?? "toggle");
	}
}