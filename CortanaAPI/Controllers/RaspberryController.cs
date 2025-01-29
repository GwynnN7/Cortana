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
		return HardwareApi.Raspberry.GetHardwareInfo(EHardwareInfo.Temperature);
	}

	[HttpGet("ip")]
	public string Ip()
	{
		return HardwareApi.Raspberry.GetHardwareInfo(EHardwareInfo.Ip);
	}

	[HttpGet("gateway")]
	public string Gateway()
	{
		return HardwareApi.Raspberry.GetHardwareInfo(EHardwareInfo.Gateway);
	}

	[HttpGet("location")]
	public string Location()
	{
		return HardwareApi.Raspberry.GetHardwareInfo(EHardwareInfo.Location);
	}

	[HttpGet("shutdown")]
	public string Shutdown()
	{
		return HardwareApi.Raspberry.Command(ERaspberryOption.Shutdown);
	}

	[HttpGet("reboot")]
	public string Reboot()
	{
		return HardwareApi.Raspberry.Command(ERaspberryOption.Reboot);
	}

	[HttpGet("update")]
	public string Update()
	{
		return HardwareApi.Raspberry.Command(ERaspberryOption.Update);
	}
}