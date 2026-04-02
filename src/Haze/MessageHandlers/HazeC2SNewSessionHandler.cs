using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public class HazeC2SNewSessionHandler : HazeC2SMessageHandler<HazeC2SNewSessionMessage>
{
    public HazeC2SNewSessionHandler(HazeDbContext dbContext, ILogger logger) : base(dbContext, logger) { }

    public Guid GenerateSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return new Guid(bytes);
    }

    public override async Task Handle(HazeC2SNewSessionMessage message, HazeMessageHandlerContext context, CancellationToken ct = default)
    {
        context.Session = new HazeClientSession { SessionId = GenerateSessionId() };
        DbContext.HazeClientSessions.Add(context.Session);
        await DbContext.SaveChangesAsync(ct);
    }
}
