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

        private static bool PCState = false;
        public static void ToggleLight()
        {
            using var controller = new GpioController();
            controller.OpenPin(LightRelayPin, PinMode.Output);

            controller.Write(LightRelayPin, PinValue.High);
            Thread.Sleep(100);
            controller.Write(LightRelayPin, PinValue.Low);
        }

        public static void SwitchPC(string state)
        {
            var enumState = state switch
            {
                "on" => State.On,
                "off" => State.Off,
                "toggle" => State.Toggle,
                "_" => State.On
            };

            using var controller = new GpioController();
            controller.OpenPin(PCPin, PinMode.Output);

            if(enumState == State.On) controller.Write(PCPin, PinValue.High);
            else if(enumState == State.Off) controller.Write(PCPin, PinValue.Low);
            else controller.Write(PCPin, PCState ? PinValue.Low : PinValue.High);

            PCState = !PCState;
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
        }
    }
}