using System.Text.Json.Serialization;

namespace HazeCommon.Messages;

[JsonDerivedType(typeof(HazeC2SAuthenticateMessage), typeDiscriminator: "authenticate-v1")]
[JsonDerivedType(typeof(HazeC2SHeartbeatMessage), typeDiscriminator: "heartbeat-v1")]
public class HazeC2SMessage
{

}
