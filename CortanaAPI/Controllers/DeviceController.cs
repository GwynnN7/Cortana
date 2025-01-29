using Kernel.Hardware;
using Microsoft.AspNetCore.Mvc;

namespace CortanaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DeviceController : ControllerBase
{
	[HttpGet]
	public string Get()
	{
		return "Automation route: specify the device and the action to perform";
	}

	[HttpGet("{device}")]
	public string PowerDevice([FromRoute] string device, [FromQuery] string? t)
	{
		return HardwareApi.Devices.Switch(device, t ?? "toggle");
	}
	
	[HttpGet("status/{device}")]
	public string DeviceStatus([FromRoute] string device)
	{
		return HardwareApi.Devices.GetPower(device);
	}

	[HttpGet("sleep")]
	public string Sleep()
	{
		HardwareApi.Devices.EnterSleepMode();
		return "Entering sleep mode";
	}
}