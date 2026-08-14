using System.Text.Json.Serialization;
using MCServerLauncher.Common.ProtoType.Serialization;

namespace MCServerLauncher.Common.ProtoType.Instance;

[JsonConverter(typeof(SnakeCaseEnumConverter<InstanceFactoryMirror>))]
public enum InstanceFactoryMirror
{
    None,
    BmclApi
}
