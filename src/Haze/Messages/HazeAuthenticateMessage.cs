using System.Text.Json.Serialization;

namespace Haze.Messages;

public class HazeAuthenticateMessage : HazeMessage
{
    public string ClientId { get; set; }

    public string ClientSecret { get; set; }
}
