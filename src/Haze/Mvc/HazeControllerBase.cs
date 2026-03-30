using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Haze.Mvc;

public class HazeControllerBase<TDerived> : ControllerBase
{
    protected readonly ILogger _logger;

    public HazeControllerBase(ILogger<TDerived> logger) {
        _logger = logger;
    }
}
