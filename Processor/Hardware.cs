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
        private static readonly Dictionary<EHardwareElements, EBooleanState> HardwareStates;
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
            HardwareStates = new Dictionary<EHardwareElements, EBooleanState>();
            foreach (EHardwareElements element in Enum.GetValues(typeof(EHardwareElements)))
            {
                HardwareStates.Add(element, EBooleanState.Off);
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
            if (HardwareStates[EHardwareElements.Computer] == EBooleanState.Off) SwitchLamp(EHardwareTrigger.Off);
            else NotifyPc("You should go to sleep");

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
                            UseGpio(LampPin, PinValue.High);
                            await Task.Delay(200);
                            UseGpio(LampPin, PinValue.Low);
                        });
                    }
                    else UseGpio(LampPin, PinValue.High);
                    HardwareStates[EHardwareElements.Lamp] = EBooleanState.On;
                    return "Lamp on";
                case EHardwareTrigger.Off when HardwareStates[EHardwareElements.Lamp] == EBooleanState.On:
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
            if (NetStats.Location == ELocation.Pisa) return SwitchLamp(state);
            
            switch (state)
            {
                case EHardwareTrigger.On:
                    UseGpio(GeneralPin, PinValue.High);
                    HardwareStates[EHardwareElements.General] = EBooleanState.On;
                    return "Power on";
                case EHardwareTrigger.Off:
                    UseGpio(GeneralPin, PinValue.Low);
                    HardwareStates[EHardwareElements.General] = EBooleanState.Off;
                    return "Power off";
                case EHardwareTrigger.Toggle:
                default:
                    return SwitchGeneral(HardwareStates[EHardwareElements.General] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
            }
        }

        public static string SwitchComputer(EHardwareTrigger state)
        {
            EBooleanState lastState = HardwareStates[EHardwareElements.Computer];
            switch (state)
            {
                case EHardwareTrigger.On:
                    SwitchOutlets(EHardwareTrigger.On);
                    PythonCaller("WoL", NetStats.Desktop_LAN_MAC);
                    return lastState == EBooleanState.On ? "Computer already on" : "Computer booting up";
                case EHardwareTrigger.Off:
                    HardwareStates[EHardwareElements.Computer] = EBooleanState.Off;
                    string res = SSH_PC("poweroff");
                    return lastState == EBooleanState.Off ? "Computer already off" : res;
                case EHardwareTrigger.Toggle:
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

                        DateTime start = DateTime.Now;
                        while (PingPc() && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

                        if ((DateTime.Now - start).Seconds < 3) await Task.Delay(25000);
                        else await Task.Delay(5000);

                        SwitchOutlets(EHardwareTrigger.Off);
                    });
                    return "Computer and outlets shutting down";
                case EHardwareTrigger.On:
                    UseGpio(ComputerPlugsPin, PinValue.High);
                    HardwareStates[EHardwareElements.Outlets] = EBooleanState.On;
                    HardwareStates[EHardwareElements.Computer] = EBooleanState.On;
                    return "Outlets on";
                case EHardwareTrigger.Off:
                    UseGpio(ComputerPlugsPin, PinValue.Low);
                    HardwareStates[EHardwareElements.Outlets] = EBooleanState.Off;
                    return "Outlets off";
                case EHardwareTrigger.Toggle:
                default:
                    return SwitchOutlets(HardwareStates[EHardwareElements.Outlets] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
            }
        }

        public static string SwitchFromEnum(EHardwareElements element, EHardwareTrigger trigger)
        {
            string result = element switch
            {
                EHardwareElements.Lamp => SwitchLamp(trigger),
                EHardwareElements.Computer => SwitchComputer(trigger),
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
            EHardwareElements? elementResult = HardwareElementFromString(element);
            EHardwareTrigger? triggerResult = TriggerStateFromString(trigger);
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


        public static string SSH_PC(string command, bool asRoot = true, bool returnResult = false)
        {
            string usr = asRoot ? NetStats.DesktopRoot : NetStats.DesktopUsername;
            string pass = Software.GetConfigurationSecrets()["desktop-password"]!;
            string addr = NetStats.Desktop_IP;

            string result;
            try
            {
                using var client = new SshClient(addr, usr, pass);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(3);
                client.Connect();
                SshCommand r = client.RunCommand(command);
                if (r.ExitStatus == 0) result = (returnResult && r.Result.Length != 0) ? r.Result : "Command executed";
                else result = (returnResult && r.Error.Length != 0) ? r.Error : "Command not executed";
                var log = $"Exit Status: {r.ExitStatus}\nResult: {r.Result}\nError: {r.Error}";
                Software.Log("SSH", log);
                client.Disconnect();
            }
            catch
            {
                result = "Computer not reachable";
            }
            return result;
        }


        public static string RebootPc()
        {
            return SSH_PC("reboot");
        }

        public static string NotifyPc(string text)
        {
            return SSH_PC($@"/home/{NetStats.DesktopUsername}/.config/cortana/cortana-notify.sh \'{text}\'", asRoot: false);
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

        private static EHardwareTrigger? TriggerStateFromString(string state)
        {
            state = string.Concat(state[0].ToString().ToUpper(), state.AsSpan(1));
            bool res = Enum.TryParse(state, out EHardwareTrigger status);
            return res ? status : null;
        }

        private static EHardwareElements? HardwareElementFromString(string element)
        {
            element = string.Concat(element[0].ToString().ToUpper(), element.AsSpan(1));
            bool res = Enum.TryParse(element, out EHardwareElements status);
            return res ? status : null;
        }

        private static void PythonCaller(string fileName, string args = "", bool stdRedirect = false)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = "python",
                Arguments = $"Python/{fileName}.py {args}",
                RedirectStandardOutput = stdRedirect
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