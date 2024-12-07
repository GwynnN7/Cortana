using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Kernel.Software;

namespace CortanaAPI.Controllers;

[Route("")]
[ApiController]
public class HomeController : ControllerBase
{
	[HttpGet]
	public ContentResult Get()
	{
		string html = FileHandler.LoadHtml("Home");
		string url = Request.GetEncodedUrl();
		html = html.Replace("<<PageUrl>>", url[..^1]);
		return base.Content(html, "text/html");
	}

	[HttpGet("api")]
	public string GetApi()
	{
		return "Hi, I'm Cortana";
	}
}