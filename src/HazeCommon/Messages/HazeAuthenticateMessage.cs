namespace HazeCommon.Messages;

public class HazeAuthenticateMessage : HazeMessage
{
    public string ClientId { get; set; }

    public string ClientSecret { get; set; }
}
