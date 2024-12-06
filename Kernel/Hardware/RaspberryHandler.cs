using System.Net.NetworkInformation;
using Iot.Device.CpuTemperature;
using Kernel.Hardware.Utility;
using UnitsNet;

namespace Kernel.Hardware;

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

	internal static ELocation GetNetworkLocation() => NetworkAdapter.Location;
		
	internal static void PowerRaspberry(EPowerOption option)
	{
		Task.Run(async () =>
		{
			await Task.Delay(1000);
			switch (option)
			{
				case EPowerOption.Shutdown:
					Helper.RunScript("power", "shutdown");
					break;
				case EPowerOption.Reboot:
					Helper.RunScript("power", "reboot");
					break;
			}
		});
	}
}