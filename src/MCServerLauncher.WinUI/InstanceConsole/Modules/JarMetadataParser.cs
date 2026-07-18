using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;

namespace MCServerLauncher.WinUI.InstanceConsole.Modules;

public sealed record JarMetadata(string DisplayName, string Version, bool IsClientSideOnly = false);

public static class JarMetadataParser
{
    public static JarMetadata? Parse(string jarPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(jarPath);
            var json = archive.GetEntry("fabric.mod.json") ?? archive.GetEntry("velocity-plugin.json");
            if (json is not null) return ParseJson(json, false);
            var quilt = archive.GetEntry("quilt.mod.json");
            if (quilt is not null) return ParseJson(quilt, true);
            var toml = archive.GetEntry("META-INF/neoforge.mods.toml") ?? archive.GetEntry("META-INF/mods.toml");
            if (toml is not null) return ParseToml(toml);
            var plugin = archive.GetEntry("plugin.yml") ?? archive.GetEntry("paper-plugin.yml") ?? archive.GetEntry("bungee.yml");
            return plugin is null ? null : ParseYaml(plugin);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[WinUI] Failed to parse JAR metadata {JarPath}", jarPath);
            return null;
        }
    }

    public static bool IsClientSideMod(string jarPath)
    {
        var metadata = Parse(jarPath);
        return metadata?.IsClientSideOnly == true;
    }

    private static JarMetadata? ParseJson(ZipArchiveEntry entry, bool quilt)
    {
        using var stream = entry.Open();
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var source = root;
        if (quilt && root.TryGetProperty("quilt_loader", out var loader)) source = loader;
        var name = GetString(source, "name") ?? GetString(source, "id") ?? string.Empty;
        var version = GetString(source, "version") ?? string.Empty;
        if (quilt && source.TryGetProperty("metadata", out var metadata))
            name = GetString(metadata, "name") ?? name;
        var environment = GetString(source, "environment");
        if (quilt && source.TryGetProperty("metadata", out var meta)) environment ??= GetString(meta, "environment");
        return new JarMetadata(name, version, string.Equals(environment, "client", StringComparison.OrdinalIgnoreCase));
    }

    private static JarMetadata? ParseToml(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        var content = reader.ReadToEnd();
        var block = Regex.Match(content, @"\[\[mods\]\](?<body>[\s\S]*?)(?=\[\[|\z)", RegexOptions.Multiline).Groups["body"].Value;
        if (string.IsNullOrWhiteSpace(block)) block = content;
        var name = Extract(block, "displayName") ?? Extract(block, "modId") ?? string.Empty;
        var version = Extract(block, "version") ?? string.Empty;
        var side = Regex.Match(content, @"^\s*side\s*=\s*[""'](?<side>[^""']+)", RegexOptions.Multiline | RegexOptions.IgnoreCase).Groups["side"].Value;
        return new JarMetadata(name, version, string.Equals(side, "CLIENT", StringComparison.OrdinalIgnoreCase));
    }

    private static JarMetadata? ParseYaml(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        var content = reader.ReadToEnd();
        return new JarMetadata(ExtractYaml(content, "name") ?? string.Empty, ExtractYaml(content, "version") ?? string.Empty);
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Extract(string body, string key)
    {
        var match = Regex.Match(body, @"^\s*" + Regex.Escape(key) + @"\s*=\s*""(?<value>[^""]*)""", RegexOptions.Multiline);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? ExtractYaml(string body, string key)
    {
        var match = Regex.Match(body, @"^" + Regex.Escape(key) + @"\s*:\s*(?<value>.+?)\s*$", RegexOptions.Multiline);
        if (!match.Success) return null;
        var value = match.Groups["value"].Value.Trim();
        return value.Length >= 2 && ((value[0] == '\'' && value[^1] == '\'') || (value[0] == '"' && value[^1] == '"'))
            ? value[1..^1]
            : value;
    }
}
