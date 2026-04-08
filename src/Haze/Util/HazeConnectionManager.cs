using System.Collections.Generic;
using System.Threading.Tasks;
using HazeCommon.Messages;

namespace Haze.Util;

public class HazeConnectionManager
{
    private readonly Dictionary<string, HazeMessageHandlerContext> _sessionIdToConnectionMap = new();

    public HazeMessageHandlerContext? GetConnection(string sessionId)
        => _sessionIdToConnectionMap.TryGetValue(sessionId, out var context) ? context : null;

    public void AddConnection(string sessionId, HazeMessageHandlerContext connection)
        => _sessionIdToConnectionMap.Add(sessionId, connection);

    public bool DropConnection(string sessionId)
        => _sessionIdToConnectionMap.Remove(sessionId);

    public bool IsConnected(string sessionId)
        => _sessionIdToConnectionMap.ContainsKey(sessionId);

    public ValueTask QueueMessage(string sessionId, HazeS2CMessage message)
        => _sessionIdToConnectionMap[sessionId].QueueS2CMessage(message);
}
