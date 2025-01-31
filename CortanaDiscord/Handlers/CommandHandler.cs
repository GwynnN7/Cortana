using System.Reflection;
using CortanaDiscord.Utility;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CortanaDiscord.Handlers;

internal class CommandHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
{
	public async Task InitializeAsync()
	{
		Assembly discordBotAssembly = Assembly.Load(new AssemblyName("CortanaDiscord"));
		await commands.AddModulesAsync(discordBotAssembly, services);
		client.InteractionCreated += HandleInteraction;

		commands.SlashCommandExecuted += SlashCommandExecuted;
		commands.ContextCommandExecuted += ContextCommandExecuted;
		commands.ComponentCommandExecuted += ComponentCommandExecuted;
	}

	private static async Task ComponentCommandExecuted(ComponentCommandInfo arg1, IInteractionContext arg2, IResult arg3)
	{
		if (arg3.IsSuccess) return;
		switch (arg3.Error)
		{
			case InteractionCommandError.UnmetPrecondition:
				await arg2.Interaction.RespondAsync("Non hai l'autorizzazione per eseguire questo comando", ephemeral: true);
				break;
			case InteractionCommandError.UnknownCommand:
				await arg2.Interaction.RespondAsync("Mi dispiace, non conosco questo comando", ephemeral: true);
				break;
			case InteractionCommandError.BadArgs:
				await arg2.Interaction.RespondAsync("Mi dispiace, non ho capito cosa intendi", ephemeral: true);
				break;
			default:
				await arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
				break;
		}

		await DiscordUtils.SendToChannel<string>($"C'è stato un problema:\n```{arg3.Error}: {arg3.ErrorReason}```", ECortanaChannels.Log);
	}

	private static async Task ContextCommandExecuted(ContextCommandInfo arg1, IInteractionContext arg2, IResult arg3)
	{
		if (arg3.IsSuccess) return;
		switch (arg3.Error)
		{
			case InteractionCommandError.UnmetPrecondition:
				await arg2.Interaction.RespondAsync("Non hai l'autorizzazione per eseguire questo comando", ephemeral: true);
				break;
			case InteractionCommandError.UnknownCommand:
				await arg2.Interaction.RespondAsync("Mi dispiace, non conosco questo comando", ephemeral: true);
				break;
			case InteractionCommandError.BadArgs:
				await arg2.Interaction.RespondAsync("Mi dispiace, non ho capito cosa intendi", ephemeral: true);
				break;
			default:
				await arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
				break;
		}

		await DiscordUtils.SendToChannel($"C'è stato un problema:\n```{arg3.Error}: {arg3.ErrorReason}```", ECortanaChannels.Log);
	}

	private static async Task SlashCommandExecuted(SlashCommandInfo arg1, IInteractionContext arg2, IResult arg3)
	{
		if (arg3.IsSuccess) return;
		switch (arg3.Error)
		{
			case InteractionCommandError.UnmetPrecondition:
				await arg2.Interaction.RespondAsync("Non hai l'autorizzazione per eseguire questo comando", ephemeral: true);
				break;
			case InteractionCommandError.UnknownCommand:
				await arg2.Interaction.RespondAsync("Mi dispiace, non conosco questo comando", ephemeral: true);
				break;
			case InteractionCommandError.BadArgs:
				await arg2.Interaction.RespondAsync("Mi dispiace, non ho capito cosa intendi", ephemeral: true);
				break;
			default:
				await arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
				break;
		}

		await DiscordUtils.SendToChannel<string>($"C'è stato un problema:\n```{arg3.Error}: {arg3.ErrorReason}```", ECortanaChannels.Log);
	}

	private async Task HandleInteraction(SocketInteraction arg)
	{
		try
		{
			var ctx = new SocketInteractionContext(client, arg);
			await commands.ExecuteCommandAsync(ctx, services);
		}
		catch (Exception ex)
		{
			await DiscordUtils.SendToChannel<string>($"C'è stato un problema in HandleInteraction:\n```{ex.Message}```", ECortanaChannels.Log);
			if (arg.Type == InteractionType.ApplicationCommand) await arg.GetOriginalResponseAsync().ContinueWith(async msg => await msg.Result.DeleteAsync());
		}
	}
}