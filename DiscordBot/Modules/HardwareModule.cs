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
	[SlashCommand("lamp", "Accendi o spegni la luce")]
	public async Task LightToggle()
	{
		string result = HardwareApi.Devices.Switch(EDevice.Lamp, EPowerAction.Toggle);
		Embed embed = DiscordUtils.CreateEmbed(result);
		await RespondAsync(embed: embed, ephemeral: true);
	}

	[SlashCommand("hardware", "Interagisci con i dispositivi hardware", runMode: RunMode.Async)]
	public async Task HardwareInteract([Summary("dispositivo", "Con cosa vuoi interagire?")] EDevice element, [Summary("azione", "Cosa vuoi fare?")] EPowerAction action)
	{
		await DeferAsync(true);

		string result = HardwareApi.Devices.Switch(element, action);

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}

	[SlashCommand("info", "Ricevi informazioni sull'hardware", runMode: RunMode.Async)]
	public async Task HardwareInfo([Summary("info", "Quale informazione vuoi?")] EHardwareInfo info)
	{
		await DeferAsync(true);

		string result = HardwareApi.Raspberry.GetHardwareInfo(info);

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
	
	[SlashCommand("sensor", "Ricevi informazioni dai sensori", runMode: RunMode.Async)]
	public async Task SensorInfo([Summary("info", "Quale informazione vuoi?")] ESensor info)
	{
		await DeferAsync(true);

		string result = HardwareApi.Sensors.GetData(info);

		Embed embed = DiscordUtils.CreateEmbed(result);
		await FollowupAsync(embed: embed, ephemeral: true);
	}
}