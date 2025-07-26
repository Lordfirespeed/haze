using Haze.Util;
using Microsoft.AspNetCore.Mvc;

namespace Haze.Controllers;


public class HomeController : ControllerBase
{
    [Route("/api")]
    [HttpGet, HttpHead]
    public IActionResult Index() =>
        new StandardJsonResult(null);
}
