using System.Text.Json.Serialization;
using MCServerLauncher.Common.ProtoType.Serialization;

namespace MCServerLauncher.Common.ProtoType.Instance;

[JsonConverter(typeof(SnakeCaseEnumConverter<InstanceStatus>))]
public enum InstanceStatus
{
    Running,
    Stopped,
    Crashed
}
