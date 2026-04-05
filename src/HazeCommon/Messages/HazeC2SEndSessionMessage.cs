namespace HazeCommon.Messages;

public class HazeC2SEndSessionMessage: HazeC2SMessage
{
    public required string SessionId { get; init; }
}
