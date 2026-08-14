using System.Text.Json.Serialization;
using MCServerLauncher.Common.ProtoType.EventTrigger;

namespace MCServerLauncher.Common.ProtoType.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = true)]
[JsonSerializable(typeof(EventRule))]
[JsonSerializable(typeof(List<EventRule>))]
public partial class EventRulesContext : JsonSerializerContext
{
}
