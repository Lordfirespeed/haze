using System;
using System.Threading;
using System.Threading.Tasks;
using Haze.Steam;
using Haze.Util;
using HazeCommon.Messages;
using HazeCommon.Messages.Steam;
using Microsoft.Extensions.Logging;
using SteamKit2.Authentication;

namespace Haze.MessageHandlers;

public class HazeC2SSteamQrAuthHandler : HazeC2SMessageHandler<HazeC2SSteamQrAuthMessage>
{
    public HazeC2SSteamQrAuthHandler(HazeDbContext dbContext, ILogger logger) : base(dbContext, logger) { }

    public override Task Handle(HazeC2SSteamQrAuthMessage message, HazeMessageHandlerContext context,
        CancellationToken ct = default)
    {
        _ = Task.Run(() => HandleInner(message, context, ct), ct);
        return Task.CompletedTask;
    }

    public async Task HandleInner(HazeC2SSteamQrAuthMessage message, HazeMessageHandlerContext context,
        CancellationToken ct = default)
    {
        await using var connection = new SteamConnection(Logger);
        await connection.Connect();

        Task NotifyChallengeUrl(QrAuthSession session) => context.QueueS2CMessage(
            new HazeS2CSteamQrAuthChallengeMessage {
                RegardingMessageId = message.MessageId,
                ChallengeUrl = new Uri(session.ChallengeURL),
            },
            ct
        ).AsTask();

        try {
            await connection.QrAuth(NotifyChallengeUrl, ct);
        } catch (AuthenticationException exc) {
            Logger.LogDebug("Failed authentication to steam");
            await context.QueueS2CMessage(
                new HazeS2CErrorMessage {
                    RegardingMessageId = message.MessageId,
                    ErrorCode = "ERR_STEAM_AUTH_FAILED",
                    ErrorTitle = "Unauthorized",
                    ErrorDetail = $"{exc.Result}: {exc.Message}",
                },
                ct
            );
            return;
        }
        await connection.LogOn();

        await context.QueueS2CMessage(
            new HazeS2CSuccessMessage { RegardingMessageId = message.MessageId }, ct
        );
    }
}
