using System.Text.Json.Serialization;

namespace HazeCommon.Messages;

public abstract class HazeS2CResponse : HazeS2CMessage
{
    [JsonPropertyName("re_id")]
    public required string RegardingMessageId { get; set; }
}
