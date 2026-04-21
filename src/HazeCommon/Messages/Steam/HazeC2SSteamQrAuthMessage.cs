using HazeCommon.Models;

namespace HazeCommon.Messages.Steam;

public class HazeC2SSteamQrAuthMessage: HazeC2SMessage
{
    public SteamAccountCredentialUsage CredentialUsage { get; set; }
}
