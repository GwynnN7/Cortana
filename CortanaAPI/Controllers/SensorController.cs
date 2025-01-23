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
    
    [HttpGet("mode/{set}")]
    public string SetMode([FromRoute] int mode)
    {
        if(mode is >= 0 and <= 2) HardwareSettings.LimitControlMode = (EControlMode) mode;
        return $"Limit mode: {HardwareSettings.LimitControlMode}, current mode: {HardwareSettings.CurrentControlMode}";
    }
}