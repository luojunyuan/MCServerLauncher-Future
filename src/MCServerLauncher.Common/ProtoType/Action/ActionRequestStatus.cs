using System.Text.Json.Serialization;
using MCServerLauncher.Common.ProtoType.Serialization;

namespace MCServerLauncher.Common.ProtoType.Action;

[JsonConverter(typeof(SnakeCaseEnumConverter<ActionRequestStatus>))]
public enum ActionRequestStatus
{
    Ok,
    Error
}
