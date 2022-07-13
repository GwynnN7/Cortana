using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Reflection;

namespace DiscordBot
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient client;
        private readonly InteractionService commands;
        private readonly IServiceProvider services;

        public CommandHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
        {
            this.client = client;
            this.commands = commands;
            this.services = services;
        }

        public async Task InitializeAsync()
        {
            Assembly DiscordBotAssembly = Assembly.Load(new AssemblyName("DiscordBot"));
            await commands.AddModulesAsync(DiscordBotAssembly, services);
            client.InteractionCreated += HandleInteraction;

            commands.SlashCommandExecuted += SlashCommandExecuted;
            commands.ContextCommandExecuted += ContextCommandExecuted;
            commands.ComponentCommandExecuted += ComponentCommandExecuted;
        }

        private Task ComponentCommandExecuted(ComponentCommandInfo arg1, IInteractionContext arg2, IResult arg3)
        {
            if (!arg3.IsSuccess)
            {
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
                    case InteractionCommandError.Exception:
                        arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
                        break;
                    case InteractionCommandError.Unsuccessful:
                        arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
                        break;
                    default:
                        break;
                }
                DiscordData.SendToChannel($"C'è stato un problema: {arg3.Error} {arg3.ErrorReason}", ECortanaChannels.Log);
            }

            return Task.CompletedTask;
        }

        private Task ContextCommandExecuted(ContextCommandInfo arg1, IInteractionContext arg2, IResult arg3)
        {
            if (!arg3.IsSuccess)
            {
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
                    case InteractionCommandError.Exception:
                        arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
                        break;
                    case InteractionCommandError.Unsuccessful:
                        arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
                        break;
                    default:
                        break;
                }
                DiscordData.SendToChannel($"C'è stato un problema: {arg3.Error} {arg3.ErrorReason}", ECortanaChannels.Log);
            }

            return Task.CompletedTask;
        }

        private Task SlashCommandExecuted(SlashCommandInfo arg1, IInteractionContext arg2, IResult arg3)
        {
            if (!arg3.IsSuccess)
            {
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
                    case InteractionCommandError.Exception:
                        arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
                        break;
                    case InteractionCommandError.Unsuccessful:
                        arg2.Interaction.RespondAsync("Non sono riuscita ad eseguire questo comando", ephemeral: true);
                        break;
                    default:
                        break;
                }
                DiscordData.SendToChannel($"C'è stato un problema: {arg3.Error} {arg3.ErrorReason}", ECortanaChannels.Log);
            }

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
                Console.WriteLine(ex.StackTrace);
                if (arg.Type == InteractionType.ApplicationCommand) await arg.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        }
    }
}

