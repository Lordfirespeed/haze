namespace HazeCommon.Messages;

public class HazeS2CSessionCreatedMessage : HazeS2CMessage
{
    public required string SessionId { get; set; }
}
