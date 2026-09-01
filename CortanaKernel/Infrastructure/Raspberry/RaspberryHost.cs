using System.Net.NetworkInformation;
using System.Net.Sockets;
using CortanaKernel.Domain.Services;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using Iot.Device.CpuTemperature;

namespace CortanaKernel.Infrastructure.Raspberry;

/// The Raspberry Pi itself
public sealed class RaspberryHost : IHostMachine
{
	private static readonly TimeSpan PublicIpCache = TimeSpan.FromMinutes(10);

	private readonly SemaphoreSlim _ipGate = new(1, 1);
	private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

	private string? _publicIp;
	private DateTime _publicIpAt = DateTime.MinValue;

	public RaspberryHost()
	{
		Gateway = ResolveGateway();
		Profile = NetworkProfile.Select(Gateway);
	}

	public NetworkProfile Profile { get; }

	public Location Location => Profile.Location;

	public string Gateway { get; }

	public double CpuTemperature
	{
		get
		{
			try
			{
				using var reader = new CpuTemperature();
				return reader.IsAvailable ? reader.Temperature.DegreesCelsius : 0;
			}
			catch (Exception)
			{
				return 0;
			}
		}
	}

	public async Task<string> PublicIpAddress(CancellationToken token = default)
	{
		if (_publicIp != null && DateTime.UtcNow - _publicIpAt < PublicIpCache) return _publicIp;

		await _ipGate.WaitAsync(token);
		try
		{
			if (_publicIp != null && DateTime.UtcNow - _publicIpAt < PublicIpCache) return _publicIp;

			_publicIp = (await _http.GetStringAsync("https://api.ipify.org", token)).Trim();
			_publicIpAt = DateTime.UtcNow;
			return _publicIp;
		}
		catch (Exception ex)
		{
			Log.Write("Raspberry", $"Could not resolve the public IP: {ex.Message}");
			return _publicIp ?? "Unavailable";
		}
		finally
		{
			_ipGate.Release();
		}
	}

	public Result<string> PowerOff()
	{
		Shell.StartDetached($"{Sudo()} shutdown now", TimeSpan.FromSeconds(1));
		return Result.Ok("Shutting down");
	}

	public Result<string> Reboot()
	{
		Shell.StartDetached($"{Sudo()} reboot", TimeSpan.FromSeconds(1));
		return Result.Ok("Rebooting");
	}

	public async Task<Result<string>> RunShellCommand(string command, CancellationToken token = default)
	{
		if (string.IsNullOrWhiteSpace(command)) return Result.Fail<string>("Empty command");

		try
		{
			return Result.Ok(await Shell.Run(command, TimeSpan.FromSeconds(20)));
		}
		catch (Exception ex)
		{
			return Result.Fail<string>($"The command failed: {ex.Message}");
		}
	}

	public Result<string> WakeComputer(string macAddress)
	{
		if (string.IsNullOrWhiteSpace(macAddress)) return Result.Fail<string>("No desktop MAC address is configured");

		// A magic packet needs no privileges, the wakeonlan/etherwake tools are a fallback
		try
		{
			byte[] mac = [.. macAddress.Split(':', '-').Select(part => Convert.ToByte(part, 16))];
			if (mac.Length != 6) return Result.Fail<string>($"'{macAddress}' is not a MAC address");

			byte[] packet = new byte[102];
			for (var index = 0; index < 6; index++) packet[index] = 0xFF;
			for (var repeat = 1; repeat <= 16; repeat++) mac.CopyTo(packet, repeat * 6);

			using var client = new UdpClient { EnableBroadcast = true };
			client.Send(packet, packet.Length, new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, 9));
		}
		catch (Exception ex)
		{
			Log.Write("Raspberry", $"The magic packet failed: {ex.Message}");
		}

		Shell.StartDetached($"{Sudo()} etherwake {macAddress}", TimeSpan.Zero);
		return Result.Ok("Magic packet sent");
	}

	public static bool Ping(string address)
	{
		if (string.IsNullOrWhiteSpace(address)) return false;

		try
		{
			using var ping = new Ping();
			return ping.Send(address, 2000).Status == IPStatus.Success;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string ResolveGateway()
	{
		IEnumerable<string> gateways =
			from adapter in NetworkInterface.GetAllNetworkInterfaces()
			where adapter.OperationalStatus == OperationalStatus.Up
			from gateway in adapter.GetIPProperties().GatewayAddresses
			where gateway.Address.AddressFamily == AddressFamily.InterNetwork
			select gateway.Address.ToString();

		return gateways.FirstOrDefault() ?? "0.0.0.0";
	}

	/// Prefer a passwordless sudoers rule, with the password variable as a fallback
	private static string Sudo()
	{
		string? password = CortanaEnvironment.Read("CORTANA_PASSWORD");
		return string.IsNullOrEmpty(password) ? "sudo -n" : $"echo {password} | sudo -S";
	}
}
