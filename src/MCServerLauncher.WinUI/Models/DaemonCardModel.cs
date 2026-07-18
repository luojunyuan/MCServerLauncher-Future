using CommunityToolkit.Mvvm.ComponentModel;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.Models;

public partial class DaemonCardModel : ObservableObject
{
    [ObservableProperty] public partial string FriendlyName { get; set; } = string.Empty;
    [ObservableProperty] public partial string Address { get; set; } = string.Empty;
    [ObservableProperty] public partial string Status { get; set; } = "ing";
    [ObservableProperty] public partial string SystemType { get; set; } = string.Empty;
    [ObservableProperty] public partial double CpuUsage { get; set; }
    [ObservableProperty] public partial double MemoryUsage { get; set; }
    [ObservableProperty] public partial double DriveUsage { get; set; }
    [ObservableProperty] public partial string CpuUsageText { get; set; } = string.Empty;
    [ObservableProperty] public partial string MemoryUsageText { get; set; } = string.Empty;
    [ObservableProperty] public partial string DriveUsageText { get; set; } = string.Empty;
    [ObservableProperty] public partial string ResourceSummary { get; set; } = string.Empty;
    [ObservableProperty] public partial string SystemVersion { get; set; } = string.Empty;
    [ObservableProperty] public partial string DaemonVersion { get; set; } = string.Empty;
    [ObservableProperty] public partial string DriveUsageTooltip { get; set; } = string.Empty;
    [ObservableProperty] public partial string LastErrorMessage { get; set; } = string.Empty;

    public required DaemonConfigModel Config { get; set; }
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public string StatusText => Status switch
    {
        "ok" => Texts["Status_OK"],
        "err" => Texts["Status_Error"],
        _ => Texts["Connecting"]
    };
    public bool HasError => Status == "err";

    public void MarkResourceLoadFailed(string message)
    {
        LastErrorMessage = message;
        CpuUsage = 0;
        MemoryUsage = 0;
        DriveUsage = 0;
        var failed = Texts["Status_LoadFailed"];
        CpuUsageText = failed;
        MemoryUsageText = failed;
        DriveUsageText = failed;
        ResourceSummary = failed;
        SystemVersion = failed;
        DaemonVersion = failed;
        DriveUsageTooltip = failed;
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnStatusChanged(string value) => RefreshLocalizedText();
}
