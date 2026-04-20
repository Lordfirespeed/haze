using System.Text.Json.Serialization;
using HazeCommon.Messages.Steam;

namespace HazeCommon.Messages;

[JsonDerivedType(typeof(HazeS2CErrorMessage), typeDiscriminator: "error-v1")]
[JsonDerivedType(typeof(HazeS2CSuccessMessage), typeDiscriminator: "success-v1")]
[JsonDerivedType(typeof(HazeS2CSessionCreatedMessage), typeDiscriminator: "session-created-v1")]
[JsonDerivedType(typeof(HazeS2CWhoAmIResponse), typeDiscriminator: "whoami-v1-response")]
[JsonDerivedType(typeof(HazeS2CSteamQrAuthChallengeMessage), typeDiscriminator: "steam/qr-auth-challenge-v1")]
public abstract class HazeS2CMessage
{
    [JsonPropertyName("id")]
    public string MessageId { get; } = HazeMessages.MakeMessageId();
}
