using System.Text.Json.Serialization;

namespace MCServerLauncher.Common.DownloadProvider;

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(List<string>))]
internal partial class DownloadProviderJsonContext : JsonSerializerContext
{
}
