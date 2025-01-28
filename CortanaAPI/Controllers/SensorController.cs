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
        return HardwareAdapter.GetSensorInfo(sensor);
    }
    
    [HttpGet("mode")]
    public string Mode()
    {
        return HardwareAdapter.ControlMode.ToString();
    }
    
    [HttpGet("mode/{mode}")]
    public string SetMode([FromRoute] int mode)
    {
        Settings settings = HardwareAdapter.GetSettings();
        settings.LimitControlMode = mode switch
        {
            0 => EControlMode.Manual,
            1 => EControlMode.Night,
            2 => EControlMode.Automatic,
            _ => settings.LimitControlMode
        };
        return $"Limit mode: {settings.LimitControlMode} ~ Current mode: {HardwareAdapter.ControlMode}";
    }
    
    [HttpGet("threshold")]
    public string Light()
    {
        return HardwareAdapter.GetSettings().LightThreshold.ToString();
    }
    
    [HttpGet("threshold/{light}")]
    public string SetLight([FromRoute] int light)
    {
        Settings settings = HardwareAdapter.GetSettings();
        settings.LightThreshold = light;
        return $"Current Light: {HardwareAdapter.GetSensorInfo(ESensor.Light)} ~ Light Threshold: {settings.LightThreshold}";
    }
}