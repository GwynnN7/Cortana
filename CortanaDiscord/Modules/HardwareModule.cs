using CortanaDiscord.Utility;
using CortanaLib;
using CortanaLib.Structures;
using Discord;
using Discord.Interactions;

namespace CortanaDiscord.Modules;

[Group("hardware", "Gestione domotica")]
[RequireOwner]
public class HardwareModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("lamp", "Switch Lamp")]
	public async Task LightToggle()
	{
		string result = await ApiHandler.Post("toggle", "device", "lamp");
		Embed embed = DiscordUtils.CreateEmbed(result);
		await RespondAsync(embed: embed, ephemeral: true);
	}

	[SlashCommand("device", "Switch Device", runMode: RunMode.Async)]
	public async Task DeviceInteract([Summary("device", "Select Device")] EDevice element, [Summary("azione", "Select Action")] EPowerAction action)
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Post(action.ToString(), "device", element.ToString());

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("device-info", "Get Device Status", runMode: RunMode.Async)]
	public async Task DeviceStatus([Summary("device", "Select Device")] EDevice element)
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Get("device", element.ToString());
		
		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}

	[SlashCommand("command-raspberry", "Command Raspberry", runMode: RunMode.Async)]
	public async Task CommandRaspberry([Summary("option", "Select Option")] ERaspberryCommand option)
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Post(null, "raspberry", option.ToString());

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("raspberry-info", "Get Raspberry Info", runMode: RunMode.Async)]
	public async Task RaspberryInfo([Summary("info", "Select Info")] ERaspberryInfo info)
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Get("raspberry", info.ToString());

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("command-pc", "Interact with PC", runMode: RunMode.Async)]
	public async Task ComputerCommand([Summary("command", "Select Command")] EComputerCommand command, [Summary("args", "Insert Argument")] string? args = null)
	{
		await DeferAsync(true);

		string result = await ApiHandler.Post(args, "computer", command.ToString());

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("sensor", "Get Sensor Data", runMode: RunMode.Async)]
	public async Task SensorData([Summary("info", "Select Data")] ESensor info)
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Get( "sensor", info.ToString());

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("sleep", "Enter Sleep Mode", runMode: RunMode.Async)]
	public async Task Sleep()
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Post("", "device", "sleep");

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
}