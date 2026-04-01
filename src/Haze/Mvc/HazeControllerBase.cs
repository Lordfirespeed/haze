using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Haze.Mvc;

public class HazeControllerBase<TDerived> : ControllerBase
{
    protected readonly ILogger _logger;
    protected readonly HazeDbContext _dbContext;

    public HazeControllerBase(ILogger<TDerived> logger, HazeDbContext dbContext) {
        _logger = logger;
        _dbContext = dbContext;
    }
}
