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
        private static int LampPin = 4;
        private static int LEDPin = 27;
        private static int OutletsPin = 17;

        private static EBooleanState OutletsState = EBooleanState.On;
        private static EBooleanState PCState = EBooleanState.On;
        private static EBooleanState LEDState = EBooleanState.On;
        private static EBooleanState OLEDState = EBooleanState.On;
        private static EBooleanState LampState = EBooleanState.Off;

        public static NetworkStats NetStats;

        //private static EBooleanState FanState = EBooleanState.Off;
        //private static SerialPort? Fan;


        public static void Init()
        {
            LoadNetworkData();
            HandleNight();
            //Fan = new SerialPort("/dev/rfcomm0", 9600);
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
            new UtilityTimer(Name: "night-handler", TargetTime: new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0), Callback: HandleNightCallback, TimerLocation: ETimerLocation.Utility, Loop: ETimerLoop.Quotidiano);
        }

        private static void HandleNightCallback(object? sender, EventArgs e)
        { 
            if (PCState == EBooleanState.Off) SwitchRoom(EHardwareTrigger.Off);  
            else Functions.RequestPC("notify/night");

            if(DateTime.Now.Hour < 5) new UtilityTimer(Name: "safety-night-handler", Hours: 0, Minutes: 30, Seconds: 0, Callback: HandleNightCallback, TimerLocation: ETimerLocation.Utility, Loop: ETimerLoop.No);
        }

        private static void ToggleLamp()
        {
            Task.Run(async () =>
            {
                using var controller = new GpioController();
                controller.OpenPin(LampPin, PinMode.Output);
                controller.Write(LampPin, PinValue.High);
                await Task.Delay(100);
                controller.Write(LampPin, PinValue.Low);
            });
        }

        public static string SwitchRoom(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On) SwitchPC(state);
            else SwitchOutlets(state);
            SwitchOLED(state);
            SwitchLED(state);
            //SwitchFan(state);

            var hour = DateTime.Now.Hour;
            if (hour >= 18 && hour <= 6) SwitchLamp(state);
            else if(state == EHardwareTrigger.Off || (state == EHardwareTrigger.Toggle && LampState == EBooleanState.On)) SwitchLamp(state);

            return "Procedo";
        }

        public static string SwitchLamp(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                if (LampState == EBooleanState.Off) ToggleLamp();
                LampState = EBooleanState.On;
                return "Lampada accesa";
            }
            else if (state == EHardwareTrigger.Off)
            {
                if (LampState == EBooleanState.On) ToggleLamp();
                LampState = EBooleanState.Off;
                return "Lampada spenta";
            }
            else return SwitchLamp(LampState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchPC(EHardwareTrigger state)
        {
            if(state == EHardwareTrigger.On)
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
            else if(state == EHardwareTrigger.Off)
            {
                string text = "PC in spegnimento";
                try
                {
                    var result = Functions.RequestPC("hardware/shutdown");
                    if(!result) text = "PC già spento";
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

        public static string SwitchLED(EHardwareTrigger state)
        {
            using var controller = new GpioController();
            controller.OpenPin(LEDPin, PinMode.Output);

            if (state == EHardwareTrigger.On)
            {
                controller.Write(LEDPin, PinValue.High);
                LEDState = EBooleanState.On;
                return "Led acceso";
            }
            else if (state == EHardwareTrigger.Off)
            {
                controller.Write(LEDPin, PinValue.Low);
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

        public static string SwitchOutlets(EHardwareTrigger state)
        {
            using var controller = new GpioController();
            controller.OpenPin(OutletsPin, PinMode.Output);

            if (state == EHardwareTrigger.On)
            {
                controller.Write(OutletsPin, PinValue.High);
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
                        while (PingPC() && (DateTime.Now - start).Seconds <= 120) await Task.Delay(1000);

                        await Task.Delay(3000);
                        SwitchOutlets(EHardwareTrigger.Off);
                        
                    });
                    return "PC e ciabatta in spegnimento";
                }
                controller.Write(OutletsPin, PinValue.Low);
                OutletsState = EBooleanState.Off;
                return "Ciabatta spenta";
            }
            else return SwitchOutlets(OutletsState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
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
    }

    public class NetworkStats
    {
        public string Cortana_IP { get; set;  }
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




/*

       public static string SwitchFan(EHardwareTrigger state)
       {
           if(state == EHardwareTrigger.On)
           {
               Task.Run(() => SendFanCommand("1"));
               FanState = EBooleanState.On;
               return "Accendo il ventilatore";
           }
           else if(state == EHardwareTrigger.Off)
           {
               Task.Run(() => SendFanCommand("0"));
               FanState = EBooleanState.Off;
               return "Spengo il ventilatore";
           }
           else return SwitchFan(FanState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
       }

       public static string SetFanSpeed(EFanSpeeds speed)
       {
           if(speed == EFanSpeeds.Off) return SwitchFan(EHardwareTrigger.Off);

           var command = speed switch
           {
               EFanSpeeds.Low => "a",
               EFanSpeeds.Medium => "b",
               EFanSpeeds.High => "c",
               _ => "a"
           };

           FanState = EBooleanState.On;
           command = $"1\n{command}";

           Task.Run(() => SendFanCommand(command));
           return "Comando inviato";
       }

       public static void SendFanCommand(string command)
       {
           try
           {
               if (Fan == null) Fan = new SerialPort("/dev/rfcomm0", 9600);

               Fan.Open();
               Fan.Write(command);
               Fan.Close();
           }
           catch { }
       }
       */