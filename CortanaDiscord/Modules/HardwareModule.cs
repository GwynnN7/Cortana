﻿using CortanaDiscord.Utility;
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
		string result = await ApiHandler.Post($"{ERoute.Devices}/{EDevice.Lamp}");
		Embed embed = DiscordUtils.CreateEmbed(result);
		await RespondAsync(embed: embed, ephemeral: true);
	}

	[SlashCommand("device", "Switch Device", runMode: RunMode.Async)]
	public async Task DeviceInteract([Summary("device", "Select Device")] EDevice device, [Summary("azione", "Select Action")] EPowerAction action)
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Post($"{ERoute.Devices}/{device}", new PostAction(action.ToString()));

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("device-info", "Get Device Status", runMode: RunMode.Async)]
	public async Task DeviceStatus([Summary("device", "Select Device")] EDevice device)
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Get($"{ERoute.Devices}/{device}");
		
		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}

	[SlashCommand("command-raspberry", "Interact with Raspberry", runMode: RunMode.Async)]
	public async Task CommandRaspberry([Summary("option", "Select Option")] ERaspberryCommand command, [Summary("args", "Insert Argument")] string args = "")
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand(command.ToString(), args));

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("raspberry-info", "Get Raspberry Info", runMode: RunMode.Async)]
	public async Task RaspberryInfo([Summary("info", "Select Info")] ERaspberryInfo info)
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Get($"{ERoute.Raspberry}/{info}");

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("command-pc", "Interact with PC", runMode: RunMode.Async)]
	public async Task ComputerCommand([Summary("command", "Select Command")] EComputerCommand command, [Summary("args", "Insert Argument")] string args = "")
	{
		await DeferAsync(true);

		string result = await ApiHandler.Post($"{ERoute.Computer}", new PostCommand(command.ToString(), args));

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("sensor", "Get Sensor Data", runMode: RunMode.Async)]
	public async Task SensorData([Summary("info", "Select Data")] ESensor info)
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Get($"{ERoute.Sensors}/{info}");

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("sleep", "Enter Sleep Mode", runMode: RunMode.Async)]
	public async Task Sleep()
	{
		await DeferAsync(true);
		
		string result = await ApiHandler.Post($"{ERoute.Devices}/sleep");

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
}