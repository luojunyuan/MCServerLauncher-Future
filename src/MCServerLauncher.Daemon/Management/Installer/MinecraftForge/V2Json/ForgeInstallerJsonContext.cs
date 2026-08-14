using System.Text.Json.Serialization;
using MCServerLauncher.Daemon.Management.Installer.MinecraftForge.Json;
using JsonVersion = MCServerLauncher.Daemon.Management.Installer.MinecraftForge.Json.Version;

namespace MCServerLauncher.Daemon.Management.Installer.MinecraftForge.V2Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(Manifest))]
[JsonSerializable(typeof(JsonVersion))]
[JsonSerializable(typeof(InstallV1))]
[JsonSerializable(typeof(ForgeInstallerV1.ProfileFile))]
internal partial class ForgeInstallerJsonContext : JsonSerializerContext
{
}
