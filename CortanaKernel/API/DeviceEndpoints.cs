using Carter;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.Extensions.Primitives;

namespace CortanaKernel.API;

public class DeviceEndpoints: ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app) 
	{
		RouteGroupBuilder group = app.MapGroup($"{ERoute.Devices}/");
		
		group.MapGet("", Root);
		group.MapGet("{device}", DeviceStatus);
		group.MapPost("{device}", SwitchDevice);
		group.MapPost("sleep", Sleep);
	}
	
	private static IResult Root(HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		return acceptHeader.Contains("text/plain") ? 
			TypedResults.Text("Device API", "text/plain") : 
			TypedResults.Json(new MessageResponse(Message: "Device API"));
	}
	
	private static IResult DeviceStatus(string device, HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		IOption<EDevice> dev = device.ToEnum<EDevice>();
		return dev.Match<IResult>(
			onSome: deviceVal =>
			{
				EPowerStatus status = HardwareApi.Devices.GetPower(deviceVal);
				return acceptHeader.Contains("text/plain")
					? TypedResults.Text($"{deviceVal} is {status}", "text/plain")
					: TypedResults.Json(new DeviceResponse(Device: deviceVal.ToString(), Status: status.ToString()));
			},
			onNone: () => acceptHeader.Contains("text/plain") ? 
				TypedResults.BadRequest("Device not found") :
				TypedResults.BadRequest(new ErrorResponse(Error: "Device not found"))
		);
	}
	
	private static IResult SwitchDevice(string device, PostAction? status, HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		IOption<EDevice> dev = device.ToEnum<EDevice>();
		IOption<EPowerAction> action = status is null || status.Action == "" ? new Some<EPowerAction>(EPowerAction.Toggle) : status.Action.ToEnum<EPowerAction>();

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
		
		return result.Match<IResult>(
			val =>
			{
				if (acceptHeader.Contains("text/plain")) {
					return TypedResults.Text($"{dev} switched {val}", "text/plain");
				}
				return TypedResults.Json(new DeviceResponse(Device: dev.ToString(), Status: val));
			},
			err => acceptHeader.Contains("text/plain") ? 
				TypedResults.BadRequest(err) :
				TypedResults.BadRequest(new ErrorResponse(Error: err))
		);
	}
	
	private static IResult Sleep(HttpRequest request)
	{
		HardwareApi.Devices.EnterSleepMode();
		StringValues acceptHeader = request.Headers.Accept;
		return acceptHeader.Contains("text/plain") ? 
			TypedResults.Text("Entering sleep mode", "text/plain") : 
			TypedResults.Json(new MessageResponse(Message: "Entering sleep mode"));
	}
}