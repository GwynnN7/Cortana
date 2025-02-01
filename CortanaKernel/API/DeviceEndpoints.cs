using Carter;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CortanaKernel.API;

public class DeviceEndpoints: ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app) 
	{
		RouteGroupBuilder group = app.MapGroup($"{ERoute.Device}/");
		
		group.MapGet("", Root);
		group.MapGet("{device}", DeviceStatus);
		group.MapPost("{device}", SwitchDevice);
		group.MapPost("sleep", Sleep);
	}
	
	private static Ok<ResponseMessage> Root()
	{
		return TypedResults.Ok(new ResponseMessage("Device API"));
	}
	
	private static StringOrFail DeviceStatus(string device)
	{
		IOption<EDevice> dev = device.ToEnum<EDevice>();
		
		return dev.Match<StringOrFail>(
			onSome: deviceVal => TypedResults.Ok(new ResponseMessage($"{deviceVal} is {HardwareApi.Devices.GetPower(deviceVal)}")),
			onNone: () => TypedResults.BadRequest(new ResponseMessage("Device not found"))
		);
	}
	
	private static StringOrFail SwitchDevice(string device, PostAction status)
	{
		IOption<EDevice> dev = device.ToEnum<EDevice>();
		IOption<EPowerAction> action = status.Action == "" ? new Some<EPowerAction>(EPowerAction.Toggle) : status.Action.ToEnum<EPowerAction>();

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
		
		return result.Match<StringOrFail>(
			val => TypedResults.Ok(new ResponseMessage(val)),
			err => TypedResults.BadRequest(new ResponseMessage(err))
		);
	}
	
	private static Ok<ResponseMessage> Sleep()
	{
		HardwareApi.Devices.EnterSleepMode();
		return TypedResults.Ok(new ResponseMessage("Entering sleep mode"));
	}
}