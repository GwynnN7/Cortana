using Iot.Device.CpuTemperature;
using Newtonsoft.Json;
using System.Device.Gpio;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;

namespace Utility
{
    public static class HardwareDriver
    {
        private const int RelayPin_0 = 25; //PC-OUTLET
        private const int RelayPin_1 = 8;  //LAMP
        private const int RelayPin_2 = 7;  //GUITAR
        private const int RelayPin_3 = 1;  //GENERAL

        private static int SwitchPin = 4;
        private static int LEDPin = 14;
        private static int PCPlugsPin = RelayPin_0;
        private static int LampPin = RelayPin_1;
        private static int GuitarPin = RelayPin_2;
        private static int GeneralPin = RelayPin_3;

        private static EBooleanState OutletsState = EBooleanState.Off;
        private static EBooleanState PCState = EBooleanState.Off;
        private static EBooleanState LEDState = EBooleanState.Off;
        private static EBooleanState OLEDState = EBooleanState.Off;
        private static EBooleanState LampState = EBooleanState.Off;
        private static EBooleanState GuitarState = EBooleanState.Off;
        private static EBooleanState GeneralState = EBooleanState.Off;

        public static NetworkStats NetStats;

        public static void Init()
        {
            LoadNetworkData();

            SwitchRoom(EHardwareTrigger.Off);
            HandleNight();
        }

        public static void LoadNetworkData()
        {
            if (File.Exists("Data/Global/NetworkData.json"))
            {
                var file = File.ReadAllText("Data/Global/NetworkData.json");
                NetStats = JsonConvert.DeserializeObject<NetworkStats>(file) ?? new();
            }
        }

        public static void HandleNight()
        {
            new UtilityTimer(Name: "night-handler", TargetTime: new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 1, 0, 0), Callback: HandleNightCallback, TimerLocation: ETimerLocation.Utility, Loop: ETimerLoop.Quotidiano);
        }

        private static void HandleNightCallback(object? sender, EventArgs e)
        {
            if (PCState == EBooleanState.Off) SwitchRoom(EHardwareTrigger.Off);
            else Functions.RequestPC("notify/night");

            if (DateTime.Now.Hour < 4) new UtilityTimer(Name: "safety-night-handler", Hours: 1, Minutes: 0, Seconds: 0, Callback: HandleNightCallback, TimerLocation: ETimerLocation.Utility, Loop: ETimerLoop.No);
        }

        public static string SwitchRoom(EHardwareTrigger state)
        {
            
            if (state == EHardwareTrigger.On) SwitchPC(state);
            else
            {
                SwitchLamp(state);
                SwitchOutlets(state);
            }
            SwitchOLED(state);
            SwitchLED(state);

            return "Procedo";
        }

        public static string SwitchLamp(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                if (LampState == EBooleanState.Off) UseGPIO(LampPin, PinValue.Low); //Pull-Up
                LampState = EBooleanState.On;
                return "Lampada accesa";
            }
            else if (state == EHardwareTrigger.Off)
            {
                if (LampState == EBooleanState.On) UseGPIO(LampPin, PinValue.High); //Pull-Up
                LampState = EBooleanState.Off;
                return "Lampada spenta";
            }
            else return SwitchLamp(LampState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchGuitar(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                if (GuitarState == EBooleanState.Off) UseGPIO(GuitarPin, PinValue.Low); //Pull-Up
                GuitarState = EBooleanState.On;
                return "Chitarra accesa";
            }
            else if (state == EHardwareTrigger.Off)
            {
                if (GuitarState == EBooleanState.On) UseGPIO(GuitarPin, PinValue.High); //Pull-Up
                GuitarState = EBooleanState.Off;
                return "Chitarra spenta";
            }
            else return SwitchGuitar(GuitarState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchGeneral(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                if (GeneralState == EBooleanState.Off) UseGPIO(GeneralPin, PinValue.Low); //Pull-Up
                GeneralState = EBooleanState.On;
                return "Presa generale accesa";
            }
            else if (state == EHardwareTrigger.Off)
            {
                if (GeneralState == EBooleanState.On) UseGPIO(GeneralPin, PinValue.High); //Pull-Up
                GeneralState = EBooleanState.Off;
                return "Presa generale spenta";
            }
            else return SwitchGeneral(GeneralState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchPC(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                PCState = EBooleanState.On;
                if (OutletsState == EBooleanState.Off)
                {
                    SwitchOutlets(EHardwareTrigger.On);
                    return "PC e ciabatta in accensione";
                }
                else
                {
                    string mac = NetStats.Desktop_LAN_MAC;
                    Process.Start(new ProcessStartInfo() { FileName = "python", Arguments = $"Python/WoL.py {mac}" });

                    return "PC in accensione";
                }

            }
            else if (state == EHardwareTrigger.Off)
            {
                string text = "PC in spegnimento";
                try
                {
                    var result = Functions.RequestPC("hardware/shutdown");
                    if (!result) text = "PC già spento";
                }
                catch
                {
                    text = "PC già spento";
                }
                finally
                {
                    PCState = EBooleanState.Off;
                }
                return text;
            }
            else return SwitchPC(PCState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchOutlets(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                UseGPIO(PCPlugsPin, PinValue.Low); //Pull-Up
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
                        while (PingPC() && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1000);

                        await Task.Delay(10000);
                        SwitchOutlets(EHardwareTrigger.Off);

                    });
                    return "PC e ciabatta in spegnimento";
                }
                UseGPIO(PCPlugsPin, PinValue.High); //Pull-Up
                OutletsState = EBooleanState.Off;

                return "Ciabatta spenta";
            }
            else return SwitchOutlets(OutletsState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchLED(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                UseGPIO(LEDPin, PinValue.High);
                LEDState = EBooleanState.On;
                return "Led acceso";
            }
            else if (state == EHardwareTrigger.Off)
            {
                UseGPIO(LEDPin, PinValue.Low);
                LEDState = EBooleanState.Off;
                return "Led spento";
            }
            else return SwitchLED(LEDState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
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
                    return "Errore display";
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
                    return "Errore display";
                }
            }
            else return SwitchOLED(OLEDState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
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
            using Ping pingSender = new Ping();
            PingReply reply = pingSender.Send(NetStats.Desktop_WLAN_IP, 2000);

            return reply.Status == IPStatus.Success;
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
    }
}