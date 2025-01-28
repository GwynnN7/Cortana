using Kernel.Hardware;
using Kernel.Hardware.DataStructures;
using Microsoft.AspNetCore.Mvc;

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
		return HardwareAdapter.GetHardwareInfo(EHardwareInfo.Temperature);
	}

	[HttpGet("ip")]
	public string Ip()
	{
		return HardwareAdapter.GetHardwareInfo(EHardwareInfo.Ip);
	}

	[HttpGet("gateway")]
	public string Gateway()
	{
		return HardwareAdapter.GetHardwareInfo(EHardwareInfo.Gateway);
	}

	[HttpGet("location")]
	public string Location()
	{
		return HardwareAdapter.GetHardwareInfo(EHardwareInfo.Location);
	}

	[HttpGet("shutdown")]
	public string Shutdown()
	{
		return HardwareAdapter.CommandRaspberry(ERaspberryOption.Shutdown);
	}

	[HttpGet("reboot")]
	public string Reboot()
	{
		return HardwareAdapter.CommandRaspberry(ERaspberryOption.Reboot);
	}

	[HttpGet("update")]
	public string Update()
	{
		return HardwareAdapter.CommandRaspberry(ERaspberryOption.Update);
	}
}