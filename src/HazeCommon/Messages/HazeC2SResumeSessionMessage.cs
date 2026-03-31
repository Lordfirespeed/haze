namespace HazeCommon.Messages;

public class HazeC2SResumeSessionMessage: HazeC2SMessage
{
    public required string SessionId { get; init; }
}
