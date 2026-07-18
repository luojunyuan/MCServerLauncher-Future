using System.ComponentModel;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.Models;

public sealed class ResourceCoreItem
{
    public string Provider { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ApiName { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string HomePage { get; init; } = string.Empty;
    public int Id { get; init; }
    public bool Recommend { get; init; }
    public IReadOnlyList<string> MinecraftVersions { get; init; } = [];
}

public sealed class ResourceVersionItem
{
    public string Provider { get; init; } = string.Empty;
    public string Core { get; init; } = string.Empty;
    public string MinecraftVersion { get; init; } = string.Empty;
    public string BuildVersion { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string FileSize { get; init; } = string.Empty;
    public LocalizedStrings Texts => App.Services.Localization.Texts;
}
