using Kernel.Hardware;
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
        return HardwareSettings.HardwareControlMode.ToString();
    }
}