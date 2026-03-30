using System.Text.Json.Serialization;

namespace Haze.Messages;

[JsonDerivedType(typeof(HazeMessage), typeDiscriminator: "authenticate")]
public class HazeMessage
{

}
