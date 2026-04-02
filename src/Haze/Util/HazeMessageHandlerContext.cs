using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Haze.Models;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.Util;

public sealed class HazeMessageHandlerContext(HazeWebSocket webSocket, ChannelWriter<HazeS2CMessage> messageQueueWriter)
{
    public ValueTask QueueS2CMessage(HazeS2CMessage message, CancellationToken ct = default)
        => messageQueueWriter.WriteAsync(message, ct);

    public Task CloseConnection(
        HazeS2CMessage? closeMessage,
        WebSocketCloseStatus closeStatus,
        string? closeStatusDetail,
        CancellationToken ct = default
    ) => webSocket.Close(closeMessage, closeStatus, closeStatusDetail, ct);

    public HazeClientSession? Session
    {
        get => webSocket.ClientSession;
        set => webSocket.ClientSession = value;
    }

    public ILogger Logger => webSocket.Logger;
}
