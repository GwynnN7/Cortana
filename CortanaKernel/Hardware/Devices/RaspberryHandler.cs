using System.Net.NetworkInformation;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Structures;
using Iot.Device.CpuTemperature;

namespace CortanaKernel.Hardware.Devices;

public static class RaspberryHandler
{
	private static readonly TimeSpan PublicIpCacheDuration = TimeSpan.FromMinutes(10);
	private static readonly SemaphoreSlim PublicIpLock = new(1, 1);
	private static readonly HttpClient IpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

	private static string? _cachedPublicIp;
	private static DateTime _cachedPublicIpTime = DateTime.MinValue;

	public static async Task<string> RequestPublicIpv4()
	{
		if (_cachedPublicIp != null && DateTime.Now - _cachedPublicIpTime < PublicIpCacheDuration) return _cachedPublicIp;

		await PublicIpLock.WaitAsync();
		try
		{
			if (_cachedPublicIp != null && DateTime.Now - _cachedPublicIpTime < PublicIpCacheDuration) return _cachedPublicIp;

			string ip = (await IpClient.GetStringAsync("https://api.ipify.org")).Trim();
			_cachedPublicIp = ip;
			_cachedPublicIpTime = DateTime.Now;
			return ip;
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[Raspberry] Could not resolve public IP: {ex.Message}");
			return _cachedPublicIp ?? "Unavailable";
		}
		finally
		{
			PublicIpLock.Release();
		}
	}

	public static double ReadCpuTemperature()
	{
		try
		{
			using var cpuTemperature = new CpuTemperature();
			return cpuTemperature.IsAvailable ? cpuTemperature.Temperature.DegreesCelsius : 0.0;
		}
		catch
		{
			return 0.0;
		}
	}

		public static string GetNetworkGateway()
	{
		IEnumerable<string> gateways =
			from netInterface in NetworkInterface.GetAllNetworkInterfaces()
			where netInterface.OperationalStatus == OperationalStatus.Up
			from props in netInterface.GetIPProperties().GatewayAddresses
			where props.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
			select props.Address.ToString();

		return gateways.FirstOrDefault() ?? "0.0.0.0";
	}

	public static ELocation GetNetworkLocation() => AutomationService.NetworkData.Location;

	public static void Shutdown() => Helper.DelayCommand(DecodeCommand("shutdown"));
	public static void Reboot() => Helper.DelayCommand(DecodeCommand("reboot"));

	public static async Task<StringResult> RunShellCommand(string command)
	{
		if (string.IsNullOrWhiteSpace(command)) return StringResult.Failure("Empty command");

		try
		{
			string output = await Helper.RunCommandWithOutput(command, TimeSpan.FromSeconds(20));
			return StringResult.Success(string.IsNullOrWhiteSpace(output) ? "Command executed" : output);
		}
		catch (Exception ex)
		{
			return StringResult.Failure($"Command failed: {ex.Message}");
		}
	}

		private static string SudoPrefix()
	{
		string? password = DataHandler.EnvOrNull("CORTANA_PASSWORD");
		if (string.IsNullOrEmpty(password)) return "sudo -n";

		return $"echo {password} | sudo -S";
	}

	public static string DecodeCommand(string command, string arg = "")
	{
		string sudo = SudoPrefix();
		return command switch
		{
			"shutdown" => $"{sudo} shutdown now",
			"reboot" => $"{sudo} reboot",
			"wakeonlan" => $"{sudo} wakeonlan {arg}",
			"etherwake" => $"{sudo} etherwake {arg}",
			_ => ""
		};
	}
}
