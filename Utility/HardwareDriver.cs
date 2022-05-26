using Iot.Device.CpuTemperature;
using System.Device.Gpio;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

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

        public static void HandleNight()
        {
            new UtilityTimer(Name: "night-handler", TargetTime: new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 2, 0, 0), Callback: HandleNightCallback, TimerLocation: ETimerLocation.Utility, Loop: ETimerLoop.Quotidiano);
        }

        private static void HandleNightCallback(object? sender, EventArgs e)
        {
            var result = Functions.RequestPC("notify/night");
            if (!result)
            {
                SwitchLED(EHardwareTrigger.Off);
                SwitchOLED(EHardwareTrigger.Off);
                SwitchOutlets(EHardwareTrigger.Off);
            }
            SwitchLamp(EHardwareTrigger.Off);
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

        public static void SwitchRoom(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On) SwitchPC(state);
            else SwitchOutlets(state);
            SwitchOLED(state);
            SwitchLED(state);

            if (DateTime.Now.Hour < 20 && DateTime.Now.Hour > 7 && state == EHardwareTrigger.Off) SwitchLamp(state);
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
                    PhysicalAddress target = PhysicalAddress.Parse("B4-2E-99-31-CF-74");
                    var header = Enumerable.Repeat(byte.MaxValue, 6);
                    var data = Enumerable.Repeat(target.GetAddressBytes(), 16).SelectMany(mac => mac);
                    var magicPacket = header.Concat(data).ToArray();
                    using var client = new UdpClient();
                    client.Send(magicPacket, magicPacket.Length, new IPEndPoint(IPAddress.Broadcast, 9));

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
                Process.Start(new ProcessStartInfo() { FileName = "python", Arguments = "Python/OLED_ON.py" });

                OLEDState = EBooleanState.On;
                return "Display acceso";
            }
            else if (state == EHardwareTrigger.Off)
            {
                Process.Start(new ProcessStartInfo() { FileName = "python", Arguments = "Python/OLED_OFF.py" });

                OLEDState = EBooleanState.Off;
                return "Display spento";
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
                return "Ciabatta accesa";
            }
            else if (state == EHardwareTrigger.Off)
            {
                if (PCState == EBooleanState.On)
                {
                    Task.Run(async () =>
                    {
                        SwitchPC(EHardwareTrigger.Off);
                        while (PingPC()) await Task.Delay(500);
                        await Task.Delay(2000);
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
            PingReply reply = pingSender.Send("192.168.1.17", 1000);

            return reply.Status == IPStatus.Success;
        }

        public static bool Ping(string ip)
        {
            using Ping pingSender = new Ping();
            PingReply reply = pingSender.Send(ip, 2000);

            return reply.Status == IPStatus.Success;
        }
    }
}