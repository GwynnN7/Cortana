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
    public string Mode([FromQuery] int? val)
    {
        if(val.HasValue) HardwareApi.Sensors.Settings.LimitControlMode = (EControlMode) Math.Clamp(val.Value, (int) EControlMode.Manual, (int) EControlMode.Automatic);
        return $"Current/Limit: {HardwareApi.Sensors.ControlMode}/{HardwareApi.Sensors.Settings.LimitControlMode}";
    }
    
    [HttpGet("threshold")]
    public string Light([FromQuery] int? val)
    {
        if(val.HasValue) HardwareApi.Sensors.Settings.LightThreshold = val.Value;
        return $"Light/Threshold: {HardwareApi.Sensors.GetData(ESensor.Light)}/{HardwareApi.Sensors.Settings.LightThreshold}";
    }
}