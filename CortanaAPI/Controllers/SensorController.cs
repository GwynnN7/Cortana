using Kernel.Hardware;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Interfaces;
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
        return HardwareProxy.GetSensorInfo(sensor);
    }
    
    [HttpGet("mode")]
    public string Mode()
    {
        return HardwareSettings.CurrentControlMode.ToString();
    }
    
    [HttpGet("mode/{mode}")]
    public string SetMode([FromRoute] int mode)
    {
        HardwareSettings.LimitControlMode = mode switch
        {
            0 => EControlMode.Manual,
            1 => EControlMode.Night,
            2 => EControlMode.Automatic,
            _ => HardwareSettings.LimitControlMode
        };
        return $"Limit mode: {HardwareSettings.LimitControlMode} ~ Current mode: {HardwareSettings.CurrentControlMode}";
    }
    
    [HttpGet("threshold")]
    public string Light()
    {
        return HardwareSettings.LightThreshold.ToString();
    }
    
    [HttpGet("threshold/{light}")]
    public string SetLight([FromRoute] int light)
    {
        HardwareSettings.LightThreshold = light;
        return $"Current Light: {HardwareProxy.GetSensorInfo(ESensor.Light)} ~ Light Threshold: {HardwareSettings.LightThreshold}";
    }
}