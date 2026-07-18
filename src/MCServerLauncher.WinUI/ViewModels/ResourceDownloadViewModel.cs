using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downloader;
using MCServerLauncher.Common.DownloadProvider;
using MCServerLauncher.Common.Minecraft;
using MCServerLauncher.DaemonClient;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.Core.Storage;
using MCServerLauncher.WinUI.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Serilog;

namespace MCServerLauncher.WinUI.ViewModels;

public partial class ResourceDownloadViewModel : ObservableObject
{
    private readonly StoragePaths _paths;
    private readonly DaemonStore _daemons;
    private readonly IDaemonConnectionService _connections;
    private readonly ILocalizationService _localization;
    private readonly INotificationService _notifications;
    private CancellationTokenSource? _downloadCancellation;

    public ResourceDownloadViewModel(
        StoragePaths paths,
        DaemonStore daemons,
        IDaemonConnectionService connections,
        ILocalizationService localization,
        INotificationService notifications)
    {
        _paths = paths;
        _daemons = daemons;
        _connections = connections;
        _localization = localization;
        _notifications = notifications;
        foreach (var config in daemons.Items) DaemonNames.Add(config.DisplayName);
        ProviderItems.Add(localization.Get("Settings_FastMirrorName"));
        ProviderItems.Add(localization.Get("Settings_PolarsMirrorName"));
        ProviderItems.Add(localization.Get("Settings_RainYunName"));
        ProviderItems.Add(localization.Get("Settings_MSLAPIName"));
        ProviderItems.Add(localization.Get("Settings_MCSLSyncName"));
        SelectedProviderIndex = ProviderKeys.IndexOf(App.Services.Settings.Current.Download.DownloadSource);
        if (SelectedProviderIndex < 0) SelectedProviderIndex = 0;
        foreach (var item in LoadHistory())
        {
            item.RetryCommand = RetryCommand;
            History.Add(item);
        }
        _localization.LanguageChanged += Localization_LanguageChanged;
    }

    private static readonly string[] ProviderKeys = ["FastMirror", "PolarsMirror", "RainYun", "MSLAPI", "MCSLSync"];

    public ObservableCollection<string> ProviderItems { get; } = [];
    public ObservableCollection<string> DaemonNames { get; } = [];
    public ObservableCollection<ResourceCoreItem> CoreItems { get; } = [];
    public ObservableCollection<string> MinecraftVersions { get; } = [];
    public ObservableCollection<ResourceVersionItem> VersionItems { get; } = [];
    public ObservableCollection<DownloadHistoryItem> History { get; } = [];
    public IReadOnlyList<DaemonConfigModel> DaemonConfigs => _daemons.Items;

    [ObservableProperty] public partial int SelectedProviderIndex { get; set; }
    [ObservableProperty] public partial ResourceCoreItem? SelectedCore { get; set; }
    [ObservableProperty] public partial string? SelectedMinecraftVersion { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string ErrorText { get; set; } = string.Empty;

    public string ProviderKey => ProviderKeys[Math.Clamp(SelectedProviderIndex, 0, ProviderKeys.Length - 1)];
    public string Subtitle => string.Format(
        _localization.Get("ResDownloadTipPrefix") + " {0} " + _localization.Get("ResDownloadTipSuffix"),
        ProviderItems[Math.Clamp(SelectedProviderIndex, 0, ProviderItems.Count - 1)]);

    public async Task SelectProviderAsync(int index)
    {
        if (index < 0 || index >= ProviderKeys.Length) return;
        SelectedProviderIndex = index;
        App.Services.Settings.Current.Download.DownloadSource = ProviderKeys[index];
        await App.Services.Settings.SaveAsync();
        OnPropertyChanged(nameof(Subtitle));
    }

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorText = string.Empty;
        CoreItems.Clear();
        MinecraftVersions.Clear();
        VersionItems.Clear();
        SelectedCore = null;
        SelectedMinecraftVersion = null;
        try
        {
            switch (ProviderKey)
            {
                case "FastMirror":
                    foreach (var core in await FastMirror.GetCoreInfo() ?? [])
                        CoreItems.Add(new ResourceCoreItem
                        {
                            Provider = ProviderKey, Name = core.Name ?? string.Empty, ApiName = core.Name ?? string.Empty,
                            Tag = core.Tag switch
                            {
                                "proxy" => _localization.Get("DownloadModule_FastMirrorProxyType"),
                                "vanilla" => _localization.Get("DownloadModule_FastMirrorVanillaType"),
                                "pure" => _localization.Get("DownloadModule_FastMirrorPureType"),
                                "mod" => _localization.Get("DownloadModule_FastMirrorModType"),
                                "bedrock" => _localization.Get("DownloadModule_FastMirrorBedrockType"),
                                _ => core.Tag ?? string.Empty
                            },
                            Recommend = core.Recommend, HomePage = core.HomePage ?? string.Empty,
                            MinecraftVersions = Sequence(core.MinecraftVersions)
                        });
                    break;
                case "PolarsMirror":
                    foreach (var core in await PolarsMirror.GetCoreInfo() ?? [])
                        CoreItems.Add(new ResourceCoreItem
                        {
                            Provider = ProviderKey, Name = core.Name ?? string.Empty, ApiName = core.Id.ToString(), Id = core.Id,
                            Description = core.Description ?? string.Empty, HomePage = core.IconUrl ?? string.Empty
                        });
                    break;
                case "RainYun":
                    foreach (var file in await AList.GetFileList("https://mirrors.rainyun.com", "服务端合集") ?? [])
                        if (!file.IsDirectory) continue; else CoreItems.Add(new ResourceCoreItem { Provider = ProviderKey, Name = file.FileName ?? string.Empty, ApiName = file.FileName ?? string.Empty });
                    break;
                case "MSLAPI":
                    foreach (var core in await MSLAPI.GetCoreInfo() ?? [])
                        CoreItems.Add(new ResourceCoreItem { Provider = ProviderKey, ApiName = core, Name = MSLAPI.SerializeCoreName(core) });
                    break;
                case "MCSLSync":
                    foreach (var core in await MCSLSync.GetCoreInfo() ?? [])
                        CoreItems.Add(new ResourceCoreItem { Provider = ProviderKey, ApiName = core, Name = core });
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            Log.Warning(ex, "[WinUI] Resource provider {Provider} failed to refresh", ProviderKey);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectCoreAsync(ResourceCoreItem? core)
    {
        if (core is null || IsBusy) return;
        SelectedCore = core;
        MinecraftVersions.Clear();
        VersionItems.Clear();
        SelectedMinecraftVersion = null;
        try
        {
            if (core.Provider is "FastMirror")
            {
                foreach (var version in core.MinecraftVersions) MinecraftVersions.Add(version);
                return;
            }
            if (core.Provider is "PolarsMirror")
            {
                foreach (var detail in await PolarsMirror.GetCoreDetail(core.Id) ?? [])
                    VersionItems.Add(new ResourceVersionItem
                    {
                        Provider = core.Provider, Core = core.ApiName, FileName = detail.FileName ?? string.Empty,
                        DownloadUrl = detail.DownloadUrl ?? string.Empty
                    });
                return;
            }
            foreach (var version in Sequence(core.Provider switch
            {
                "MCSLSync" => await MCSLSync.GetMinecraftVersions(core.ApiName),
                "MSLAPI" => await MSLAPI.GetMinecraftVersions(core.ApiName),
                _ => (await AList.GetFileList("https://mirrors.rainyun.com", $"服务端合集/{core.ApiName}") ?? [])
                    .Where(file => !file.IsDirectory && !string.IsNullOrWhiteSpace(file.FileName))
                    .Select(file => file.FileName!)
                    .ToList()
            })) MinecraftVersions.Add(version);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            Log.Warning(ex, "[WinUI] Resource provider {Provider} core {Core} failed", core.Provider, core.Name);
        }
    }

    public async Task SelectVersionAsync(string? version)
    {
        if (string.IsNullOrWhiteSpace(version) || SelectedCore is null || IsBusy) return;
        SelectedMinecraftVersion = version;
        VersionItems.Clear();
        try
        {
            switch (SelectedCore.Provider)
            {
                case "FastMirror":
                    foreach (var detail in await FastMirror.GetCoreDetail(SelectedCore.ApiName, version) ?? [])
                        VersionItems.Add(new ResourceVersionItem
                        {
                            Provider = "FastMirror", Core = SelectedCore.ApiName, MinecraftVersion = version,
                            BuildVersion = detail.CoreVersion ?? string.Empty,
                            FileName = $"{SelectedCore.ApiName}-{version}-{detail.CoreVersion}.jar",
                            DownloadUrl = FastMirror.CombineDownloadUrl(SelectedCore.ApiName, version, detail.CoreVersion ?? string.Empty)
                        });
                    break;
                case "MCSLSync":
                    foreach (var build in await MCSLSync.GetCoreVersions(SelectedCore.ApiName, version) ?? [])
                        VersionItems.Add(new ResourceVersionItem
                        {
                            Provider = "MCSLSync", Core = SelectedCore.ApiName, MinecraftVersion = version,
                            BuildVersion = build, FileName = $"{SelectedCore.ApiName}-{version}-{build}.jar"
                        });
                    break;
                case "MSLAPI":
                    VersionItems.Add(new ResourceVersionItem
                    {
                        Provider = "MSLAPI", Core = SelectedCore.ApiName, MinecraftVersion = version,
                        BuildVersion = version, FileName = $"{SelectedCore.ApiName}-{version}.jar"
                    });
                    break;
                case "RainYun":
                    VersionItems.Add(new ResourceVersionItem
                    {
                        Provider = "RainYun", Core = SelectedCore.ApiName, MinecraftVersion = version,
                        BuildVersion = version, FileName = version
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            Log.Warning(ex, "[WinUI] Resource provider {Provider} version {Version} failed", SelectedCore.Provider, version);
        }
    }

    public async Task DownloadItemAsync(ResourceVersionItem? item, XamlRoot root)
    {
        if (item is null || IsBusy) return;
        var url = await ResolveDownloadUrlAsync(item);
        if (string.IsNullOrWhiteSpace(url))
        {
            _notifications.Push(
                _localization.Get("DownloadFailed"),
                $"{item.FileName} {_localization.Get("DownloadFailed")}",
                NotificationSeverity.Error);
            return;
        }
        var selected = await ShowDestinationDialogAsync(root, item.FileName);
        if (selected is null) return;
        IsBusy = true;
        _downloadCancellation = new CancellationTokenSource();
        string? localPath = null;
        string? tempPath = null;
        try
        {
            if (selected.SaveLocal)
            {
                var file = await App.Services.Files.PickSaveFileAsync(App.WindowHandle, item.FileName);
                if (file is null && selected.Daemons.Count == 0) return;
                localPath = file?.Path;
            }
            if (selected.Daemons.Count > 0)
            {
                tempPath = Path.Combine(Path.GetTempPath(), $"mcsl-res-{Guid.NewGuid():N}-{Path.GetFileName(item.FileName)}");
            }
            var outputPath = localPath ?? tempPath;
            if (outputPath is null) return;
            await DownloadUrlToFileAsync(url, outputPath, _downloadCancellation.Token);
            if (localPath is not null)
            {
                AddHistory(item, url, localPath, new FileInfo(localPath).Length);
            }
            foreach (var config in selected.Daemons)
            {
                try
                {
                    var daemon = await _connections.GetAsync(config, _downloadCancellation.Token);
                    if (daemon is null) continue;
                    var upload = await daemon.UploadFileAsync(outputPath, $"caches/downloads/{Path.GetFileName(item.FileName)}", 1024 * 1024, ct: _downloadCancellation.Token);
                    if (upload.NetworkLoadTask is not null) await upload.NetworkLoadTask;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[WinUI] Failed to push resource {File} to daemon {Daemon}", item.FileName, config.DisplayName);
                }
            }
            _notifications.Push(
                _localization.Get("DownloadFinished"),
                $"{item.FileName} {_localization.Get("DownloadFinished")}",
                NotificationSeverity.Success);
        }
        catch (OperationCanceledException)
        {
            StatusText = _localization.Get("DownloadCancelled");
            _notifications.Push(
                _localization.Get("DownloadCancelled"),
                $"{item.FileName} {_localization.Get("DownloadCancelled")}",
                NotificationSeverity.Warning);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            _notifications.Push(
                _localization.Get("DownloadFailed"),
                $"{item.FileName} {_localization.Get("DownloadFailed")}\n{ex.Message}",
                NotificationSeverity.Error);
        }
        finally
        {
            if (tempPath is not null) try { File.Delete(tempPath); } catch { }
            _downloadCancellation?.Dispose();
            _downloadCancellation = null;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _downloadCancellation?.Cancel();

    [RelayCommand]
    private async Task RetryAsync(DownloadHistoryItem? item)
    {
        if (item is null) return;
        var version = new ResourceVersionItem { FileName = item.FileName, DownloadUrl = item.Url, Provider = "direct" };
        await DownloadItemAsync(version, App.Window.RootPage.XamlRoot);
    }

    public void ReloadHistory()
    {
        History.Clear();
        foreach (var item in LoadHistory())
        {
            item.RetryCommand = RetryCommand;
            History.Add(item);
        }
    }

    private async Task<string?> ResolveDownloadUrlAsync(ResourceVersionItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.DownloadUrl)) return item.DownloadUrl;
        return item.Provider switch
        {
            "RainYun" => await AList.GetFileUrl("https://mirrors.rainyun.com", $"服务端合集/{item.Core}/{item.FileName}"),
            "MSLAPI" => await MSLAPI.GetDownloadUrl(item.Core, item.MinecraftVersion),
            "MCSLSync" => (await MCSLSync.GetCoreDetail(item.Core, item.MinecraftVersion, item.BuildVersion))?.DownloadUrl,
            _ => null
        };
    }

    private async Task DownloadUrlToFileAsync(string url, string path, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var threadCount = Math.Clamp(App.Services.Settings.Current.Download.ThreadCnt, 1, 256);
        var configuration = new DownloadConfiguration
        {
            Timeout = 5000,
            ChunkCount = threadCount,
            ParallelCount = threadCount,
            ParallelDownload = threadCount > 1,
            RequestConfiguration =
            {
                UserAgent = MCServerLauncher.Common.Network.HttpHelper.UserAgent
            }
        };
        using var download = new DownloadService(configuration);
        download.DownloadProgressChanged += (_, args) =>
        {
            var progress = args.ProgressPercentage;
            var status = args.TotalBytesToReceive > 0
                ? $"{FormatSize(args.ReceivedBytesSize)} / {FormatSize(args.TotalBytesToReceive)}"
                : FormatSize(args.ReceivedBytesSize);
            App.DispatcherQueue.TryEnqueue(() =>
            {
                Progress = progress;
                StatusText = status;
            });
        };
        try
        {
            await download.DownloadFileTaskAsync(url, path, token);
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            try { File.Delete(path); } catch { }
            throw;
        }
    }

    private async Task<DestinationChoice?> ShowDestinationDialogAsync(XamlRoot root, string fileName)
    {
        var local = new CheckBox { Content = _localization.Get("FirstSetup_DaemonLocalHost"), IsChecked = true };
        var panel = new StackPanel { Spacing = 6, MinWidth = 420 };
        panel.Children.Add(new TextBlock { Text = $"{_localization.Get("Download")}: {fileName}", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(local);
        var daemonChecks = new List<(CheckBox Box, DaemonConfigModel Config)>();
        foreach (var config in DaemonConfigs)
        {
            var check = new CheckBox { Content = config.DisplayName };
            daemonChecks.Add((check, config));
            panel.Children.Add(check);
        }
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = _localization.Get("Select"),
            Content = panel,
            PrimaryButtonText = _localization.Get("Continue"),
            CloseButtonText = _localization.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        var daemons = daemonChecks.Where(item => item.Box.IsChecked == true).Select(item => item.Config).ToArray();
        if (local.IsChecked != true && daemons.Length == 0) return null;
        return new DestinationChoice(local.IsChecked == true, daemons);
    }

    private void AddHistory(ResourceVersionItem item, string url, string localPath, long size)
    {
        var history = new DownloadHistoryItem
        {
            FileName = item.FileName,
            Url = url,
            LocalPath = localPath,
            Size = size,
            CompletedAt = DateTimeOffset.UtcNow,
            Status = "completed",
            RetryCommand = RetryCommand
        };
        History.Insert(0, history);
        while (History.Count > 50) History.RemoveAt(History.Count - 1);
        try
        {
            Directory.CreateDirectory(_paths.ConfigurationRoot);
            File.WriteAllText(Path.Combine(_paths.ConfigurationRoot, "DownloadHistory.json"), JsonSerializer.Serialize(History, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Log.Warning(ex, "[WinUI] Failed to save download history"); }
    }

    private IEnumerable<DownloadHistoryItem> LoadHistory()
    {
        var path = Path.Combine(_paths.ConfigurationRoot, "DownloadHistory.json");
        try { return File.Exists(path) ? JsonSerializer.Deserialize<List<DownloadHistoryItem>>(File.ReadAllText(path)) ?? [] : []; }
        catch { return []; }
    }

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        var selected = ProviderKey;
        ProviderItems.Clear();
        foreach (var key in new[] { "Settings_FastMirrorName", "Settings_PolarsMirrorName", "Settings_RainYunName", "Settings_MSLAPIName", "Settings_MCSLSyncName" }) ProviderItems.Add(_localization.Get(key));
        SelectedProviderIndex = Math.Max(0, Array.IndexOf(ProviderKeys, selected));
        OnPropertyChanged(nameof(Subtitle));
    }

    private static List<string> Sequence(IEnumerable<string?>? versions) =>
        McVersionSequencer.Sequence((versions ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList())
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList();

    private static string FormatSize(long bytes)
    {
        var value = Math.Max(0, (double)bytes);
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value:F1} {units[index]}";
    }

    private sealed record DestinationChoice(bool SaveLocal, IReadOnlyList<DaemonConfigModel> Daemons);
}
