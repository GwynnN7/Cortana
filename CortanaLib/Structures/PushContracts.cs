namespace CortanaLib.Structures;

public record PostPushDevice(string Endpoint, string P256dh, string Auth, bool AlertsOnly = true, IReadOnlyList<string>? Sources = null, bool Sticky = false, bool Vibrate = true);
public record PushKeyResponse(string PublicKey) : IApiResponse;
public record PushDeviceListResponse(int Devices) : IApiResponse;
