using Carter;
using CortanaKernel.Hardware;
using CortanaKernel.Hardware.Structures;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CortanaKernel.API;

public class DeviceEndpoints: ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app) 
	{
		RouteGroupBuilder group = app.MapGroup("device/");
		
		group.MapGet("", Root);
		group.MapGet("{device}", DeviceStatus);
		group.MapPost("{device}", SwitchDevice);
		group.MapPost("sleep", Sleep);
	}
	
	private static Ok<string> Root()
	{
		return TypedResults.Ok("Device API");
	}
	
	private static StringOrNotFoundResult DeviceStatus(string device)
	{
		IOption<EDevice> dev = device.ToEnum<EDevice>();
		
		return dev.Match<StringOrNotFoundResult>(
			onSome: deviceVal => TypedResults.Ok($"{deviceVal} is {HardwareApi.Devices.GetPower(deviceVal)}"),
			onNone: () => TypedResults.NotFound("Device not found")
		);
	}
	
	private static StringOrNotFoundResult SwitchDevice(string device, [FromBody] string? trigger)
	{
		IOption<EDevice> dev = device.ToEnum<EDevice>();
		IOption<EPowerAction> action = trigger is null ? new Some<EPowerAction>(EPowerAction.Toggle) : trigger.ToEnum<EPowerAction>();

		StringResult result = dev.Match(
			onSome: deviceVal =>
			{
				return action.Match(
					onSome: value => HardwareApi.Devices.Switch(deviceVal, value),
					onNone: () => StringResult.Failure("Action not supported")
				);
			},
			onNone: () =>
			{
				if (device != "room") return StringResult.Failure("Device not supported");
				return action.Match(
					onSome: HardwareApi.Devices.SwitchRoom,
					onNone: () => StringResult.Failure("Action not supported")
				);
			}
		);
		
		return result.Match<StringOrNotFoundResult>(
			val => TypedResults.Ok(val),
			err => TypedResults.NotFound(err)
		);
	}
	
	private static Ok<string> Sleep()
	{
		HardwareApi.Devices.EnterSleepMode();
		return TypedResults.Ok("Entering sleep mode");
	}
}