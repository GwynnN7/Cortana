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
        return $"Current/Limit: {HardwareApi.Sensors.ControlMode}/{HardwareApi.Sensors.Settings.LimitControlMode}";
    }
    
    [HttpGet("mode/{mode}")]
    public string SetMode([FromRoute] int mode)
    {
        HardwareApi.Sensors.Settings.LimitControlMode = (EControlMode) Math.Clamp(mode, (int) EControlMode.Manual, (int) EControlMode.Automatic);
        return Mode();
    }
    
    [HttpGet("threshold")]
    public string Light()
    {
        return $"Light/Threshold: {HardwareApi.Sensors.GetData(ESensor.Light)}/{HardwareApi.Sensors.Settings.LightThreshold}";
    }
    
    [HttpGet("threshold/{light}")]
    public string SetLight([FromRoute] int light)
    {
        HardwareApi.Sensors.Settings.LightThreshold = light;
        return Light();
    }
}