using Iot.Device.CpuTemperature;
using Renci.SshNet;
using System.Device.Gpio;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;

namespace Utility
{
    public static class HardwareDriver
    {
        private static Dictionary<EHardwareElements, EBooleanState> HardwareStates = new();
        private static NetworkStats NetStats;
        
        private const int RelayPin_0 = 25; //Lamp Orvieto
        private const int RelayPin_1 = 23; //General/Lamp Pisa
        private const int RelayPin_2 = 24;  //Computer-OUTLET

        private static int ComputerPlugsPin => RelayPin_2;
        private static int LampPin => NetStats.Location == ELocation.Orvieto ? RelayPin_0 : RelayPin_1;
        private static int GeneralPin => RelayPin_1;

        public static void Init()
        {
            LoadNetworkData();

            foreach (EHardwareElements element in Enum.GetValues(typeof(EHardwareElements)))
            {
                HardwareStates.Add(element, EBooleanState.Off);
            }

            //SwitchRoom(EHardwareTrigger.Off);
            HandleNight();
        }
        
        private static void LoadNetworkData()
        {
            NetworkStats orvietoNet = Functions.LoadFile<NetworkStats>("Data/Global/NetworkDataOrvieto.json") ?? new();
            NetworkStats pisaNet = Functions.LoadFile<NetworkStats>("Data/Global/NetworkDataPisa.json") ?? new();
            NetStats = GetDefaultGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
        }

        private static void HandleNight()
        {
            new UtilityTimer(Name: "night-handler", TargetTime: new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 1, 0, 0), Callback: HandleNightCallback, TimerLocation: ETimerLocation.Utility, Loop: ETimerLoop.Daily);
        }

        private static void HandleNightCallback(object? sender, EventArgs e)
        {
            if (HardwareStates[EHardwareElements.Computer] == EBooleanState.Off) SwitchRoom(EHardwareTrigger.Off);
            else NotifyPC("You should go to sleep");

            if (DateTime.Now.Hour < 6) new UtilityTimer(Name: "safety-night-handler", Hours: 1, Minutes: 0, Seconds: 0, Callback: HandleNightCallback, TimerLocation: ETimerLocation.Utility, Loop: ETimerLoop.No);
        }

        public static string PowerRaspberry(EPowerOption option)
        {
            switch (option)
            {
                case EPowerOption.Shutdown:
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        PythonCaller("Power", "shutdown");
                    });
                    return "Raspberry powering off";
                case EPowerOption.Reboot:
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        PythonCaller("Power", "reboot");
                    });
                    return "Raspberry rebooting";
                default:
                    return "Command not found";
            }
        }

        public static string SwitchRoom(EHardwareTrigger state)
        {

            if (state == EHardwareTrigger.On)
            {
                if (DateTime.Now.Hour >= 20) SwitchLamp(state);
                SwitchComputer(state);
            }
            else
            {
                SwitchLamp(state);
                SwitchOutlets(state);
            }
            SwitchDisplay(state);

            return "Working on it";
        }

        public static string SwitchLamp(EHardwareTrigger state)
        {
            switch (state)
            {
                case EHardwareTrigger.On when HardwareStates[EHardwareElements.Lamp] == EBooleanState.Off:
                    if (NetStats.Location == ELocation.Orvieto)
                    {
                        Task.Run(async () =>
                        {
                            UseGPIO(LampPin, PinValue.High);
                            await Task.Delay(200);
                            UseGPIO(LampPin, PinValue.Low);
                        });
                    }
                    else UseGPIO(LampPin, PinValue.High);
                    HardwareStates[EHardwareElements.Lamp] = EBooleanState.On;
                    return "Lamp on";
                case EHardwareTrigger.Off when HardwareStates[EHardwareElements.Lamp] == EBooleanState.On:
                    if (NetStats.Location == ELocation.Orvieto)
                    {
                        Task.Run(async () =>
                        {
                            UseGPIO(LampPin, PinValue.High);
                            await Task.Delay(200);
                            UseGPIO(LampPin, PinValue.Low);
                        });
                    }
                    else UseGPIO(LampPin, PinValue.Low);
                    HardwareStates[EHardwareElements.Lamp] = EBooleanState.Off;
                    return "Lamp off";
                case EHardwareTrigger.Toggle:
                    return SwitchLamp(HardwareStates[EHardwareElements.Lamp] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
                default:
                    return "Nothing to do";
            }
        }

        public static string SwitchGeneral(EHardwareTrigger state)
        {
            switch (state)
            {
                case EHardwareTrigger.On:
                    UseGPIO(GeneralPin, PinValue.High);
                    HardwareStates[EHardwareElements.General] = EBooleanState.On;
                    return "Power on";
                case EHardwareTrigger.Off:
                    UseGPIO(GeneralPin, PinValue.Low);
                    HardwareStates[EHardwareElements.General] = EBooleanState.Off;
                    return "Power off";
                default:
                    return SwitchGeneral(HardwareStates[EHardwareElements.General] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
            }
        }

        public static string SwitchComputer(EHardwareTrigger state)
        {
            var lastState = HardwareStates[EHardwareElements.Computer];
            switch (state)
            {
                case EHardwareTrigger.On:
                    SwitchOutlets(EHardwareTrigger.On);
                    PythonCaller("WoL", NetStats.Desktop_LAN_MAC);
                    return lastState == EBooleanState.On ? "Computer already on" : "Computer booting up";
                case EHardwareTrigger.Off:
                {
                    HardwareStates[EHardwareElements.Computer] = EBooleanState.Off;
                    var res = SSH_PC("poweroff");
                    return lastState == EBooleanState.Off ? "Computer already off" : res;
                }
                default:
                    return SwitchComputer(HardwareStates[EHardwareElements.Computer] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
            }
        }

        private static string SwitchOutlets(EHardwareTrigger state)
        {
            switch (state)
            {
                case EHardwareTrigger.Off when HardwareStates[EHardwareElements.Computer] == EBooleanState.On:
                    Task.Run(async () =>
                    {
                        SwitchComputer(EHardwareTrigger.Off);
                        await Task.Delay(1000);

                        var start = DateTime.Now;
                        while (PingPC() && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

                        if ((DateTime.Now - start).Seconds < 3) await Task.Delay(25000);
                        else await Task.Delay(5000);

                        SwitchOutlets(EHardwareTrigger.Off);
                    });
                    return "Computer and outlets shutting down";
                case EHardwareTrigger.On:
                    UseGPIO(ComputerPlugsPin, PinValue.High);
                    HardwareStates[EHardwareElements.Outlets] = EBooleanState.On;
                    HardwareStates[EHardwareElements.Computer] = EBooleanState.On;
                    return "Outlets on";
                case EHardwareTrigger.Off:
                    UseGPIO(ComputerPlugsPin, PinValue.Low);
                    HardwareStates[EHardwareElements.Outlets] = EBooleanState.Off;
                    return "Outlets off";
                default:
                    return SwitchOutlets(HardwareStates[EHardwareElements.Outlets] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
            }
        }

        private static string SwitchDisplay(EHardwareTrigger state)
        {
            switch (state)
            {
                case EHardwareTrigger.On:
                    try
                    {
                        PythonCaller("DisplayON");
                        HardwareStates[EHardwareElements.Display] = EBooleanState.On;
                        return "Display on";
                    }
                    catch
                    {
                        return "Display not reachable";
                    }
                case EHardwareTrigger.Off:
                    try
                    {
                        PythonCaller("DisplayOFF");
                        HardwareStates[EHardwareElements.Display] = EBooleanState.Off;
                        return "Display off";
                    }
                    catch
                    {
                        return "Display not reachable";
                    }
                default:
                    return SwitchDisplay(HardwareStates[EHardwareElements.Display] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
            }
        }

        public static string SwitchFromEnum(EHardwareElements element, EHardwareTrigger trigger)
        {
            var result = element switch
            {
                EHardwareElements.Lamp => SwitchLamp(trigger),
                EHardwareElements.Computer => SwitchComputer(trigger),
                EHardwareElements.Display => SwitchDisplay(trigger),
                EHardwareElements.Outlets => SwitchOutlets(trigger),
                EHardwareElements.General => SwitchGeneral(trigger),
                _ => "Hardware device not listed"
            };
            return result;
        }

        public static string SwitchFromString(string element, string trigger)
        {
            element = element.ToLower();
            trigger = trigger.ToLower();
            var elementResult = HardwareElementFromString(element);
            var triggerResult = TriggerStateFromString(trigger);
            if (triggerResult == null) return "Invalid action";
            if (elementResult != null) return SwitchFromEnum(elementResult.Value, triggerResult.Value);
            return element == "room" ? SwitchRoom(triggerResult.Value) : "Hardware device not listed";
        }

        public static async Task<string> GetPublicIP()
        {
            using var client = new HttpClient();
            var ip = await client.GetStringAsync("https://api.ipify.org");
            return ip;
        }


        public static string SSH_PC(string command, bool asRoot = true, bool returnResult = false)
        {
            var usr = asRoot ? NetStats.DesktopRoot : NetStats.DesktopUsername;
            var pass = NetStats.DesktopPassword;
            var addr = NetStats.Desktop_IP;

            string result;
            try
            {
                using var client = new SshClient(addr, usr, pass);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(3);
                client.Connect();
                var r = client.RunCommand(command);
                if (r.ExitStatus == 0) result = (returnResult && r.Result.Length != 0) ? r.Result : "Command executed";
                else result = (returnResult && r.Error.Length != 0) ? r.Error : "Command not executed";
                string log = $"Exit Status: {r.ExitStatus}\nResult: {r.Result}\nError: {r.Error}";
                Functions.Log("SSH", log);
                client.Disconnect();
            }
            catch
            {
                result = "Computer not reachable";
            }
            return result;
        }


        public static string RebootPC()
        {
            return SSH_PC("reboot");
        }

        public static string NotifyPC(string text)
        {
            return SSH_PC($"/home/{NetStats.DesktopUsername}/.config/cortana/cortana-notify.sh \\\'{text}\\\'", asRoot: false);
        }

        public static string GetCPUTemperature()
        {
            using CpuTemperature cpuTemperature = new CpuTemperature();
            var temperatures = cpuTemperature.ReadTemperatures();
            double average = 0;
            foreach (var temp in temperatures) average += temp.Temperature.DegreesCelsius;
            average /= temperatures.Count;
            string tempFormat = $"{Math.Round(average, 1).ToString(CultureInfo.InvariantCulture)}°C";
            return tempFormat;
        }
        
        public static string GetDefaultGateway() 
        { 
            var defaultGateway = 
                from nics in NetworkInterface.GetAllNetworkInterfaces() 
                from props in nics.GetIPProperties().GatewayAddresses 
                where nics.OperationalStatus == OperationalStatus.Up 
                select props.Address.ToString();
            return defaultGateway.First();
        }

        public static string GetLocation()
        {
            return NetStats.Location switch
            {
                ELocation.Orvieto => "Orvieto",
                ELocation.Pisa => "Pisa",
                _ => "Unknown"
            };
        }

        public static bool PingPC()
        {
            return Ping(NetStats.Desktop_IP);
        }

        public static bool Ping(string ip)
        {
            using Ping pingSender = new Ping();
            PingReply reply = pingSender.Send(ip, 2000);

            return reply.Status == IPStatus.Success;
        }

        private static void UseGPIO(int pin, PinValue value)
        {
            using var controller = new GpioController();
            controller.OpenPin(pin, PinMode.Output);
            controller.Write(pin, value);
        }

        private static EHardwareTrigger? TriggerStateFromString(string state)
        {
            state = string.Concat(state[0].ToString().ToUpper(), state.AsSpan(1));
            var res = Enum.TryParse(state, out EHardwareTrigger status);
            return res ? status : null;
        }

        private static EHardwareElements? HardwareElementFromString(string element)
        {
            element = string.Concat(element[0].ToString().ToUpper(), element.AsSpan(1));
            var res = Enum.TryParse(element, out EHardwareElements status);
            return res ? status : null;
        }

        private static Process? PythonCaller(string fileName, string args = "", bool stdRedirect = false)
        {
            var proc = Process.Start(new ProcessStartInfo()
            {
                FileName = "python",
                Arguments = $"Python/{fileName}.py {args}",
                RedirectStandardOutput = stdRedirect
            });
            return proc;
        }
    }

    public class NetworkStats
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ELocation Location { get; set; }
        public string Cortana_IP { get; set; }
        public string Cortana_LAN_MAC { get; set; }
        public string Cortana_WLAN_MAC { get; set; }
        public string Desktop_IP { get; set; }
        public string Desktop_LAN_MAC { get; set; }
        public string Desktop_WLAN_MAC { get; set; }

        public string SubnetMask { get; set; }
        public string Gateway { get; set; }
        public string CortanaUsername { get; set; }
        public string DesktopUsername { get; set; }
        public string DesktopRoot { get; set; }
        public string DesktopPassword { get; set; }
    }
}