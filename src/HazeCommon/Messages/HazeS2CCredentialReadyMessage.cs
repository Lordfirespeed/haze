namespace HazeCommon.Messages;

public class HazeS2CCredentialReadyMessage : HazeS2CMessage
{
    public required string AccountName { get; init; }
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
}
