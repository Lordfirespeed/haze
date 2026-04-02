namespace HazeCommon.Messages;

public class HazeS2CWhoAmIResponse : HazeS2CResponse
{
    public required string? ClientId { get; set; }
}
