using Kernel.Hardware;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Utility;
using Microsoft.AspNetCore.Mvc;

namespace CortanaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ComputerController : ControllerBase
{
	[HttpGet]
	public string Get()
	{
		return "Utility route: specify the action to perform";
	}

	[HttpGet("notify")]
	public string Notify([FromQuery] string? text)
	{
		return HardwareApi.Devices.CommandComputer(EComputerCommand.Notify, text);
	}
	
	[HttpGet("shutdown")]
	public string Shutdown()
	{
		return HardwareApi.Devices.CommandComputer(EComputerCommand.Shutdown);
	}
	
	[HttpGet("suspend")]
	public string Suspend()
	{
		return HardwareApi.Devices.CommandComputer(EComputerCommand.Suspend);
	}
	
	[HttpGet("reboot")]
	public string Reboot()
	{
		return HardwareApi.Devices.CommandComputer(EComputerCommand.Reboot);
	}
	
	[HttpGet("swap-os")]
	public string SwapOs()
	{
		return HardwareApi.Devices.CommandComputer(EComputerCommand.SwapOs);
	}
	
	[HttpGet("command")]
	public string CommandPc([FromQuery] string? cmd)
	{
		return HardwareApi.Devices.CommandComputer(EComputerCommand.Command, cmd);
	}
}