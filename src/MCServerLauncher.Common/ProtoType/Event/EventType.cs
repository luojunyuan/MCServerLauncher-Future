using System.Text.Json.Serialization;
using MCServerLauncher.Common.ProtoType.Serialization;

namespace MCServerLauncher.Common.ProtoType.Event;

[JsonConverter(typeof(SnakeCaseEnumConverter<EventType>))]
public enum EventType
{
    InstanceLog,
    DaemonReport
}
