namespace HazeCommon.Messages;

public class HazeS2CSessionCreatedMessage : HazeS2CResponse
{
    public required string SessionId { get; set; }
}
