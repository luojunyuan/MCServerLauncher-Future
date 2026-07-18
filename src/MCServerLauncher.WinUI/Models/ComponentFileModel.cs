using CommunityToolkit.Mvvm.ComponentModel;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.InstanceConsole.Modules;

namespace MCServerLauncher.WinUI.Models;

public partial class ComponentFileModel : ObservableObject
{
    [ObservableProperty] public partial string FileName { get; set; } = string.Empty;
    [ObservableProperty] public partial string DisplayName { get; set; } = string.Empty;
    [ObservableProperty] public partial string Version { get; set; } = string.Empty;
    [ObservableProperty] public partial string VirtualPath { get; set; } = string.Empty;
    [ObservableProperty] public partial long FileSize { get; set; }
    [ObservableProperty] public partial bool IsEnabled { get; set; }
    [ObservableProperty] public partial bool IsClientSideOnly { get; set; }
    [ObservableProperty] public partial ComponentKind Kind { get; set; }

    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? FileName : DisplayName;
    public string Description => string.IsNullOrWhiteSpace(Version)
        ? $"{FileName} ({FormatSize(FileSize)})"
        : $"{FileName} | v{Version} ({FormatSize(FileSize)})";
    public string DisplayText => IsClientSideOnly ? $"{Title} [{Texts["ComponentManager_ClientSideBadge"]}]\n{Description}" : Description;
    public string ClientWarningText => IsClientSideOnly ? Texts["ComponentManager_ClientSideModsWarning"] : string.Empty;
    public string ToggleText => IsEnabled ? Texts["ComponentManager_Disable"] : Texts["ComponentManager_Enable"];

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(ClientWarningText));
        OnPropertyChanged(nameof(ToggleText));
    }

    partial void OnFileNameChanged(string value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(DisplayText));
    }
    partial void OnDisplayNameChanged(string value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(DisplayText));
    }
    partial void OnVersionChanged(string value)
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(DisplayText));
    }
    partial void OnFileSizeChanged(long value)
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(DisplayText));
    }
    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(ToggleText));
    partial void OnIsClientSideOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(ClientWarningText));
    }

    private static string FormatSize(long bytes)
    {
        var value = Math.Max(0, (double)bytes);
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value:F1} {units[index]}";
    }
}
