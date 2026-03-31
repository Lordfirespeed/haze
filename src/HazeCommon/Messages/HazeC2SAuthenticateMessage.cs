namespace HazeCommon.Messages;

public class HazeC2SAuthenticateMessage : HazeC2SMessage
{
    public required string ClientId { get; set; }

    public required string ClientSecret { get; set; }
}
