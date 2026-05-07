using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace HazeCommon.Messages;

public class HazeC2SEnqueueJobMessage : HazeC2SMessage
{
    [JsonPropertyName("depot_ids")]
    public ImmutableArray<uint> DepotIds { get; init; }
}
