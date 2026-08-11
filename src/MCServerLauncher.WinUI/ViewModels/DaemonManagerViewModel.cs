using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MCServerLauncher.Common.ProtoType.Status;
using MCServerLauncher.DaemonClient;
using MCServerLauncher.WinUI.Core;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.Core.Storage;
using MCServerLauncher.WinUI.Models;
using MCServerLauncher.WinUI.ViewModels.Models;
using Serilog;
using Windows.System;

namespace MCServerLauncher.WinUI.ViewModels;

public partial class DaemonManagerViewModel : ObservableObject
{
    private static readonly string[] RefreshIntervalResourceKeys =
    [
        "RefreshInterval_5Seconds",
        "RefreshInterval_20Seconds",
        "RefreshInterval_30Seconds",
        "RefreshInterval_45Seconds",
        "RefreshInterval_1Minute"
    ];

    private readonly DaemonStore _store;
    private readonly SettingsStore _settings;
    private readonly IDaemonConnectionService _connections;
    private readonly INotificationService _notifications;
    private readonly ILocalizationService _localization;
    private DispatcherQueueTimer? _searchDebounceTimer;
    private long _refreshInFlight;
    private bool _attached;

    public DaemonManagerViewModel(
        DaemonStore store,
        SettingsStore settings,
        IDaemonConnectionService connections,
        INotificationService notifications,
        ILocalizationService localization)
    {
        _store = store;
        _settings = settings;
        _connections = connections;
        _notifications = notifications;
        _localization = localization;
        var storedInterval = settings.Current.Instance.AutoRefreshInterval;
        AutoRefreshEnabled = storedInterval > 0;
        RefreshIntervalSeconds = RefreshIntervalOptions.Normalize(storedInterval);
        RefreshLocalizedText();
    }

    public ObservableCollection<DaemonCardModel> Daemons { get; } = [];
    public ObservableCollection<DaemonCardModel> FilteredDaemons { get; } = [];
    public ObservableCollection<RefreshIntervalOption> RefreshIntervals { get; } = [];

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool AutoRefreshEnabled { get; set; }
    [ObservableProperty] public partial int RefreshIntervalSeconds { get; set; } = 5;
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public void Attach()
    {
        if (_attached) return;
        _localization.LanguageChanged += Localization_LanguageChanged;
        _attached = true;
    }

    public void Detach()
    {
        if (!_attached) return;
        _localization.LanguageChanged -= Localization_LanguageChanged;
        _searchDebounceTimer?.Stop();
        _attached = false;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            Daemons.Clear();
            FilteredDaemons.Clear();
            var tasks = _store.Items.Select(CreateAndLoadAsync).ToArray();
            foreach (var card in await Task.WhenAll(tasks)) Daemons.Add(card);
            ApplyFilters();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task AutoRefreshAsync()
    {
        if (Daemons.Count == 0) return;
        if (Interlocked.Exchange(ref _refreshInFlight, 1) == 1) return;
        try
        {
            await Task.WhenAll(Daemons.Select(LoadCardAsync));
            ApplyFilters();
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInFlight, 0);
        }
    }

    public async Task<string?> AddConnectionAsync(DaemonConfigModel config)
    {
        var model = CreateModel(config);
        Daemons.Add(model);
        ApplyFilters();
        if (!await LoadCardAsync(model))
        {
            Daemons.Remove(model);
            ApplyFilters();
            return model.LastErrorMessage;
        }

        _store.Add(config);
        ApplyFilters();
        return null;
    }

    public async Task<string?> EditConnectionAsync(DaemonCardModel card, DaemonConfigModel replacement)
    {
        var original = card.Config;
        var replacementModel = CreateModel(replacement);
        await _connections.RemoveAsync(original);
        if (!await LoadCardAsync(replacementModel))
        {
            await LoadCardAsync(card);
            return replacementModel.LastErrorMessage;
        }

        _store.Replace(original, replacement);
        ApplyModel(card, replacementModel);
        ApplyFilters();
        return null;
    }

    public async Task DeleteConnectionAsync(DaemonCardModel card)
    {
        try
        {
            await _connections.RemoveAsync(card.Config);
            _store.Remove(card.Config);
            Daemons.Remove(card);
            ApplyFilters();
            _notifications.Push(
                _localization.Get("Status_OK"),
                _localization.Get("DaemonDeleted"),
                NotificationSeverity.Success,
                isClosable: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Failed to delete daemon {Address}", card.Address);
            _notifications.Push(
                _localization.Get("Status_Error"),
                string.Format(_localization.Get("DaemonDeleteFailed"), ex.Message),
                NotificationSeverity.Error);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceTimer ??= App.DispatcherQueue.CreateTimer();
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(300);
        _searchDebounceTimer.IsRepeating = false;
        _searchDebounceTimer.Tick -= SearchDebounceTimer_Tick;
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        _searchDebounceTimer.Start();
    }

    private void SearchDebounceTimer_Tick(DispatcherQueueTimer sender, object args) => ApplyFilters();

    partial void OnAutoRefreshEnabledChanged(bool value)
    {
        _settings.Current.Instance.AutoRefreshInterval = value ? RefreshIntervalSeconds : 0;
        _settings.SaveAsync().FireAndForget("DaemonManagerViewModel.OnAutoRefreshEnabledChanged");
    }

    partial void OnRefreshIntervalSecondsChanged(int value)
    {
        var normalized = RefreshIntervalOptions.Normalize(value);
        if (value != normalized)
        {
            RefreshIntervalSeconds = normalized;
            return;
        }

        if (!AutoRefreshEnabled) return;
        _settings.Current.Instance.AutoRefreshInterval = normalized;
        _settings.SaveAsync().FireAndForget("DaemonManagerViewModel.OnRefreshIntervalSecondsChanged");
    }

    private DaemonCardModel CreateModel(DaemonConfigModel config) => new()
    {
        Config = config,
        FriendlyName = string.IsNullOrWhiteSpace(config.FriendlyName)
            ? _localization.Get("Main_DaemonManagerNavMenu")
            : config.FriendlyName,
        Address = $"{(config.IsSecure ? "wss" : "ws")}://{config.EndPoint}:{config.Port}",
        Status = "ing"
    };

    private async Task<DaemonCardModel> CreateAndLoadAsync(DaemonConfigModel config)
    {
        var model = CreateModel(config);
        await LoadCardAsync(model);
        return model;
    }

    private async Task<bool> LoadCardAsync(DaemonCardModel card)
    {
        card.Status = "ing";
        try
        {
            var daemon = await _connections.GetAsync(card.Config);
            if (daemon is null)
            {
                card.Status = "err";
                card.MarkResourceLoadFailed(_localization.Get("ConnectDaemonFailedSubTip"));
                return false;
            }

            var info = await daemon.GetSystemInfoAsync();
            card.SystemType = DetectSystemType(info);
            UpdateResourceUsage(card, info);
            card.LastErrorMessage = string.Empty;
            card.Status = "ok";
            return true;
        }
        catch (Exception ex)
        {
            card.Status = "err";
            card.MarkResourceLoadFailed(ex.Message);
            Log.Warning(ex, "[WinUI] Failed to load daemon card {Address}", card.Address);
            return false;
        }
    }

    private void ApplyFilters()
    {
        var search = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(search)
            ? Daemons.AsEnumerable()
            : Daemons.Where(card => MatchesSearch(card, search));
        SyncFiltered(filtered.ToList());
    }

    private void SyncFiltered(IReadOnlyList<DaemonCardModel> filtered)
    {
        for (var index = FilteredDaemons.Count - 1; index >= 0; index--)
        {
            if (!filtered.Contains(FilteredDaemons[index])) FilteredDaemons.RemoveAt(index);
        }

        for (var index = 0; index < filtered.Count; index++)
        {
            var item = filtered[index];
            var current = FilteredDaemons.IndexOf(item);
            if (current < 0) FilteredDaemons.Insert(index, item);
            else if (current != index) FilteredDaemons.Move(current, index);
        }
    }

    private void Localization_LanguageChanged(object? sender, EventArgs e) => RefreshLocalizedText();

    private void RefreshLocalizedText()
    {
        var selected = RefreshIntervalSeconds;
        RefreshIntervals.Clear();
        for (var index = 0; index < RefreshIntervalOptions.AllowedSeconds.Length; index++)
        {
            RefreshIntervals.Add(new RefreshIntervalOption(
                RefreshIntervalOptions.AllowedSeconds[index],
                _localization.Get(RefreshIntervalResourceKeys[index])));
        }
        RefreshIntervalSeconds = selected;
        foreach (var daemon in Daemons) daemon.RefreshLocalizedText();
    }

    private static bool MatchesSearch(DaemonCardModel card, string search) =>
        Contains(card.FriendlyName, search)
        || Contains(card.Address, search)
        || Contains(card.Status, search)
        || Contains(card.SystemType, search)
        || Contains(card.SystemVersion, search)
        || Contains(card.DaemonVersion, search)
        || Contains(card.Config.FriendlyName, search)
        || Contains(card.Config.EndPoint, search)
        || Contains(card.Config.Port.ToString(), search);

    private static bool Contains(string? value, string search) =>
        value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    private static string DetectSystemType(SystemInfo info)
    {
        if (info.Os.Name.Contains("Windows NT", StringComparison.OrdinalIgnoreCase)) return "Windows";
        if (info.Os.Name.Contains("Unix", StringComparison.OrdinalIgnoreCase))
            return info.Cpu.Vendor.Contains("Apple", StringComparison.OrdinalIgnoreCase) ? "Darwin" : "Linux";
        return info.Os.Name;
    }

    private void UpdateResourceUsage(DaemonCardModel model, SystemInfo info)
    {
        model.SystemVersion = $"{info.Os.Name} ({info.Os.Arch})";
        model.DaemonVersion = string.IsNullOrWhiteSpace(info.DaemonVersion)
            ? _localization.Get("Status_LoadFailed")
            : info.DaemonVersion;
        model.CpuUsage = Clamp(info.Cpu.Usage);
        model.MemoryUsage = Usage(info.Mem.Total, info.Mem.Free);

        var drives = info.Drives is { Length: > 0 } ? info.Drives : [info.Drive];
        var total = drives.Aggregate(0UL, (sum, drive) => sum + drive.Total);
        var free = drives.Aggregate(0UL, (sum, drive) => sum + drive.Free);
        model.DriveUsage = Usage(total, free);
        model.CpuUsageText = $"{model.CpuUsage:F2}% ({info.Cpu.CoreCount}C / {info.Cpu.ThreadCount}T)";
        model.MemoryUsageText = $"{model.MemoryUsage:F2}% ({FormatSize((info.Mem.Total - Math.Min(info.Mem.Total, info.Mem.Free)) * 1024d)} / {FormatSize(info.Mem.Total * 1024d)})";
        model.DriveUsageText = $"{model.DriveUsage:F2}% ({FormatSize(total - Math.Min(total, free))} / {FormatSize(total)})";
        model.DriveUsageTooltip = string.Join(Environment.NewLine, drives.Select(FormatDriveUsage));
        model.ResourceSummary = $"{_localization.Get("Daemon_CpuUsage")} {model.CpuUsage:F2}% | {_localization.Get("Daemon_MemoryUsage")} {model.MemoryUsage:F2}% | {_localization.Get("Daemon_DriveUsage")} {model.DriveUsage:F2}%";
    }

    private static void ApplyModel(DaemonCardModel target, DaemonCardModel source)
    {
        target.Config = source.Config;
        target.FriendlyName = source.FriendlyName;
        target.Address = source.Address;
        target.Status = source.Status;
        target.SystemType = source.SystemType;
        target.CpuUsage = source.CpuUsage;
        target.MemoryUsage = source.MemoryUsage;
        target.DriveUsage = source.DriveUsage;
        target.CpuUsageText = source.CpuUsageText;
        target.MemoryUsageText = source.MemoryUsageText;
        target.DriveUsageText = source.DriveUsageText;
        target.ResourceSummary = source.ResourceSummary;
        target.SystemVersion = source.SystemVersion;
        target.DaemonVersion = source.DaemonVersion;
        target.DriveUsageTooltip = source.DriveUsageTooltip;
        target.LastErrorMessage = source.LastErrorMessage;
    }

    private static double Usage(ulong total, ulong free) =>
        total == 0 ? 0 : Clamp((total - Math.Min(total, free)) * 100d / total);

    private static double Clamp(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;

    private static string FormatSize(double bytes) => Core.Format.FormatSize(bytes, "F2");

    private static string FormatDriveUsage(DriveInformation drive)
    {
        var used = drive.Total - Math.Min(drive.Total, drive.Free);
        var name = string.IsNullOrWhiteSpace(drive.Name) ? drive.DriveFormat : drive.Name;
        return $"{name} {Usage(drive.Total, drive.Free):F2}% ({FormatSize(used)} / {FormatSize(drive.Total)})";
    }
}
