using CommunityToolkit.Mvvm.ComponentModel;

namespace MCServerLauncher.WinUI.Models;

public partial class RemoteFileModel : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string VirtualPath { get; set; } = "/";
    [ObservableProperty] public partial bool IsDirectory { get; set; }
    [ObservableProperty] public partial long SizeBytes { get; set; }
    [ObservableProperty] public partial long ModifiedTime { get; set; }

    public string IconGlyph => IsDirectory ? "\uE8B7" : "\uE8A5";
    public string TypeText => App.Services.Localization.Texts["File"];
    public string SizeText => IsDirectory ? string.Empty : FormatSize(SizeBytes);
    public string ModifiedText => ModifiedTime <= 0
        ? string.Empty
        : DateTimeOffset.FromUnixTimeSeconds(ModifiedTime).ToLocalTime().ToString("yyyy/MM/dd HH:mm");

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(TypeText));
    }

    partial void OnIsDirectoryChanged(bool value)
    {
        OnPropertyChanged(nameof(IconGlyph));
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(SizeText));
    }

    partial void OnSizeBytesChanged(long value) => OnPropertyChanged(nameof(SizeText));
    partial void OnModifiedTimeChanged(long value) => OnPropertyChanged(nameof(ModifiedText));

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
        return $"{value:0.##} {units[index]}";
    }
}
