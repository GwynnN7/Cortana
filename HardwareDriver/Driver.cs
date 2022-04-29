using System.Device.Gpio;

namespace HardwareDriver
{
    public enum State
    {
        On,
        Off,
        Toggle
    }
    public static class Driver
    {
        private static int LightRelayPin = 4;
        private static int LEDPin = 3;
        private static int PCPin = 2;

        private static bool PCPowerState = true;
        private static bool LEDState = false;
        public static void ToggleLight()
        {
            using var controller = new GpioController();
            controller.OpenPin(LightRelayPin, PinMode.Output);

            controller.Write(LightRelayPin, PinValue.High);
            Thread.Sleep(100);
            controller.Write(LightRelayPin, PinValue.Low);
        }

        public static string SwitchPC(string state)
        {
            var enumState = state switch
            {
                "on" => State.On,
                "off" => State.Off,
                "toggle" => State.Toggle,
                _ => State.On
            };

            if (enumState == State.On && PCPowerState == true) return "PC già alimentato";
            else if (enumState == State.Off && PCPowerState == false) return "PC già spento";

            using var controller = new GpioController();
            controller.OpenPin(PCPin, PinMode.Output);

            if (enumState == State.On)
            {
                controller.Write(PCPin, PinValue.High);
                PCPowerState = true;
            }
            else if (enumState == State.Off)
            {
                controller.Write(PCPin, PinValue.Low);
                PCPowerState = false;
            }
            else
            {
                controller.Write(PCPin, PCPowerState ? PinValue.Low : PinValue.High);
                PCPowerState = !PCPowerState;
            }
            return "done";
        }

        public static void SwitchLED(string state)
        {
            var enumState = state switch
            {
                "on" => State.On,
                "off" => State.Off,
                "toggle" => State.Toggle,
                _ => State.Off
            };

            using var controller = new GpioController();
            controller.OpenPin(LEDPin, PinMode.Output);

            if (enumState == State.On) controller.Write(LEDPin, PinValue.High);
            else if (enumState == State.Off) controller.Write(LEDPin, PinValue.Low);
            else controller.Write(LEDPin, LEDState ? PinValue.Low : PinValue.High);

            LEDState = !LEDState;
        }

        public static void BlinkLED()
        {
            using var controller = new GpioController();
            controller.OpenPin(LEDPin, PinMode.Output);

            controller.Write(LEDPin, PinValue.High);
            Thread.Sleep(1000);
            controller.Write(LEDPin, PinValue.Low);
            Thread.Sleep(1000);
            controller.Write(LEDPin, PinValue.High);
            LEDState = true;
        }
    }
}