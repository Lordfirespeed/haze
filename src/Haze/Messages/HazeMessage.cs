using System.Text.Json.Serialization;

namespace Haze.Messages;

[JsonDerivedType(typeof(HazeAuthenticateMessage), typeDiscriminator: "authenticate")]
public class HazeMessage
{

}
