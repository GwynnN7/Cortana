using System.Net.NetworkInformation;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Structures;
using Iot.Device.CpuTemperature;
using UnitsNet;

namespace CortanaKernel.Hardware.Devices;

public static class RaspberryHandler
{
	public static async Task<string> RequestPublicIpv4()
	{
		using var client = new HttpClient();
		return await client.GetStringAsync("https://api.ipify.org");
	}

	public static double ReadCpuTemperature()
	{
		using var cpuTemperature = new CpuTemperature();
		List<(string Sensor, Temperature Temperature)> temperatures = cpuTemperature.ReadTemperatures();
		double average = temperatures.Sum(temp => temp.Temperature.DegreesCelsius);
		average /= temperatures.Count;
		return average;
	}

	public static string GetNetworkGateway()
	{
		IEnumerable<string> defaultGateway =
			from netInterfaces in NetworkInterface.GetAllNetworkInterfaces()
			from props in netInterfaces.GetIPProperties().GatewayAddresses
			where netInterfaces.OperationalStatus == OperationalStatus.Up
			select props.Address.ToString();
		return defaultGateway.First();
	}

	public static ELocation GetNetworkLocation() => Service.NetworkData.Location;
	public static int GetApiPort() => Service.NetworkData.ApiPort;

	public static void Shutdown() => RunCommandWithDelay("shutdown");
	public static void Reboot() => RunCommandWithDelay("reboot");
	public static void Update() => RunCommandWithDelay("update");
	
	private static void RunCommandWithDelay(string command)
	{
		Task.Run(async () =>
		{
			await Task.Delay(1000);
			Helper.RunCommand(DecodeCommand(command));
		});
	}
	
	public static string DecodeCommand(string command, string arg = "")
	{
		var sudo = $"echo {FileHandler.Secrets.CortanaPassword} | sudo -S";
		return command switch
		{
			"shutdown" => $"{sudo} shutdown now",
			"reboot" => $"{sudo} reboot",
			"update" => "cortana --restart",
			"wakeonlan" => $"{sudo} wakeonlan {arg}",
			"etherwake" => $"{sudo} etherwake {arg}",
			_ => ""
		};
	}
}