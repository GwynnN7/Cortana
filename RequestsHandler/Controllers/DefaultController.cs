using Microsoft.AspNetCore.Mvc;

namespace RequestsHandler.Controllers
{
    [Route("cortana-api")]
    [ApiController]
    public class DefaultController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            return "Hi, I'm Cortana";
        }
    }
}
