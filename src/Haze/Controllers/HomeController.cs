using Haze.Mvc;
using Haze.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Haze.Controllers;


public class HomeController : HazeControllerBase<HomeController>
{
    public HomeController(ILogger<HomeController> logger) : base(logger) { }

    [Route("/api")]
    [HttpGet, HttpHead]
    public IActionResult Index() =>
        new StandardJsonResult(null);
}
