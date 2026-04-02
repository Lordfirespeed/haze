using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public interface IHazeC2SMessageHandler
{
    public Task Handle(HazeC2SMessage message, HazeMessageHandlerContext context, CancellationToken ct = default);
    public bool CanHandle(Type messageType);
}

public abstract class HazeC2SMessageHandler<TMessage> : IHazeC2SMessageHandler where TMessage : HazeC2SMessage
{
    protected readonly HazeDbContext DbContext;
    protected readonly ILogger Logger;

    public HazeC2SMessageHandler(HazeDbContext dbContext, ILogger logger)
    {
        DbContext = dbContext;
        Logger = logger;
    }

    public abstract Task Handle(TMessage message, HazeMessageHandlerContext context, CancellationToken ct = default);

    public Task Handle(HazeC2SMessage message, HazeMessageHandlerContext context, CancellationToken ct = default)
    {
        Debug.Assert(message.GetType().IsAssignableTo(typeof(TMessage)));
        return Handle((TMessage)message, context, ct);
    }

    public virtual bool CanHandle(Type messageType) => messageType.IsAssignableTo(typeof(TMessage));
}
