using System;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public class HazeC2SEndSessionHandler : HazeC2SMessageHandler<HazeC2SNewSessionMessage>
{
    private readonly HazeConnectionManager _connectionManager;

    public HazeC2SEndSessionHandler(HazeDbContext dbContext, ILogger logger, HazeConnectionManager connectionManager) : base(dbContext, logger)
    {
        _connectionManager = connectionManager;
    }

    public override async Task Handle(HazeC2SNewSessionMessage message, HazeMessageHandlerContext context, CancellationToken ct = default)
    {
        if (context.Session is null) {
            await context.QueueS2CMessage(new HazeS2CErrorMessage
            {
                RegardingMessageId = message.MessageId,
                ErrorCode = "ERR_NO_SESSION",
                ErrorTitle = "Bad Message",
                ErrorDetail = "Session not initialised",
            }, ct);
            return;
        }

        _connectionManager.DropConnection(context.Session.SessionId);
        DbContext.HazeClientSessions.Remove(context.Session);
        await DbContext.SaveChangesAsync(ct);

        await context.CloseConnection(
            new HazeS2CSuccessMessage {
                RegardingMessageId = message.MessageId,
            },
            WebSocketCloseStatus.NormalClosure,
            "Session closed, goodbye",
            ct
        );
    }
}
