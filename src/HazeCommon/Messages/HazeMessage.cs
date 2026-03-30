using System.Text.Json.Serialization;

namespace HazeCommon.Messages;

[JsonDerivedType(typeof(HazeAuthenticateMessage), typeDiscriminator: "authenticate")]
public class HazeMessage
{

}
