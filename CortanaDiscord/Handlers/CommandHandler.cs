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
		Assembly discordBotAssembly = Assembly.Load(new AssemblyName(nameof(CortanaDiscord)));
		await commands.AddModulesAsync(discordBotAssembly, services);
		client.InteractionCreated += HandleInteraction;

		commands.SlashCommandExecuted += SlashCommandExecuted;
		commands.ContextCommandExecuted += ContextCommandExecuted;
		commands.ComponentCommandExecuted += ComponentCommandExecuted;
	}

	private static Task ComponentCommandExecuted(ComponentCommandInfo _, IInteractionContext context, IResult result) =>
		HandleCommandError(context, result);

	private static Task ContextCommandExecuted(ContextCommandInfo _, IInteractionContext context, IResult result) =>
		HandleCommandError(context, result);

	private static Task SlashCommandExecuted(SlashCommandInfo _, IInteractionContext context, IResult result) =>
		HandleCommandError(context, result);

	private static async Task HandleCommandError(IInteractionContext context, IResult result)
	{
		if (result.IsSuccess) return;
		switch (result.Error)
		{
			case InteractionCommandError.UnmetPrecondition:
				await context.Interaction.RespondAsync("Non hai l'autorizzazione per eseguire questo comando", ephemeral: true);
				break;
			case InteractionCommandError.UnknownCommand:
				await context.Interaction.RespondAsync("Mi dispiace, non conosco questo comando", ephemeral: true);
				break;
			case InteractionCommandError.BadArgs:
				await context.Interaction.RespondAsync("Mi dispiace, non ho capito cosa intendi", ephemeral: true);
				break;
			default:
				await context.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
				break;
		}

		await DiscordUtils.SendToChannel<string>($"C'è stato un problema:\n```{result.Error}: {result.ErrorReason}```", ECortanaChannels.Log);
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