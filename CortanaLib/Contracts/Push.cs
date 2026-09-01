namespace CortanaLib.Contracts;

/// A browser subscription plus the per-device choices made in the dashboard
public sealed record PushDeviceRequest(
	string Endpoint,
	string P256dh,
	string Auth,
	bool AlertsOnly = true,
	IReadOnlyList<string>? Sources = null,
	bool StatusNotification = false,
	bool Vibrate = true);

public sealed record PushKeyResponse(string PublicKey);

public sealed record PushDevicesResponse(int Devices, string StatusLine);
