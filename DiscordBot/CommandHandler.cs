using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Processor;

namespace DiscordBot;

public class CommandHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
{
	public async Task InitializeAsync()
	{
		Assembly discordBotAssembly = Assembly.Load(new AssemblyName("DiscordBot"));
		await commands.AddModulesAsync(discordBotAssembly, services);
		client.InteractionCreated += HandleInteraction;

		commands.SlashCommandExecuted += SlashCommandExecuted;
		commands.ContextCommandExecuted += ContextCommandExecuted;
		commands.ComponentCommandExecuted += ComponentCommandExecuted;
	}

	private static Task ComponentCommandExecuted(ComponentCommandInfo arg1, IInteractionContext arg2, IResult arg3)
	{
		if (arg3.IsSuccess) return Task.CompletedTask;
		switch (arg3.Error)
		{
			case InteractionCommandError.UnmetPrecondition:
				arg2.Interaction.RespondAsync("Non hai l'autorizzazione per eseguire questo comando", ephemeral: true);
				break;
			case InteractionCommandError.UnknownCommand:
				arg2.Interaction.RespondAsync("Mi dispiace, non conosco questo comando", ephemeral: true);
				break;
			case InteractionCommandError.BadArgs:
				arg2.Interaction.RespondAsync("Mi dispiace, non ho capito cosa intendi", ephemeral: true);
				break;
			default:
				arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
				break;
		}

		DiscordUtils.SendToChannel($"C'è stato un problema: {arg3.Error}: {arg3.ErrorReason}", ECortanaChannels.Log);

		return Task.CompletedTask;
	}

	private static Task ContextCommandExecuted(ContextCommandInfo arg1, IInteractionContext arg2, IResult arg3)
	{
		if (arg3.IsSuccess) return Task.CompletedTask;
		switch (arg3.Error)
		{
			case InteractionCommandError.UnmetPrecondition:
				arg2.Interaction.RespondAsync("Non hai l'autorizzazione per eseguire questo comando", ephemeral: true);
				break;
			case InteractionCommandError.UnknownCommand:
				arg2.Interaction.RespondAsync("Mi dispiace, non conosco questo comando", ephemeral: true);
				break;
			case InteractionCommandError.BadArgs:
				arg2.Interaction.RespondAsync("Mi dispiace, non ho capito cosa intendi", ephemeral: true);
				break;
			default:
				arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
				break;
		}

		DiscordUtils.SendToChannel($"C'è stato un problema: {arg3.Error}: {arg3.ErrorReason}", ECortanaChannels.Log);

		return Task.CompletedTask;
	}

	private static Task SlashCommandExecuted(SlashCommandInfo arg1, IInteractionContext arg2, IResult arg3)
	{
		if (arg3.IsSuccess) return Task.CompletedTask;
		switch (arg3.Error)
		{
			case InteractionCommandError.UnmetPrecondition:
				arg2.Interaction.RespondAsync("Non hai l'autorizzazione per eseguire questo comando", ephemeral: true);
				break;
			case InteractionCommandError.UnknownCommand:
				arg2.Interaction.RespondAsync("Mi dispiace, non conosco questo comando", ephemeral: true);
				break;
			case InteractionCommandError.BadArgs:
				arg2.Interaction.RespondAsync("Mi dispiace, non ho capito cosa intendi", ephemeral: true);
				break;
			default:
				arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
				break;
		}

		DiscordUtils.SendToChannel($"C'è stato un problema: {arg3.Error}: {arg3.ErrorReason}", ECortanaChannels.Log);

		return Task.CompletedTask;
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
			DiscordUtils.SendToChannel($"C'è stato un problema in HandleInteraction: {ex.StackTrace}", ECortanaChannels.Log);
			if (arg.Type == InteractionType.ApplicationCommand) await arg.GetOriginalResponseAsync().ContinueWith(async msg => await msg.Result.DeleteAsync());
		}
	}
}