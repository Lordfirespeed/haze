using Haze.Mvc;
using Haze.Util;
using Microsoft.AspNetCore.Mvc;

namespace Haze.Controllers;


public class HomeController : HazeControllerBase
{
    [Route("/api")]
    [HttpGet, HttpHead]
    public IActionResult Index() =>
        new StandardJsonResult(null);
}
