using Iot.Device.CpuTemperature;
using Renci.SshNet;
using System.Device.Gpio;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Utility
{
    public static class HardwareDriver
    {
        private static int LightRelayPin = 4;
        private static int LEDPin = 27;
        private static int OutletsPin = 17;

        private static EBooleanState OutletsState = EBooleanState.On;
        private static EBooleanState PCState = EBooleanState.On;
        private static EBooleanState LEDState = EBooleanState.Off;
        private static EBooleanState OLEDState = EBooleanState.Off;

        public static string ToggleLamp()
        {
            using var controller = new GpioController();
            controller.OpenPin(LightRelayPin, PinMode.Output);

            controller.Write(LightRelayPin, PinValue.High);
            Thread.Sleep(100);
            controller.Write(LightRelayPin, PinValue.Low);

            return "Lampada attivata";
        }

        public static string SwitchPC(EHardwareTrigger state)
        {
            if(state == EHardwareTrigger.On)
            {
                PCState = EBooleanState.On;
                if (OutletsState == EBooleanState.Off)
                {
                    SwitchOutlets(EHardwareTrigger.On);
                    return "PC e Ciabatta in accensione";
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
                    using var client = new HttpClient();
                    client.GetAsync("http://192.168.1.17:5000/cortana-pc/hardware/shutdown");
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
                return "Led Acceso";
            }
            if (state == EHardwareTrigger.Off)
            {
                controller.Write(LEDPin, PinValue.Low);
                LEDState = EBooleanState.Off;
                return "Led Spento";
            }
            else return SwitchLED(LEDState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
        }

        public static string SwitchOLED(EHardwareTrigger state)
        {
            if (state == EHardwareTrigger.On)
            {
                Process.Start(new ProcessStartInfo() { FileName = "python", Arguments = "Python/OLED_ON.py" });

                OLEDState = EBooleanState.On;
                return "Display Acceso";
            }
            if (state == EHardwareTrigger.Off)
            {
                Process.Start(new ProcessStartInfo() { FileName = "python", Arguments = "Python/OLED_OFF.py" });

                OLEDState = EBooleanState.Off;
                return "Display Spento";
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
                return "Ciabatta Accesa";
            }
            if (state == EHardwareTrigger.Off)
            {
                if(PCState == EBooleanState.On)
                {
                    Task.Run(() =>
                    {
                        SwitchPC(EHardwareTrigger.Off);
                        while (PingPC()) { Task.Delay(1000); };
                        Task.Delay(1000);
                        controller.Write(OutletsPin, PinValue.Low);
                    });
                    return "PC e Ciabatta in spegnimento";
                }
                controller.Write(OutletsPin, PinValue.Low);
                OutletsState = EBooleanState.Off;
                return "Ciabatta Spenta";
            }
            else return SwitchLED(OutletsState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
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