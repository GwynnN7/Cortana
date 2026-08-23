namespace CortanaLib.Structures;

public record PostCommand(string Command, string Args = "");
public record PostAction(string Action = "toggle");
public record PostValue(int Value);

public interface IApiResponse;

public record MessageResponse(string Message) : IApiResponse;
public record ErrorResponse(string Error) : IApiResponse;

public record DeviceResponse(string Device, string Status) : IApiResponse;
public record SensorResponse(string Sensor, string Value, string Unit) : IApiResponse;
public record SettingsResponse(string Setting, string Value) : IApiResponse;
public record SubfunctionResponse(string Subfunction, bool Running) : IApiResponse;

public record DeviceListResponse(IReadOnlyList<DeviceResponse> Devices) : IApiResponse;
public record SensorListResponse(IReadOnlyList<SensorResponse> Sensors) : IApiResponse;
public record SettingsListResponse(IReadOnlyList<SettingsResponse> Settings) : IApiResponse;
public record RaspberryListResponse(IReadOnlyList<SensorResponse> Info) : IApiResponse;
public record SubfunctionListResponse(IReadOnlyList<SubfunctionResponse> Subfunctions) : IApiResponse;

public record SystemStatusResponse(
	IReadOnlyList<DeviceResponse> Devices,
	IReadOnlyList<SensorResponse> Sensors,
	IReadOnlyList<SettingsResponse> Settings,
	IReadOnlyList<SensorResponse> Raspberry,
	IReadOnlyList<SubfunctionResponse> Subfunctions,
	DateTimeOffset Timestamp
) : IApiResponse;
