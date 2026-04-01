using Haze.Mvc;
using Haze.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Haze.Controllers;


public class HomeController : HazeControllerBase<HomeController>
{
    public HomeController(ILogger<HomeController> logger, HazeDbContext dbContext) : base(logger, dbContext) { }

    [Route("/api")]
    [HttpGet, HttpHead]
    public IActionResult Index() =>
        new StandardJsonResult(null);
}
