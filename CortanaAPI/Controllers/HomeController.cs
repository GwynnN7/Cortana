using Microsoft.AspNetCore.Mvc;
using Processor;

namespace CortanaAPI.Controllers;

[Route("")]
[ApiController]
public class HomeController : ControllerBase
{
	[HttpGet]
	public ContentResult Get()
	{
		return base.Content(Software.LoadHtml("Home"), "text/html");
	}

	[HttpGet("api")]
	public string GetApi()
	{
		return "Hi, I'm Cortana";
	}
}