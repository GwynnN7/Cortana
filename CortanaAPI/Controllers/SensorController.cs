using Kernel.Hardware;
using Kernel.Hardware.DataStructures;
using Microsoft.AspNetCore.Mvc;

namespace CortanaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SensorController : ControllerBase
{
    [HttpGet]
    public string Get()
    {
        return "Sensor route: specify the sensor";
    }

    [HttpGet("{sensor}")]
    public string SensorData([FromRoute] string sensor)
    {
        return HardwareApi.Sensors.GetData(sensor);
    }
    
    [HttpGet("mode")]
    public string Mode()
    {
        return HardwareApi.Sensors.ControlMode.ToString();
    }
    
    [HttpGet("mode/{mode}")]
    public string SetMode([FromRoute] int mode)
    {
        HardwareApi.Sensors.Settings.LimitControlMode = (EControlMode) Math.Clamp(mode, (int) EControlMode.Manual, (int) EControlMode.Automatic);
        return $"Limit mode: {HardwareApi.Sensors.Settings.LimitControlMode} ~ Current mode: {HardwareApi.Sensors.ControlMode}";
    }
    
    [HttpGet("threshold")]
    public string Light()
    {
        return HardwareApi.Sensors.Settings.LightThreshold.ToString();
    }
    
    [HttpGet("threshold/{light}")]
    public string SetLight([FromRoute] int light)
    {
        HardwareApi.Sensors.Settings.LightThreshold = light;
        return $"Current Light: {HardwareApi.Sensors.GetData(ESensor.Light)} ~ Light Threshold: {HardwareApi.Sensors.Settings.LightThreshold}";
    }
}