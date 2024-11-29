using Microsoft.AspNetCore.Mvc;
using Processor;

namespace CortanaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RaspberryController : ControllerBase
{
	[HttpGet]
	public string Get()
	{
		return "Raspberry route: specify the action to perform on the device";
	}

	[HttpGet("temperature")]
	public string Temperature()
	{
		return Hardware.GetCpuTemperature();
	}

	[HttpGet("ip")]
	public string Ip()
	{
		return Hardware.GetPublicIp().Result;
	}

	[HttpGet("gateway")]
	public string Gateway()
	{
		return Hardware.GetDefaultGateway();
	}

	[HttpGet("location")]
	public string Location()
	{
		return Hardware.GetLocation();
	}

	[HttpGet("shutdown")]
	public string Shutdown()
	{
		return Hardware.PowerRaspberry(EPowerOption.Shutdown);
	}

	[HttpGet("reboot")]
	public string Reboot()
	{
		return Hardware.PowerRaspberry(EPowerOption.Reboot);
	}
}