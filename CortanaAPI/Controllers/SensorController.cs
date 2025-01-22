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
    public string PowerDevice([FromRoute] string sensor)
    {
        Enum.TryParse(sensor, out ESensorData x);
        return HardwareProxy.GetSensorInfo(x);
    }
}