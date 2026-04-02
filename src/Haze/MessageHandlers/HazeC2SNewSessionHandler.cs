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
    private const string SessionIdCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

    public HazeC2SNewSessionHandler(HazeDbContext dbContext, ILogger logger) : base(dbContext, logger) { }

    public string GenerateSessionId() => RandomNumberGenerator.GetString(SessionIdCharacters, 64);

    public override async Task Handle(HazeC2SNewSessionMessage message, HazeMessageHandlerContext context, CancellationToken ct = default)
    {
        if (context.Session is not null) {
            context.Logger.LogInformation("Session already initialised");
            return;
        }

        context.Session = new HazeClientSession { SessionId = GenerateSessionId() };
        DbContext.HazeClientSessions.Add(context.Session);
        await DbContext.SaveChangesAsync(ct);
        await context.QueueS2CMessage(new HazeS2CSessionCreatedMessage {
            SessionId = context.Session.SessionId,
            RegardingMessageId = message.MessageId,
        }, ct);
    }
}
