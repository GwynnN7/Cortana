using Kernel.Hardware.Interfaces;
using Kernel.Hardware.Utility;
using Microsoft.AspNetCore.Mvc;

namespace CortanaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UtilityController : ControllerBase
{
	[HttpGet]
	public string Get()
	{
		return "Utility route: specify the action to perform";
	}

	[HttpGet("notify")]
	public string Notify([FromQuery] string? text)
	{
		return HardwareProxy.CommandComputer(EComputerCommand.Notify, text);
	}
}