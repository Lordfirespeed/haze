using System.Text.Json.Serialization;

namespace HazeCommon.Messages;

[JsonDerivedType(typeof(HazeS2CSessionCreatedMessage), typeDiscriminator: "session-created-v1")]
[JsonDerivedType(typeof(HazeS2CWhoAmIResponse), typeDiscriminator: "whoami-v1-response")]
public abstract class HazeS2CMessage
{
    [JsonPropertyName("id")]
    public string MessageId { get; } = HazeMessages.MakeMessageId();
}
