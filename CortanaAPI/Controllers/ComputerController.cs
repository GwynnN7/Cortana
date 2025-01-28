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
		return HardwareAdapter.CommandComputer(EComputerCommand.Notify, text);
	}
	
	[HttpGet("shutdown")]
	public string Shutdown()
	{
		return HardwareAdapter.CommandComputer(EComputerCommand.Shutdown);
	}
	
	[HttpGet("suspend")]
	public string Suspend()
	{
		return HardwareAdapter.CommandComputer(EComputerCommand.Suspend);
	}
	
	[HttpGet("reboot")]
	public string Reboot()
	{
		return HardwareAdapter.CommandComputer(EComputerCommand.Reboot);
	}
	
	[HttpGet("swap-os")]
	public string SwapOs()
	{
		return HardwareAdapter.CommandComputer(EComputerCommand.SwapOs);
	}
	
	[HttpGet("command")]
	public string CommandPc([FromQuery] string? cmd)
	{
		return HardwareAdapter.CommandComputer(EComputerCommand.Command, cmd);
	}
}