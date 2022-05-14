using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules
{
    [Group("domotica", "Gestione domotica")]
    public class AutomationModule : InteractionModuleBase<SocketInteractionContext>
    {

        [SlashCommand("light", "Accendi o spegni la luce")]
        [RequireOwner]
        public async Task LightToggle()
        {
            string result = Utility.HardwareDriver.SwitchLamp(EHardwareTrigger.Toggle);
            Embed embed = DiscordData.CreateEmbed(Title: result);
            await RespondAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("hardware", "Interagisci con l'hardware in camera", runMode: RunMode.Async)]
        [RequireOwner]
        public async Task HardwareInteract([Summary("dispositivo", "Con cosa vuoi interagire?")] EHardwareElements element, [Summary("azione", "Cosa vuoi fare?")] EHardwareTrigger trigger)
        {
            string result = element switch
            {
                EHardwareElements.Lamp => Utility.HardwareDriver.SwitchLamp(trigger),
                EHardwareElements.PC => Utility.HardwareDriver.SwitchPC(trigger),
                EHardwareElements.OLED => Utility.HardwareDriver.SwitchOLED(trigger),
                EHardwareElements.LED => Utility.HardwareDriver.SwitchLED(trigger),
                EHardwareElements.Outlets => Utility.HardwareDriver.SwitchOutlets(trigger),
                _ => "Dispositivo hardware non presente"
            };

            Embed embed = DiscordData.CreateEmbed(Title: result);
            await RespondAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("room", "Accendi o spegni l'hardware fondamentale", runMode: RunMode.Async)]
        [RequireOwner]
        public async Task BootUp([Summary("azione", "Cosa vuoi fare?")] EHardwareTrigger trigger)
        {
            Embed embed = DiscordData.CreateEmbed(Title: "Procedo");
            await RespondAsync(embed: embed, ephemeral: true);

            if (trigger == EHardwareTrigger.On) Utility.HardwareDriver.SwitchPC(trigger);
            else Utility.HardwareDriver.SwitchOutlets(trigger);
            Utility.HardwareDriver.SwitchOLED(trigger);
            Utility.HardwareDriver.SwitchLED(trigger);
        }

        [SlashCommand("ping", "Pinga un IP", runMode: RunMode.Async)]
        public async Task Ping([Summary("ip", "IP da pingare")] string ip)
        {
            bool result;
            if (ip == "pc") result = Utility.HardwareDriver.PingPC();
            else result = Utility.HardwareDriver.Ping(ip);

            if (result) await RespondAsync($"L'IP {ip} ha risposto al ping");
            else await RespondAsync($"L'IP {ip} non ha risposto al ping");
        }

        [SlashCommand("ip", "Ti dico il mio IP pubblico", runMode: RunMode.Async)]
        [RequireOwner]
        public async Task GetIP()
        {
            await DeferAsync(ephemeral: true);
            var ip = await Utility.Functions.GetPublicIP();
            await FollowupAsync($"L'IP pubblico è {ip}", ephemeral: true);
        }

        [SlashCommand("shutdown", "Vado offline su discord")]
        [RequireOwner]
        public async Task Shutdown()
        {
            Embed embed = DiscordData.CreateEmbed(Title: "Shutting down Chief");
            await RespondAsync(embed: embed, ephemeral: true);

            await DiscordBot.Disconnect();
        }
    }
}
