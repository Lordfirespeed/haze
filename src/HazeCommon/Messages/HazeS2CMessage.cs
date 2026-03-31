using System.Text.Json.Serialization;

namespace HazeCommon.Messages;

[JsonDerivedType(typeof(HazeS2CSessionCreatedMessage), typeDiscriminator: "session-created-v1")]
public class HazeS2CMessage
{

}
