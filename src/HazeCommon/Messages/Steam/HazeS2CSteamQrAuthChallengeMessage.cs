using System;

namespace HazeCommon.Messages.Steam;

public class HazeS2CSteamQrAuthChallengeMessage: HazeS2CResponse
{
    public required Uri ChallengeUrl { get; set; }
}
