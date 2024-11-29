using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Processor;

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
		return Hardware.CommandPc(EComputerCommand.Notify, text ?? $"Still alive at {Hardware.GetCpuTemperature()}");
	}
}