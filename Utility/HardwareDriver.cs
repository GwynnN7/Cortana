using Iot.Device.CpuTemperature;
using System.Device.Gpio;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;

namespace Utility
{
    public static class HardwareDriver
    {
        private const int RelayPin_0 = 25; //General
        private const int RelayPin_1 = 8;  //Computer-OUTLET

        private static int ComputerPlugsPin = RelayPin_1;
        private static int LampPin = RelayPin_0;
        private static int GeneralPin = RelayPin_1;

        private static Dictionary<EHardwareElements, EBooleanState> HardwareStates = new();

        public static NetworkStats NetStats;

        public static void Init()
        {
            LoadNetworkData();

            foreach(EHardwareElements element in Enum.GetValues(typeof(EHardwareElements))) {
                HardwareStates.Add(element, EBooleanState.Off);
            }

            SwitchRoom(EHardwareTrigger.Off);
            HandleNight();
        }

        public static void LoadNetworkData()
        {
            NetStats = Functions.LoadFile<NetworkStats>("Data/Global/NetworkDataPisa.json") ?? new();
        }

        public static void HandleNight()
        {
            new UtilityTimer(Name: "night-handler", TargetTime: new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 1, 0, 0), Callback: HandleNightCallback, TimerLocation: ETimerLocation.Utility, Loop: ETimerLoop.Daily);
        }

        private static void HandleNightCallback(object? sender, EventArgs e)
        {
            if (HardwareStates[EHardwareElements.Computer] == EBooleanState.Off) SwitchRoom(EHardwareTrigger.Off);
            else NotifyPC("You should go to sleep");

            if (DateTime.Now.Hour < 6) new UtilityTimer(Name: "safety-night-handler", Hours: 1, Minutes: 0, Seconds: 0, Callback: HandleNightCallback, TimerLocation: ETimerLocation.Utility, Loop: ETimerLoop.No);
        }

        private static void RebootRaspberry()
        {
            PythonCaller("Power", "Reboot");
        }

        private static void ShutdownRaspberry()
        {
            PythonCaller("Power", "Shutdown");
        }

        public static string SwitchRoom(EHardwareTrigger state)
        {
            
            if (state == EHardwareTrigger.On) 
            {
                if(DateTime.Now.Hour >= 17) SwitchLamp(state);
                SwitchComputer(state);
            }
            else
            {
                SwitchLamp(state);
                SwitchOutlets(state);
            }
            SwitchDisplay(state);

            return "Procedo";
        }

        public static string SwitchLamp(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                UseGPIO(LampPin, PinValue.High);
                HardwareStates[EHardwareElements.Lamp] = EBooleanState.On;
                return "Lampada accesa";
            }
            else if (state == EHardwareTrigger.Off)
            {
                UseGPIO(LampPin, PinValue.Low);
                HardwareStates[EHardwareElements.Lamp] = EBooleanState.Off;
                return "Lampada spenta";
            }
            else return SwitchLamp(HardwareStates[EHardwareElements.Lamp] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchGeneral(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                UseGPIO(GeneralPin, PinValue.High);
                HardwareStates[EHardwareElements.General] = EBooleanState.On;
                return "Presa attivata";
            }
            else if (state == EHardwareTrigger.Off)
            {
                UseGPIO(GeneralPin, PinValue.Low);
                HardwareStates[EHardwareElements.General] = EBooleanState.Off;
                return "Presa disattivata";
            }
            else return SwitchGeneral(HardwareStates[EHardwareElements.General] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchComputer(EHardwareTrigger state)
        {
            var lastState = HardwareStates[EHardwareElements.Computer];
            if (state == EHardwareTrigger.On)
            {
                SwitchOutlets(EHardwareTrigger.On);
                PythonCaller("WoL", NetStats.Desktop_LAN_MAC);
                return lastState == EBooleanState.On ? "Computer già acceso" : "Computer in accensione";
            }
            else if (state == EHardwareTrigger.Off)
            {
                HardwareStates[EHardwareElements.Computer] = EBooleanState.Off;
                var res = SSH_PC("sudo poweroff");
                return lastState == EBooleanState.Off ? "Computer già spento" : (res == "Computer non raggiungibile" ? res : "Computer in spegnimento");
            }
            else return SwitchComputer(HardwareStates[EHardwareElements.Computer] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchOutlets(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                UseGPIO(ComputerPlugsPin, PinValue.High);
                HardwareStates[EHardwareElements.Outlets] = EBooleanState.On;
                HardwareStates[EHardwareElements.Computer] = EBooleanState.On;
                return "Ciabatta accesa";
            }
            else if (state == EHardwareTrigger.Off)
            {
                if (HardwareStates[EHardwareElements.Computer] == EBooleanState.On)
                {
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
                    return "Computer e ciabatta in spegnimento";
                }
                UseGPIO(ComputerPlugsPin, PinValue.Low);
                HardwareStates[EHardwareElements.Outlets] = EBooleanState.Off;

                return "Ciabatta spenta";
            }
            else return SwitchOutlets(HardwareStates[EHardwareElements.Outlets] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchDisplay(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                try
                {
                    PythonCaller("DisplayON");
                    HardwareStates[EHardwareElements.Display] = EBooleanState.On;
                    return "Display acceso";
                }
                catch
                {
                    return "Display non raggiungibile";
                }

            }
            else if (state == EHardwareTrigger.Off)
            {
                try
                {
                    PythonCaller("DisplayOFF");
                    HardwareStates[EHardwareElements.Display] = EBooleanState.Off;
                    return "Display spento";
                }
                catch
                {
                    return "Display non raggiungibile";
                }
            }
            else return SwitchDisplay(HardwareStates[EHardwareElements.Display] == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchFromEnum(EHardwareElements element, EHardwareTrigger trigger)
        {
            string result = element switch
            {
                EHardwareElements.Lamp => SwitchLamp(trigger),
                EHardwareElements.Computer => SwitchComputer(trigger),
                EHardwareElements.Display => SwitchDisplay(trigger),
                EHardwareElements.Outlets => SwitchOutlets(trigger),
                EHardwareElements.General => SwitchGeneral(trigger),
                _ => "Dispositivo hardware non presente"
            };
            return result;
        }

        public static string SwitchFromString(string element, string trigger)
        {
            element = element.ToLower();
            trigger = trigger.ToLower();
            var element_result = HardwareElementFromString(element);
            var trigger_result = TriggerStateFromString(trigger);
            if(trigger_result == null) return "Azione non valida";
            if(element_result == null) {
                if(element == "room") return SwitchRoom(trigger_result.Value);
                else return "Dispositivo Hardware non presente";
            }
            return SwitchFromEnum(element_result.Value, trigger_result.Value);
        }

        public static async Task<string> GetPublicIP()
        {
            using var client = new HttpClient();
            var ip = await client.GetStringAsync("https://api.ipify.org");
            return ip;
        }

        public static string SSH_PC(string command)
        {
            
            var usr = NetStats.DesktopUsername;
            var addr = NetStats.Desktop_WLAN_IP;
            try
            {
                
                var proc = PythonCaller("SSH", $"{usr} {addr} {command}", true);
                string output = proc!.StandardOutput.ReadToEnd();
                var outputValues = output.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                string code = outputValues.Last();
                if(code == "65280") return "CONN_ERROR";
                else if(code == "0") return outputValues.Length == 1 ? code : string.Join("\n", outputValues.SkipLast(1));
                return "ERROR";
            }
            catch
            {
                return "CONN_ERROR";
            }
        }

        public static string RebootPC()
        {
            return SSH_PC("sudo reboot");
        }

        public static string NotifyPC(string text)
        {
            string res = HardwareDriver.SSH_PC($"notify {text}");
            if(res == "CONN_ERROR") return "Computer non raggiungibile";
            else if(res == "ERROR") return "Non è stato possibile inviare la notifica";
            else return res;
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

        public static bool PingPC()
        {
            return Ping(NetStats.Desktop_WLAN_IP);
        }

        public static bool Ping(string ip)
        {
            using Ping pingSender = new Ping();
            PingReply reply = pingSender.Send(ip, 2000);

            return reply.Status == IPStatus.Success;
        }

        private static void UseGPIO(int Pin, PinValue Value)
        {
            using var controller = new GpioController();
            controller.OpenPin(Pin, PinMode.Output);
            controller.Write(Pin, Value);
        }

        public static EHardwareTrigger? TriggerStateFromString(string state)
        {
            state = string.Concat(state[0].ToString().ToUpper(), state.AsSpan(1));
            bool res = Enum.TryParse(state, out EHardwareTrigger status);
            return res ? status : null;
        }

        public static EHardwareElements? HardwareElementFromString(string element)
        {
            element = string.Concat(element[0].ToString().ToUpper(), element.AsSpan(1));
            bool res = Enum.TryParse(element, out EHardwareElements status);
            return res ? status : null;
        }

        private static Process? PythonCaller(string fileName, string args = "", bool stdRedirect=false)
        {
            var proc = Process.Start(new ProcessStartInfo() {
                     FileName = "python", 
                     Arguments = $"Python/{fileName}.py {args}",
                     RedirectStandardOutput = stdRedirect
            });
            return proc;
        }
    }

    public class NetworkStats
    {
        public string Cortana_IP { get; set; }
        public string Cortana_LAN_MAC { get; set; }
        public string Cortana_WLAN_MAC { get; set; }

        public string Desktop_LAN_IP { get; set; }
        public string Desktop_WLAN_IP { get; set; }
        public string Desktop_LAN_MAC { get; set; }
        public string Desktop_WLAN_MAC { get; set; }

        public string SubnetMask { get; set; }
        public string Gateway { get; set; }
        public string CortanaUsername { get; set; }
        public string DesktopUsername { get; set; }
    }
}