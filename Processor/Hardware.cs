using Iot.Device.CpuTemperature;
using Renci.SshNet;
using System.Device.Gpio;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.Json.Serialization;

namespace Processor
{
    public static class Hardware
    {
        private static readonly Dictionary<EGpio, EStatus> HardwareStates;
        private static readonly NetworkStats NetStats;
        
        private const int RelayPin0 = 25; //Lamp Orvieto
        private const int RelayPin1 = 23; //General/Lamp Pisa
        private const int RelayPin2 = 24;  //Computer-OUTLET

        private static int ComputerPlugsPin => RelayPin2;
        private static int LampPin => NetStats.Location == ELocation.Orvieto ? RelayPin0 : RelayPin1;
        private static int GeneralPin => RelayPin1;

        static Hardware()
        {
            NetStats = GetLocationNetworkData();
            HardwareStates = new Dictionary<EGpio, EStatus>();
            foreach (EGpio element in Enum.GetValues(typeof(EGpio)))
            {
                HardwareStates.Add(element, EStatus.Off);
            }

            //SwitchRoom(EHardwareTrigger.Off);
            _ = new UtilityTimer(name: "night-handler", targetTime: new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 1, 0, 0), callback: HandleNightCallback, timerLocation: ETimerLocation.Utility, loop: ETimerLoop.Daily);
        }
        
        private static NetworkStats GetLocationNetworkData()
        {
            NetworkStats orvietoNet = Software.LoadFile<NetworkStats>("Storage/Config/Network/NetworkDataOrvieto.json") ?? new NetworkStats();
            NetworkStats pisaNet = Software.LoadFile<NetworkStats>("Storage/Config/Network/NetworkDataPisa.json") ?? new NetworkStats();
            return GetDefaultGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
        }

        private static void HandleNightCallback(object? sender, EventArgs e)
        {
            if (HardwareStates[EGpio.Computer] == EStatus.Off) SwitchLamp(ETrigger.Off);
            else CommandPc(EComputerCommand.Notify, "You should go to sleep");

            if (DateTime.Now.Hour < 6) 
                _ = new UtilityTimer(name: "safety-night-handler", hours: 1, minutes: 0, seconds: 0, callback: HandleNightCallback, timerLocation: ETimerLocation.Utility, loop: ETimerLoop.No);
        }

        public static string PowerRaspberry(EPowerOption option)
        {
            switch (option)
            {
                case EPowerOption.Shutdown:
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        ScriptRunner("power", "shutdown");
                    });
                    return "Raspberry powering off";
                case EPowerOption.Reboot:
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        ScriptRunner("power", "reboot");
                    });
                    return "Raspberry rebooting";
                default:
                    return "Command not found";
            }
        }

        public static string SwitchRoom(ETrigger state)
        {

            if (state == ETrigger.On)
            {
                if (DateTime.Now.Hour >= 20) SwitchLamp(state);
                SwitchComputer(state);
            }
            else
            {
                SwitchLamp(state);
                SwitchOutlets(state);
            }

            return "Working on it";
        }

        public static string SwitchLamp(ETrigger state)
        {
            switch (state)
            {
                case ETrigger.On when HardwareStates[EGpio.Lamp] == EStatus.Off:
                    if (NetStats.Location == ELocation.Orvieto)
                    {
                        Task.Run(async () =>
                        {
                            UseGpio(LampPin, PinValue.High);
                            await Task.Delay(200);
                            UseGpio(LampPin, PinValue.Low);
                        });
                    }
                    else UseGpio(LampPin, PinValue.High);
                    HardwareStates[EGpio.Lamp] = EStatus.On;
                    return "Lamp on";
                case ETrigger.Off when HardwareStates[EGpio.Lamp] == EStatus.On:
                    if (NetStats.Location == ELocation.Orvieto)
                    {
                        Task.Run(async () =>
                        {
                            UseGpio(LampPin, PinValue.High);
                            await Task.Delay(200);
                            UseGpio(LampPin, PinValue.Low);
                        });
                    }
                    else UseGpio(LampPin, PinValue.Low);
                    HardwareStates[EGpio.Lamp] = EStatus.Off;
                    return "Lamp off";
                case ETrigger.Toggle:
                    return SwitchLamp(HardwareStates[EGpio.Lamp] == EStatus.On ? ETrigger.Off : ETrigger.On);
                default:
                    return "Nothing to do";
            }
        }

        public static string SwitchGeneral(ETrigger state)
        {
            if (NetStats.Location == ELocation.Pisa) return SwitchLamp(state);
            
            switch (state)
            {
                case ETrigger.On:
                    UseGpio(GeneralPin, PinValue.High);
                    HardwareStates[EGpio.General] = EStatus.On;
                    return "Power on";
                case ETrigger.Off:
                    UseGpio(GeneralPin, PinValue.Low);
                    HardwareStates[EGpio.General] = EStatus.Off;
                    return "Power off";
                case ETrigger.Toggle:
                default:
                    return SwitchGeneral(HardwareStates[EGpio.General] == EStatus.On ? ETrigger.Off : ETrigger.On);
            }
        }

        public static string SwitchComputer(ETrigger state)
        {
            EStatus lastState = HardwareStates[EGpio.Computer];
            switch (state)
            {
                case ETrigger.On:
                    CommandPc(EComputerCommand.PowerOn);
                    HardwareStates[EGpio.Computer] = EStatus.On;
                    return lastState == EStatus.On ? "Computer already on" : "Computer booting up";
                case ETrigger.Off:
                    string result = CommandPc(EComputerCommand.Shutdown);
                    HardwareStates[EGpio.Computer] = EStatus.Off;
                    return lastState == EStatus.Off ? "Computer already off" : result;
                case ETrigger.Toggle:
                default:
                    return SwitchComputer(HardwareStates[EGpio.Computer] == EStatus.On ? ETrigger.Off : ETrigger.On);
            }
        }

        private static string SwitchOutlets(ETrigger state)
        {
            switch (state)
            {
                case ETrigger.Off when HardwareStates[EGpio.Computer] == EStatus.On:
                    Task.Run(async () =>
                    {
                        SwitchComputer(ETrigger.Off);
                        await Task.Delay(1000);

                        DateTime start = DateTime.Now;
                        while (PingPc() && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

                        if ((DateTime.Now - start).Seconds < 3) await Task.Delay(25000);
                        else await Task.Delay(5000);

                        SwitchOutlets(ETrigger.Off);
                    });
                    return "Computer and outlets shutting down";
                case ETrigger.On:
                    UseGpio(ComputerPlugsPin, PinValue.High);
                    HardwareStates[EGpio.Outlets] = EStatus.On;
                    HardwareStates[EGpio.Computer] = EStatus.On; //Computer automatically turns on
                    return "Outlets on";
                case ETrigger.Off:
                    UseGpio(ComputerPlugsPin, PinValue.Low);
                    HardwareStates[EGpio.Outlets] = EStatus.Off;
                    return "Outlets off";
                case ETrigger.Toggle:
                default:
                    return SwitchOutlets(HardwareStates[EGpio.Outlets] == EStatus.On ? ETrigger.Off : ETrigger.On);
            }
        }

        public static string SwitchFromEnum(EGpio element, ETrigger trigger)
        {
            string result = element switch
            {
                EGpio.Lamp => SwitchLamp(trigger),
                EGpio.Computer => SwitchComputer(trigger),
                EGpio.Outlets => SwitchOutlets(trigger),
                EGpio.General => SwitchGeneral(trigger),
                _ => "Hardware device not listed"
            };
            return result;
        }

        public static string SwitchFromString(string element, string trigger)
        {
            element = element.ToLower();
            trigger = trigger.ToLower();
            EGpio? elementResult = HardwareElementFromString(element);
            ETrigger? triggerResult = TriggerStateFromString(trigger);
            if (triggerResult == null) return "Invalid action";
            if (elementResult != null) return SwitchFromEnum(elementResult.Value, triggerResult.Value);
            return element == "room" ? SwitchRoom(triggerResult.Value) : "Hardware device not listed";
        }

        public static async Task<string> GetPublicIp()
        {
            using var client = new HttpClient();
            string ip = await client.GetStringAsync("https://api.ipify.org");
            return ip;
        }


        public static string CommandPc(EComputerCommand command, string? args = null)
        {
            string result;
            switch (command)
            {
                case EComputerCommand.PowerOn:
                {
                    result = HardwareStates[EGpio.Computer] == EStatus.On
                        ? "Computer already on"
                        : "Computer booting up";
                    SwitchOutlets(ETrigger.On);
                    ScriptRunner("wake-on-lan", NetStats.Desktop_LAN_MAC);
                    HardwareStates[EGpio.Computer] = EStatus.On;
                    break;
                }
                case EComputerCommand.Shutdown:
                {
                    SendPc("shutdown", asRoot: true, result: out result);
                    result = HardwareStates[EGpio.Computer] == EStatus.Off
                        ? "Computer already off"
                        : result;
                    HardwareStates[EGpio.Computer] = EStatus.Off;
                    break;
                }
                case EComputerCommand.Reboot:
                    SendPc("reboot", asRoot: true, result: out result);
                    break;
                case EComputerCommand.Notify:
                    SendPc($"notify {args}", asRoot: false, result: out result);
                    break;
                default:
                    result = "Command not found";
                    break;
            }

            return result;
        }
        
        public static bool SendPc(string command, bool asRoot, out string result)
        {
            var scriptPath = $"/home/{NetStats.DesktopUsername}/.config/cortana/cortana-script.sh";
            
            string usr = asRoot ? NetStats.DesktopRoot : NetStats.DesktopUsername;
            string pass = Software.GetConfigurationSecrets()["desktop-password"]!;
            string addr = NetStats.Desktop_IP;
            
            try
            {
                using var client = new SshClient(addr, usr, pass);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(3);
                client.Connect();

                string cmd = $"{scriptPath} {command}".Trim();
                SshCommand r = client.RunCommand(cmd);
                if (r.ExitStatus == 0) result = r.Result.Trim().Length > 0 && !r.Result.Trim().Equals("0") ? r.Result : "Command executed successfully.\n";
                else result = r.Error.Trim().Length > 0 ? r.Error : "There was an error executing the command.\n";
                result = result.Trim();
                
                var log = $"Exit Status: {r.ExitStatus}\nResult: {r.Result}Error: {r.Error}\n----\n";
                Software.Log("SSH", log);
                
                client.Disconnect();
                
                return r.ExitStatus == 0;
            }
            catch
            {
                result = "Sorry, I couldn't send the command";
                return false;
            }
        }

        public static string GetCpuTemperature()
        {
            using var cpuTemperature = new CpuTemperature();
            List<(string Sensor, UnitsNet.Temperature Temperature)> temperatures = cpuTemperature.ReadTemperatures();
            double average = temperatures.Sum(temp => temp.Temperature.DegreesCelsius);
            average /= temperatures.Count;
            var tempFormat = $"{Math.Round(average, 1).ToString(CultureInfo.InvariantCulture)}°C";
            return tempFormat;
        }
        
        public static string GetDefaultGateway() 
        { 
            IEnumerable<string> defaultGateway = 
                from netInterfaces in NetworkInterface.GetAllNetworkInterfaces() 
                from props in netInterfaces.GetIPProperties().GatewayAddresses 
                where netInterfaces.OperationalStatus == OperationalStatus.Up 
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

        public static bool PingPc() => Ping(NetStats.Desktop_IP);
        public static bool Ping(string ip)
        {
            using var pingSender = new Ping();
            PingReply reply = pingSender.Send(ip, 2000);

            return reply.Status == IPStatus.Success;
        }

        private static void UseGpio(int pin, PinValue value)
        {
            using var controller = new GpioController();
            controller.OpenPin(pin, PinMode.Output);
            controller.Write(pin, value);
        }

        private static ETrigger? TriggerStateFromString(string state)
        {
            state = string.Concat(state[0].ToString().ToUpper(), state.AsSpan(1));
            bool res = Enum.TryParse(state, out ETrigger status);
            return res ? status : null;
        }

        private static EGpio? HardwareElementFromString(string element)
        {
            element = string.Concat(element[0].ToString().ToUpper(), element.AsSpan(1));
            bool res = Enum.TryParse(element, out EGpio status);
            return res ? status : null;
        }

        private static void ScriptRunner(string fileName, string args = "", bool stdRedirect = false)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = "zsh",
                Arguments = $"-c Scripts/{fileName}.sh \"{args}\"",
                RedirectStandardOutput = stdRedirect,
                UseShellExecute = false,
                CreateNoWindow = true
            });
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
    }
}