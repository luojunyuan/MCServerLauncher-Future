using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.DaemonClient;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.Core.Storage;
using MCServerLauncher.WinUI.InstanceConsole;
using MCServerLauncher.WinUI.Models;
using MCServerLauncher.WinUI.ViewModels.Models;
using Serilog;

namespace MCServerLauncher.WinUI.ViewModels;

public partial class InstanceManagerViewModel : ObservableObject
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
    private bool _attached;

    public InstanceManagerViewModel(
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

    public ObservableCollection<InstanceCardModel> AllInstances { get; } = [];
    public ObservableCollection<InstanceCardModel> FilteredInstances { get; } = [];
    public ObservableCollection<string> DaemonFilterItems { get; } = [];
    public ObservableCollection<RefreshIntervalOption> RefreshIntervals { get; } = [];

    [ObservableProperty] public partial int SelectedDaemonIndex { get; set; }
    [ObservableProperty] public partial string SelectedStatusFilter { get; set; } = "All";
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string? ErrorState { get; set; }
    [ObservableProperty] public partial bool AutoRefreshEnabled { get; set; }
    [ObservableProperty] public partial int RefreshIntervalSeconds { get; set; } = 5;

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
        _attached = false;
    }

    public void LoadDaemonFilterItems()
    {
        DaemonFilterItems.Clear();
        foreach (var config in _store.Items) DaemonFilterItems.Add(config.DisplayName);
        SelectedDaemonIndex = 0;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        AllInstances.Clear();
        FilteredInstances.Clear();
        ErrorState = null;
        try
        {
            await LoadDaemonInstancesAsync(isAutoRefresh: false);
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AutoRefreshAsync()
    {
        await LoadDaemonInstancesAsync(isAutoRefresh: true);
        ApplyFilters();
    }

    public async Task StartInstanceAsync(InstanceCardModel card)
    {
        if (!card.CanStart)
        {
            PushUnavailable("InstanceCard_StartUnavailable", card);
            return;
        }
        await RunActionAsync(
            card,
            (daemon, id) => daemon.StartInstanceAsync(id),
            "InstanceCard_StartingInstance",
            "InstanceCard_StartFailed",
            NotificationSeverity.Success);
    }

    public async Task StopInstanceAsync(InstanceCardModel card)
    {
        if (!card.CanStop)
        {
            PushUnavailable("InstanceCard_StopUnavailable", card);
            return;
        }
        await RunActionAsync(
            card,
            (daemon, id) => daemon.StopInstanceAsync(id),
            "InstanceCard_StoppingInstance",
            "InstanceCard_StopFailed",
            NotificationSeverity.Success);
    }

    public async Task RestartInstanceAsync(InstanceCardModel card)
    {
        if (!card.CanRestart)
        {
            PushUnavailable("InstanceCard_RestartUnavailable", card);
            return;
        }
        await RunActionAsync(
            card,
            (daemon, id) => daemon.RestartInstanceAsync(id),
            "InstanceCard_RestartingInstance",
            "InstanceCard_RestartFailed",
            NotificationSeverity.Success);
    }

    public async Task KillInstanceAsync(InstanceCardModel card)
    {
        if (!card.CanKill)
        {
            PushUnavailable("InstanceCard_KillUnavailable", card);
            return;
        }
        await RunActionAsync(
            card,
            (daemon, id) => daemon.KillInstanceAsync(id),
            "InstanceCard_KillingInstance",
            "InstanceCard_KillFailed",
            NotificationSeverity.Warning);
    }

    public async Task DeleteInstanceAsync(InstanceCardModel card)
    {
        if (!card.CanDelete)
        {
            PushUnavailable("InstanceCard_DeleteUnavailable", card);
            return;
        }

        try
        {
            var daemon = await _connections.GetAsync(card.DaemonConfig);
            if (daemon is null)
            {
                _notifications.Push(
                    _localization.Get("Status_Error"),
                    _localization.Get("ConnectDaemonFailedTip"),
                    NotificationSeverity.Error);
                return;
            }
            await daemon.RemoveInstanceAsync(card.InstanceId);
            AllInstances.Remove(card);
            ApplyFilters();
            _notifications.Push(
                _localization.Get("Status_OK"),
                string.Format(_localization.Get("InstanceCard_DeletedInstance"), card.InstanceName),
                NotificationSeverity.Success,
                isClosable: false);
            await AutoRefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Failed to delete instance {InstanceId}", card.InstanceId);
            _notifications.Push(
                _localization.Get("Status_Error"),
                string.Format(_localization.Get("InstanceCard_DeleteFailed"), ex.Message),
                NotificationSeverity.Error);
            await AutoRefreshAsync();
        }
    }

    public void OpenConsole(InstanceCardModel card)
    {
        var window = new InstanceConsoleWindow(card.DaemonConfig, card.InstanceId);
        App.RegisterSecondaryWindow(window);
        window.Activate();
    }

    public void ApplyFilters()
    {
        IEnumerable<InstanceCardModel> filtered = AllInstances;
        filtered = SelectedStatusFilter switch
        {
            "Running" => filtered.Where(card => card.Status == InstanceStatus.Running),
            "Stopped" => filtered.Where(card => card.Status == InstanceStatus.Stopped),
            "Crashed" => filtered.Where(card => card.Status == InstanceStatus.Crashed),
            _ => filtered
        };

        var search = SearchText.Trim();
        if (!string.IsNullOrWhiteSpace(search)) filtered = filtered.Where(card => MatchesSearch(card, search));
        SyncFiltered(filtered.ToList());
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedStatusFilterChanged(string value) => ApplyFilters();

    partial void OnAutoRefreshEnabledChanged(bool value)
    {
        _settings.Current.Instance.AutoRefreshInterval = value ? RefreshIntervalSeconds : 0;
        _ = _settings.SaveAsync();
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
        _ = _settings.SaveAsync();
    }

    private async Task LoadDaemonInstancesAsync(bool isAutoRefresh)
    {
        var configs = _store.Items;
        if (configs.Count == 0)
        {
            if (!isAutoRefresh) ErrorState = "no_daemon";
            return;
        }

        if (SelectedDaemonIndex < 0 || SelectedDaemonIndex >= configs.Count) return;
        var config = configs[SelectedDaemonIndex];
        try
        {
            var daemon = await _connections.GetAsync(config)
                ?? throw new InvalidOperationException(_localization.Get("ConnectDaemonFailedTip"));
            var memoryTotal = await GetMemoryTotalAsync(daemon, config);
            var reports = await daemon.GetAllReportsAsync();
            if (reports.Count == 0)
            {
                if (isAutoRefresh)
                {
                    AllInstances.Clear();
                    FilteredInstances.Clear();
                }
                else
                {
                    ErrorState = "no_instance";
                }
                return;
            }

            if (isAutoRefresh) UpdateExistingCards(reports, config, memoryTotal);
            else foreach (var pair in reports) AllInstances.Add(CreateCard(pair.Key, pair.Value, config, memoryTotal));
        }
        catch (Exception ex)
        {
            if (!isAutoRefresh) ErrorState = "load_error";
            Log.Warning(ex, "[WinUI] Failed to load instances for {Daemon}", config.DisplayName);
        }
    }

    private void UpdateExistingCards(
        IReadOnlyDictionary<Guid, InstanceReport> reports,
        DaemonConfigModel config,
        ulong? memoryTotal)
    {
        foreach (var card in AllInstances.Where(card => !reports.ContainsKey(card.InstanceId)).ToArray())
            AllInstances.Remove(card);

        foreach (var pair in reports)
        {
            var existing = AllInstances.FirstOrDefault(card => card.InstanceId == pair.Key);
            if (existing is null)
            {
                AllInstances.Add(CreateCard(pair.Key, pair.Value, config, memoryTotal));
                continue;
            }

            existing.Status = pair.Value.Status;
            existing.CpuUsage = pair.Value.PerformanceCounter.Cpu;
            existing.MemoryUsage = pair.Value.PerformanceCounter.Memory;
            existing.MemoryTotalBytes = memoryTotal;
        }
    }

    private InstanceCardModel CreateCard(
        Guid id,
        InstanceReport report,
        DaemonConfigModel config,
        ulong? memoryTotal) => new()
    {
        InstanceId = id,
        InstanceName = report.Config.Name,
        InstanceType = report.Config.InstanceType.ToString(),
        Version = report.Config.Version ?? string.Empty,
        Status = report.Status,
        CpuUsage = report.PerformanceCounter.Cpu,
        MemoryUsage = report.PerformanceCounter.Memory,
        MemoryTotalBytes = memoryTotal,
        DaemonConfig = config
    };

    private async Task RunActionAsync(
        InstanceCardModel card,
        Func<IDaemon, Guid, Task> action,
        string successKey,
        string failureKey,
        NotificationSeverity severity)
    {
        try
        {
            var daemon = await _connections.GetAsync(card.DaemonConfig);
            if (daemon is null)
            {
                _notifications.Push(
                    _localization.Get("Status_Error"),
                    _localization.Get("ConnectDaemonFailedTip"),
                    NotificationSeverity.Error);
                return;
            }
            await action(daemon, card.InstanceId);
            _notifications.Push(
                severity == NotificationSeverity.Warning
                    ? _localization.Get("Warning")
                    : _localization.Get("Status_OK"),
                string.Format(_localization.Get(successKey), card.InstanceName),
                severity,
                isClosable: false);
            await AutoRefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Instance action failed for {InstanceId}", card.InstanceId);
            _notifications.Push(
                _localization.Get("Status_Error"),
                string.Format(_localization.Get(failureKey), ex.Message),
                NotificationSeverity.Error);
            await AutoRefreshAsync();
        }
    }

    private void PushUnavailable(string key, InstanceCardModel card) =>
        _notifications.Push(
            _localization.Get("Warning"),
            string.Format(_localization.Get(key), card.InstanceName),
            NotificationSeverity.Warning,
            isClosable: false);

    private static async Task<ulong?> GetMemoryTotalAsync(IDaemon daemon, DaemonConfigModel config)
    {
        try
        {
            var info = await daemon.GetSystemInfoAsync();
            return info.Mem.Total * 1024UL;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Failed to load memory total for {Daemon}", config.DisplayName);
            return null;
        }
    }

    private void SyncFiltered(IReadOnlyList<InstanceCardModel> filtered)
    {
        for (var index = FilteredInstances.Count - 1; index >= 0; index--)
        {
            if (!filtered.Contains(FilteredInstances[index])) FilteredInstances.RemoveAt(index);
        }
        for (var index = 0; index < filtered.Count; index++)
        {
            var item = filtered[index];
            var current = FilteredInstances.IndexOf(item);
            if (current < 0) FilteredInstances.Insert(index, item);
            else if (current != index) FilteredInstances.Move(current, index);
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
        foreach (var card in AllInstances) card.RefreshLocalizedText();
    }

    private static bool MatchesSearch(InstanceCardModel card, string search) =>
        Contains(card.InstanceName, search)
        || Contains(card.InstanceType, search)
        || Contains(card.Version, search)
        || Contains(card.StatusText, search)
        || Contains(card.Status.ToString(), search)
        || Contains(card.InstanceId.ToString(), search)
        || Contains(card.DaemonConfig.FriendlyName, search)
        || Contains(card.DaemonConfig.EndPoint, search);

    private static bool Contains(string? value, string search) =>
        value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
}
