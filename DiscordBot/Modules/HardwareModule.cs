using Discord;
using Discord.Interactions;
using DiscordBot.Utility;
using Kernel.Hardware;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Utility;

namespace DiscordBot.Modules;

[Group("hardware", "Gestione domotica")]
[RequireOwner]
public class HardwareModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("lamp", "Switch Lamp")]
	public async Task LightToggle()
	{
		string result = HardwareApi.Devices.Switch(EDevice.Lamp, EPowerAction.Toggle);
		Embed embed = DiscordUtils.CreateEmbed(result);
		await RespondAsync(embed: embed, ephemeral: true);
	}

	[SlashCommand("device", "Switch Device", runMode: RunMode.Async)]
	public async Task DeviceInteract([Summary("device", "Select Device")] EDevice element, [Summary("azione", "Select Action")] EPowerAction action)
	{
		await DeferAsync(true);

		string result = HardwareApi.Devices.Switch(element, action);

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("device-info", "Get Device Status", runMode: RunMode.Async)]
	public async Task DeviceStatus([Summary("device", "Select Device")] EDevice element)
	{
		await DeferAsync(true);

		EPower result = HardwareApi.Devices.GetPower(element);
		Embed embed = DiscordUtils.CreateEmbed(result.ToString());
		await FollowupAsync(embed: embed, ephemeral: true);
	}

	[SlashCommand("command-raspberry", "Command Raspberry", runMode: RunMode.Async)]
	public async Task CommandRaspberry([Summary("option", "Select Option")] ERaspberryOption option)
	{
		await DeferAsync(true);

		string result = HardwareApi.Raspberry.Command(option);

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("raspberry-info", "Get Raspberry Info", runMode: RunMode.Async)]
	public async Task RaspberryInfo([Summary("info", "Select Info")] EHardwareInfo info)
	{
		await DeferAsync(true);

		string result = HardwareApi.Raspberry.GetHardwareInfo(info);

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("command-pc", "Interact with PC", runMode: RunMode.Async)]
	public async Task ComputerCommand([Summary("command", "Select Command")] EComputerCommand command, [Summary("args", "Insert Argument")] string? args = null)
	{
		await DeferAsync(true);

		string result = HardwareApi.Devices.CommandComputer(command, args);

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("sensor", "Get Sensor Data", runMode: RunMode.Async)]
	public async Task SensorData([Summary("info", "Select Data")] ESensor info)
	{
		await DeferAsync(true);

		string result = HardwareApi.Sensors.GetData(info);

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("sleep", "Enter Sleep Mode", runMode: RunMode.Async)]
	public async Task Sleep()
	{
		await DeferAsync(true);

		HardwareApi.Devices.EnterSleepMode();

		Embed embed = DiscordUtils.CreateEmbed("Entering Sleep Mode");
		await FollowupAsync(embed: embed, ephemeral: true);
	}
}