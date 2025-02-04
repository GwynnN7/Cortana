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

	public static void Shutdown() => Helper.DelayCommand(DecodeCommand("shutdown"));
	public static void Reboot() => Helper.DelayCommand(DecodeCommand("reboot"));
	
	public static string DecodeCommand(string command, string arg = "")
	{
		var sudo = $"echo {DataHandler.Env("CORTANA_PASSWORD")} | sudo -S";
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