using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public class HazeC2SAuthenticateHandler : HazeC2SMessageHandler<HazeC2SAuthenticateMessage>
{
    // from `openssl rand -base64 48`
    private const string FallbackClientSecret = "nwlbImS3YZRFrFIirWAeZEwNkEUt4W9uUaeBUmuQKltDNy/6CJjzP3bJ5cai7PZQ";

    public HazeC2SAuthenticateHandler(HazeDbContext dbContext, ILogger logger) : base(dbContext, logger) { }

    private bool FixedTimeCompareClientSecret(string userClientSecret, HazeClient? client)
    {
        userClientSecret = userClientSecret.Normalize();
        var trueClientSecret = client?.ClientSecret.Normalize() ?? FallbackClientSecret;
        var trueClientSecretBytes = Encoding.UTF8.GetBytes(trueClientSecret);
        if (userClientSecret.Length != trueClientSecret.Length) {
            CryptographicOperations.FixedTimeEquals(trueClientSecretBytes, trueClientSecretBytes);
            return false;
        }
        var userClientSecretBytes = Encoding.UTF8.GetBytes(userClientSecret);
        return CryptographicOperations.FixedTimeEquals(userClientSecretBytes, trueClientSecretBytes);
    }

    public override async Task Handle(HazeC2SAuthenticateMessage message, HazeMessageHandlerContext context, CancellationToken ct = default)
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
        var client = await DbContext.HazeClients.FindAsync([message.ClientId], ct);
        var secretMatches = FixedTimeCompareClientSecret(message.ClientSecret, client);

        if (client is null || !secretMatches) {
            await context.CloseConnection(
                new HazeS2CErrorMessage
                {
                    RegardingMessageId =  message.MessageId,
                    ErrorCode = "ERR_INVALID_CREDENTIALS",
                    ErrorTitle = "Unauthorized",
                    ErrorDetail = "Invalid credentials",
                },
                WebSocketCloseStatus.PolicyViolation,
                "Failed to authenticate",
                ct
            );
            return;
        }
        context.Session.ClientId = client.ClientId;
        await DbContext.SaveChangesAsync(ct);
    }
}
