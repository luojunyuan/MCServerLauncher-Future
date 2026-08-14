using System.Text.Json.Serialization;
using MCServerLauncher.Common.ProtoType.Serialization;

namespace MCServerLauncher.Common.ProtoType.Action;

[JsonConverter(typeof(SnakeCaseEnumConverter<ExecutionMethod>))]
public enum ExecutionMethod
{
    Concurrent,
    Sequential,
    Select
}
