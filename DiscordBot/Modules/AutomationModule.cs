using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules
{
    [Group("domotica", "Gestione domotica")]
    [RequireOwner]
    public class AutomationModule : InteractionModuleBase<SocketInteractionContext>
    {

        [SlashCommand("light", "Accendi o spegni la luce")]
        public async Task LightToggle()
        {
            string result = Utility.HardwareDriver.SwitchLamp(EHardwareTrigger.Toggle);
            Embed embed = DiscordData.CreateEmbed(Title: result);
            await RespondAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("fan", "Imposta la velocità del ventilatore")]
        public async Task FanSpeed([Summary("speed", "Velocità ventola")] EFanSpeeds speed)
        {
            await DeferAsync(ephemeral: true);

            string result = Utility.HardwareDriver.SetFanSpeed(speed);
            Embed embed = DiscordData.CreateEmbed(Title: result);

            await FollowupAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("hardware", "Interagisci con l'hardware in camera", runMode: RunMode.Async)]
        public async Task HardwareInteract([Summary("dispositivo", "Con cosa vuoi interagire?")] EHardwareElements element, [Summary("azione", "Cosa vuoi fare?")] EHardwareTrigger trigger)
        {
            await DeferAsync(ephemeral: true);

            string result = element switch
            {
                EHardwareElements.Lamp => Utility.HardwareDriver.SwitchLamp(trigger),
                EHardwareElements.PC => Utility.HardwareDriver.SwitchPC(trigger),
                EHardwareElements.OLED => Utility.HardwareDriver.SwitchOLED(trigger),
                EHardwareElements.LED => Utility.HardwareDriver.SwitchLED(trigger),
                EHardwareElements.Outlets => Utility.HardwareDriver.SwitchOutlets(trigger),
                EHardwareElements.Fan => Utility.HardwareDriver.SwitchFan(trigger),
                _ => "Dispositivo hardware non presente"
            };

            Embed embed = DiscordData.CreateEmbed(Title: result);
            await FollowupAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("room", "Accendi o spegni l'hardware fondamentale", runMode: RunMode.Async)]
        public async Task BootUp([Summary("azione", "Cosa vuoi fare?")] EHardwareTrigger trigger)
        {
            Embed embed = DiscordData.CreateEmbed(Title: "Procedo");
            await RespondAsync(embed: embed, ephemeral: true);

            Utility.HardwareDriver.SwitchRoom(trigger);
        }

        [SlashCommand("ip", "Ti dico il mio IP pubblico", runMode: RunMode.Async)]
        public async Task GetIP()
        {
            await DeferAsync(ephemeral: true);
            var ip = await Utility.Functions.GetPublicIP();
            await FollowupAsync($"L'IP pubblico è {ip}", ephemeral: true);
        }

        [SlashCommand("shutdown", "Vado offline su discord")]
        public async Task Shutdown()
        {
            Embed embed = DiscordData.CreateEmbed(Title: "Shutting down Chief");
            await RespondAsync(embed: embed, ephemeral: true);

            await DiscordBot.Disconnect();
        }
    }
}
