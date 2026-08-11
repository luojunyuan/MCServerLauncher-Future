using CommunityToolkit.Mvvm.ComponentModel;
using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.Models;

public partial class InstanceCardModel : ObservableObject
{
    [ObservableProperty] public partial Guid InstanceId { get; set; }
    [ObservableProperty] public partial string InstanceName { get; set; } = string.Empty;
    [ObservableProperty] public partial string InstanceType { get; set; } = string.Empty;
    [ObservableProperty] public partial string Version { get; set; } = string.Empty;
    [ObservableProperty] public partial InstanceStatus Status { get; set; }
    [ObservableProperty] public partial double CpuUsage { get; set; }
    [ObservableProperty] public partial long MemoryUsage { get; set; }
    [ObservableProperty] public partial ulong? MemoryTotalBytes { get; set; }

    public required DaemonConfigModel DaemonConfig { get; init; }
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public double CpuUsageProgress => Math.Clamp(double.IsFinite(CpuUsage) ? CpuUsage : 0, 0, 100);
    public double MemoryUsageProgress => MemoryTotalBytes is not > 0
        ? 0
        : Math.Clamp(MemoryUsage / (double)MemoryTotalBytes.Value * 100, 0, 100);
    public string StatusText => Status switch
    {
        InstanceStatus.Running => Texts["Running"],
        InstanceStatus.Stopped => Texts["Stopped"],
        InstanceStatus.Crashed => Texts["Crashed"],
        _ => Status.ToString()
    };
    public string CpuUsageText => $"{CpuUsageProgress:F2}%";
    public string MemoryUsageText => MemoryTotalBytes is null
        ? Texts["Status_LoadFailed"]
        : MemoryTotalBytes == 0
            ? FormatSize(MemoryUsage)
            : $"{MemoryUsageProgress:F2}% ({FormatSize(MemoryUsage)} / {FormatSize(MemoryTotalBytes.Value)})";
    public bool IsActive => Status == InstanceStatus.Running;
    public bool CanStart => Status is InstanceStatus.Stopped or InstanceStatus.Crashed;
    public bool CanStop => Status == InstanceStatus.Running;
    public bool CanRestart => Status == InstanceStatus.Running;
    public bool CanKill => Status == InstanceStatus.Running;
    public bool CanDelete => Status == InstanceStatus.Stopped;

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(MemoryUsageText));
    }

    partial void OnStatusChanged(InstanceStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRestart));
        OnPropertyChanged(nameof(CanKill));
        OnPropertyChanged(nameof(CanDelete));
    }

    partial void OnCpuUsageChanged(double value)
    {
        OnPropertyChanged(nameof(CpuUsageProgress));
        OnPropertyChanged(nameof(CpuUsageText));
    }

    partial void OnMemoryUsageChanged(long value)
    {
        OnPropertyChanged(nameof(MemoryUsageProgress));
        OnPropertyChanged(nameof(MemoryUsageText));
    }

    partial void OnMemoryTotalBytesChanged(ulong? value)
    {
        OnPropertyChanged(nameof(MemoryUsageProgress));
        OnPropertyChanged(nameof(MemoryUsageText));
    }

    private static string FormatSize(double bytes) => Core.Format.FormatSize(bytes, "F2");
}
