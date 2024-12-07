using Microsoft.AspNetCore.Mvc;
using Kernel.Hardware.Interfaces;
using Kernel.Hardware.Utility;

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
		return HardwareProxy.GetHardwareInfo(EHardwareInfo.Temperature);
	}

	[HttpGet("ip")]
	public string Ip()
	{
		return HardwareProxy.GetHardwareInfo(EHardwareInfo.Ip);
	}

	[HttpGet("gateway")]
	public string Gateway()
	{
		return HardwareProxy.GetHardwareInfo(EHardwareInfo.Gateway);
	}

	[HttpGet("location")]
	public string Location()
	{
		return HardwareProxy.GetHardwareInfo(EHardwareInfo.Location);
	}

	[HttpGet("shutdown")]
	public string Shutdown()
	{
		return HardwareProxy.SwitchRaspberry(EPowerOption.Shutdown);
	}

	[HttpGet("reboot")]
	public string Reboot()
	{
		return HardwareProxy.SwitchRaspberry(EPowerOption.Reboot);
	}
}