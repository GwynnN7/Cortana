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
            string result = "";
            switch(state)
                {
                    case EHardwareTrigger.On:
                    {
                        if(OutletsState == EBooleanState.Off)
                        {
                            SwitchOutlets(EHardwareTrigger.On);
                            Thread.Sleep(2000);
                        }
                        PhysicalAddress target = PhysicalAddress.Parse("B4-2E-99-31-CF-74");
                        var header = Enumerable.Repeat(byte.MaxValue, 6);
                        var data = Enumerable.Repeat(target.GetAddressBytes(), 16).SelectMany(mac => mac);
                        var magicPacket = header.Concat(data).ToArray();
                        using var client = new UdpClient();
                        client.Send(magicPacket, magicPacket.Length, new IPEndPoint(IPAddress.Broadcast, 9));

                        PCState = EBooleanState.On;
                        result = "PC in accensione";
                        break;
                    }
                    case EHardwareTrigger.Off:
                        PCState = EBooleanState.Off;
                        result = "Ancora non posso spegnere il pc";
                        break;
                    case EHardwareTrigger.Toggle:
                        return SwitchPC(PCState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
                }

            return result;
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
                new Process()
                {
                    StartInfo = new ProcessStartInfo() { FileName = "/bin/bash", Arguments = "python Python/OLED_ON.py", }
                }.Start();

                OLEDState = EBooleanState.On;
                return "Led Acceso";
            }
            if (state == EHardwareTrigger.Off)
            {
                new Process()
                {
                    StartInfo = new ProcessStartInfo() { FileName = "/bin/bash", Arguments = "python Python/OLED_OFF.py", }
                }.Start();
                OLEDState = EBooleanState.Off;
                return "Led Spento";
            }
            else return SwitchLED(OLEDState == EBooleanState.On ? EHardwareTrigger.Off : EHardwareTrigger.On);
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
    }
}