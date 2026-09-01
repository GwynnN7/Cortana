using System.Reflection;
using CortanaLib.Runtime;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CortanaDiscord.Runtime;

/// Wires the slash-command modules to the gateway and turns failures into a readable reply
public sealed class CommandHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
{
	public async Task Initialise()
	{
		await commands.AddModulesAsync(Assembly.GetExecutingAssembly(), services);

		client.InteractionCreated += Handle;
		commands.SlashCommandExecuted += (_, context, result) => Report(context, result);
		commands.ComponentCommandExecuted += (_, context, result) => Report(context, result);
		commands.ContextCommandExecuted += (_, context, result) => Report(context, result);
	}

	private async Task Handle(SocketInteraction interaction)
	{
		try
		{
			await commands.ExecuteCommandAsync(new SocketInteractionContext(client, interaction), services);
		}
		catch (Exception ex)
		{
			Log.Error("Discord", $"Interaction failed: {ex.Message}");
			await DiscordContext.Post($"Something went wrong handling that:\n```{ex.Message}```", CortanaChannel.Log);
		}
	}

	private static async Task Report(IInteractionContext context, IResult result)
	{
		if (result.IsSuccess) return;

		string message = result.Error switch
		{
			InteractionCommandError.UnmetPrecondition => "You are not allowed to run this command",
			InteractionCommandError.UnknownCommand => "I do not know that command",
			InteractionCommandError.BadArgs => "I did not understand those arguments",
			_ => "I could not run that command"
		};

		try
		{
			if (context.Interaction.HasResponded) await context.Interaction.FollowupAsync(message, ephemeral: true);
			else await context.Interaction.RespondAsync(message, ephemeral: true);
		}
		catch (Exception)
		{
			// The interaction token can already be gone
		}

		await DiscordContext.Post($"Something went wrong:\n```{result.Error}: {result.ErrorReason}```", CortanaChannel.Log);
	}
}
