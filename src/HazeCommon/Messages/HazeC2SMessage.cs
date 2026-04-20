using System.Text.Json.Serialization;
using HazeCommon.Messages.Steam;

namespace HazeCommon.Messages;

[JsonDerivedType(typeof(HazeC2SAuthenticateMessage), typeDiscriminator: "authenticate-v1")]
[JsonDerivedType(typeof(HazeC2SHeartbeatMessage), typeDiscriminator: "heartbeat-v1")]
[JsonDerivedType(typeof(HazeC2SNewSessionMessage), typeDiscriminator: "new-session-v1")]
[JsonDerivedType(typeof(HazeC2SResumeSessionMessage), typeDiscriminator: "resume-session-v1")]
[JsonDerivedType(typeof(HazeC2SEndSessionMessage), typeDiscriminator: "end-session-v1")]
[JsonDerivedType(typeof(HazeC2SWhoAmIMessage), typeDiscriminator: "whoami-v1")]
[JsonDerivedType(typeof(HazeC2SSteamQrAuthMessage), typeDiscriminator: "steam/qr-auth-v1")]
public abstract class HazeC2SMessage
{
    [JsonPropertyName("id")]
    public string MessageId { get; } = HazeMessages.MakeMessageId();
}
