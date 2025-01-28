using System.Net.NetworkInformation;
using Iot.Device.CpuTemperature;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Utility;
using UnitsNet;

namespace Kernel.Hardware.Devices;

internal static class RaspberryHandler
{
	internal static async Task<string> RequestPublicIpv4()
	{
		using var client = new HttpClient();
		return await client.GetStringAsync("https://api.ipify.org");
	}

	internal static double ReadCpuTemperature()
	{
		using var cpuTemperature = new CpuTemperature();
		List<(string Sensor, Temperature Temperature)> temperatures = cpuTemperature.ReadTemperatures();
		double average = temperatures.Sum(temp => temp.Temperature.DegreesCelsius);
		average /= temperatures.Count;
		return average;
	}

	internal static string GetNetworkGateway()
	{
		IEnumerable<string> defaultGateway =
			from netInterfaces in NetworkInterface.GetAllNetworkInterfaces()
			from props in netInterfaces.GetIPProperties().GatewayAddresses
			where netInterfaces.OperationalStatus == OperationalStatus.Up
			select props.Address.ToString();
		return defaultGateway.First();
	}

	internal static ELocation GetNetworkLocation() => Service.NetworkData.Location;

	internal static void Shutdown() => RunCommandWithDelay("shutdown");
	internal static void Reboot() => RunCommandWithDelay("reboot");
	internal static void Update() => RunCommandWithDelay("update");
	
	private static void RunCommandWithDelay(string command)
	{
		Task.Run(async () =>
		{
			await Task.Delay(1000);
			Helper.RunCommand(DecodeCommand(command));
		});
	}
	
	internal static string DecodeCommand(string command, string arg = "")
	{
		var sudo = $"echo {Software.FileHandler.Secrets.CortanaPassword} | sudo -S";
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