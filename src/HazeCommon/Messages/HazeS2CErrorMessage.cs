using System.Text.Json.Serialization;

namespace HazeCommon.Messages;

public class HazeS2CErrorMessage : HazeS2CResponse
{
    /**
     * A string label that identifies the kind of error.
     * <see cref="ErrorCode"/> is the most stable way to identify an error.
     * It may only change between major versions of Haze.
     */
    [JsonPropertyName("code")]
    public required string ErrorCode { get; set; }

    /**
     * A human-readable label for the kind of error.
     * Can change between any versions of Haze.
     */
    [JsonPropertyName("title")]
    public string? ErrorTitle { get; set; }

    /**
     * A human-readable description of the error.
     * Can change between any versions of Haze.
     */
    [JsonPropertyName("detail")]
    public string? ErrorDetail { get; set; }
}
