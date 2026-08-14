using System.Text.Json.Serialization;

namespace MCServerLauncher.Daemon.Management.Installer.MinecraftForge.Json;

public class Manifest
{
    [JsonPropertyName("versions")] public List<Info>? Versions { get; set; }

    public string GetUrl(string version)
    {
        // return versions == null ? null : versions.stream().filter(v -> version.equals(v.getId())).map(Info::getUrl).findFirst().orElse(null);
        return Versions?.FirstOrDefault(v => v.Id == version)?.Url ?? string.Empty;
    }

    public record Info(string Id, string Url);
}
