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
    private readonly HazeConnectionManager _connectionManager;
    private const string SessionIdCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

    public HazeC2SNewSessionHandler(HazeDbContext dbContext, ILogger logger, HazeConnectionManager connectionManager) : base(dbContext, logger)
    {
        _connectionManager = connectionManager;
    }

    public string GenerateSessionId() => RandomNumberGenerator.GetString(SessionIdCharacters, 64);

    public override async Task Handle(HazeC2SNewSessionMessage message, HazeMessageHandlerContext context, CancellationToken ct = default)
    {
        if (context.Session is not null) {
            await context.QueueS2CMessage(new HazeS2CErrorMessage
            {
                RegardingMessageId = message.MessageId,
                ErrorCode = "ERR_SESSION_EXISTS",
                ErrorTitle = "Bad Message",
                ErrorDetail = "Session already initialised",
            }, ct);
            return;
        }

        context.Session = new HazeClientSession { SessionId = GenerateSessionId() };
        DbContext.HazeClientSessions.Add(context.Session);
        await DbContext.SaveChangesAsync(ct);
        _connectionManager.AddConnection(context.Session.SessionId, context);
        await context.QueueS2CMessage(new HazeS2CSessionCreatedMessage {
            SessionId = context.Session.SessionId,
            RegardingMessageId = message.MessageId,
        }, ct);
    }
}
