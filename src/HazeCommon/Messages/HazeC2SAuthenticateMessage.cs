namespace HazeCommon.Messages;

public class HazeC2SAuthenticateMessage : HazeC2SMessage
{
    public string ClientId { get; set; }

    public string ClientSecret { get; set; }
}
