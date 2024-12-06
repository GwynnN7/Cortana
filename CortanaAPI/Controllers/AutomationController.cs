using Kernel.Hardware.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CortanaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
internal class AutomationController : ControllerBase
{
	[HttpGet]
	public string Get()
	{
		return "Automation route: specify the device and the action to perform";
	}

	[HttpGet("{device}")]
	public string PowerDevice([FromRoute] string device, [FromQuery] string? t)
	{
		return HardwareProxy.SwitchDevice(device, t ?? "toggle");
	}
}