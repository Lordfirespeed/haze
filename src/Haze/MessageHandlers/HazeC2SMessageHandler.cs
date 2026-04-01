using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Haze.Util;
using HazeCommon.Messages;

namespace Haze.MessageHandlers;

public interface IHazeC2SMessageHandler
{
    public Task Handle(HazeWebSocket webSocket, HazeC2SMessage message, CancellationToken ct = default);
    public bool CanHandle(Type messageType);
}

public abstract class HazeC2SMessageHandler<TMessage> : IHazeC2SMessageHandler where TMessage : HazeC2SMessage
{
    protected HazeDbContext DbContext;

    public HazeC2SMessageHandler(HazeDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public abstract Task Handle(HazeWebSocket webSocket, TMessage message, CancellationToken ct = default);

    public Task Handle(HazeWebSocket webSocket, HazeC2SMessage message, CancellationToken ct = default)
    {
        Debug.Assert(message.GetType().IsAssignableTo(typeof(TMessage)));
        return Handle(webSocket, (TMessage)message, ct);
    }

    public virtual bool CanHandle(Type messageType) => messageType.IsAssignableTo(typeof(TMessage));
}
