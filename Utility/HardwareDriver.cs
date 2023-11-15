using Iot.Device.CpuTemperature;
using System.Device.Gpio;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using Renci.SshNet;

namespace Utility
{
    public static class HardwareDriver
    {
        private const int RelayPin_0 = 25; //LAMP
        private const int RelayPin_1 = 8;  //PC-OUTLET

        private static int PCPlugsPin = RelayPin_1;
        private static int LampPin = RelayPin_0;

        private static EBooleanState OutletsState = EBooleanState.Off;
        private static EBooleanState PCState = EBooleanState.Off;
        private static EBooleanState OLEDState = EBooleanState.Off;
        private static EBooleanState LampState = EBooleanState.Off;

        public static NetworkStats NetStats;

        public static void Init()
        {
            LoadNetworkData();

            //SwitchRoom(EHardwareTrigger.Off);
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
            if (PCState == EBooleanState.Off) SwitchRoom(EHardwareTrigger.Off);
            else Functions.NotifyPC("You should go to sleep");

            if (DateTime.Now.Hour < 6) new UtilityTimer(Name: "safety-night-handler", Hours: 1, Minutes: 0, Seconds: 0, Callback: HandleNightCallback, TimerLocation: ETimerLocation.Utility, Loop: ETimerLoop.No);
        }

        public static string SwitchRoom(EHardwareTrigger state)
        {
            
            if (state == EHardwareTrigger.On) 
            {
                if(DateTime.Now.Hour >= 17) SwitchLamp(state);
                SwitchPC(state);
            }
            else
            {
                SwitchLamp(state);
                SwitchOutlets(state);
            }
            SwitchOLED(state);

            return "Procedo";
        }

        public static string SwitchLamp(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                UseGPIO(LampPin, PinValue.High);
                LampState = EBooleanState.On;
                return "Lampada accesa";
            }
            else if (state == EHardwareTrigger.Off)
            {
                UseGPIO(LampPin, PinValue.Low);
                LampState = EBooleanState.Off;
                return "Lampada spenta";
            }
            else return SwitchLamp(LampState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchPC(EHardwareTrigger state)
        {
            var lastState = PCState;
            if (state == EHardwareTrigger.On)
            {
                SwitchOutlets(EHardwareTrigger.On);

                string mac = NetStats.Desktop_LAN_MAC;
                Process.Start(new ProcessStartInfo() { FileName = "python", Arguments = $"Python/WoL.py {mac}" });

                return lastState == EBooleanState.On ? "PC già acceso" : "PC in accensione";
            }
            else if (state == EHardwareTrigger.Off)
            {
                PCState = EBooleanState.Off;
                var res = SSH_PC("sudo shutdown now");
                return lastState == EBooleanState.Off ? "PC già spento" : (res == "PC non raggiungibile" ? res : "PC in spegnimento");
            }
            else return SwitchPC(PCState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchOutlets(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                UseGPIO(PCPlugsPin, PinValue.High);
                OutletsState = EBooleanState.On;
                PCState = EBooleanState.On;
                return "Ciabatta accesa";
            }
            else if (state == EHardwareTrigger.Off)
            {
                if (PCState == EBooleanState.On)
                {
                    Task.Run(async () =>
                    {
                        SwitchPC(EHardwareTrigger.Off);
                        await Task.Delay(1000);

                        var start = DateTime.Now;
                        while (PingPC() && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

                        if ((DateTime.Now - start).Seconds < 3) await Task.Delay(25000);
                        else await Task.Delay(5000);

                        SwitchOutlets(EHardwareTrigger.Off);

                    });
                    return "PC e ciabatta in spegnimento";
                }
                UseGPIO(PCPlugsPin, PinValue.Low);
                OutletsState = EBooleanState.Off;

                return "Ciabatta spenta";
            }
            else return SwitchOutlets(OutletsState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchOLED(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                try
                {
                    Process.Start(new ProcessStartInfo() { FileName = "python", Arguments = "Python/OLED_ON.py" });
                    OLEDState = EBooleanState.On;
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
                    Process.Start(new ProcessStartInfo() { FileName = "python", Arguments = "Python/OLED_OFF.py" });
                    OLEDState = EBooleanState.Off;
                    return "Display spento";
                }
                catch
                {
                    return "Display non raggiungibile";
                }
            }
            else return SwitchOLED(OLEDState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SSH_PC(string command)
        {
            var usr = NetStats.DesktopUsername;
            var addr = NetStats.Desktop_WLAN_IP;
            try
            {
                var x = Process.Start(new ProcessStartInfo() {
                     FileName = "python", 
                     Arguments = $"Python/SSH.py {usr} {addr} {command}",
                     RedirectStandardOutput = true
                });
                string output = x.StandardOutput.ReadToEnd();
                var ls = output.ToList();
                string code = "-1";
                for(int i=ls.Count-1; i>=0; i--)
                {
                    if(char.IsNumber(ls[i])) code = ls[i].ToString();
                }
                if(code == "65280") return "CONN_ERROR";
                else if(code == "0") return output.Length == 0 ? code : output;
                return "ERROR";
            }
            catch
            {
                return "CONN_ERROR";
            }
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
            var x =  controller.OpenPin(Pin, PinMode.Output);
            controller.Write(Pin, Value);
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