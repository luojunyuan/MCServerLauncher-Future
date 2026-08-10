using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Storage;
using MCServerLauncher.WinUI.ViewModels.Models;

namespace MCServerLauncher.WinUI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly string[] DownloadSourceKeys =
        ["FastMirror", "PolarsMirror", "RainYun", "MSLAPI", "MCSLSync"];

    private static readonly string[] DownloadSourceResourceKeys =
    [
        "Settings_FastMirrorName",
        "Settings_PolarsMirrorName",
        "Settings_RainYunName",
        "Settings_MSLAPIName",
        "Settings_MCSLSyncName"
    ];

    private static readonly string[] DownloadErrorKeys = ["stop", "retry1", "retry3"];
    private static readonly string[] DownloadErrorResourceKeys =
    [
        "Settings_ActionWhenDownloadError_Stop",
        "Settings_ActionWhenDownloadError_Retry1",
        "Settings_ActionWhenDownloadError_Retry3"
    ];

    private static readonly string[] DoubleClickKeys = ["Console", "Start", "Stop", "Restart", "Kill"];
    private static readonly string[] DoubleClickResourceKeys =
    [
        "Settings_Instance_ActionOnDoubleClick_Console",
        "Settings_Instance_ActionOnDoubleClick_Start",
        "Settings_Instance_ActionOnDoubleClick_Stop",
        "Settings_Instance_ActionOnDoubleClick_Restart",
        "Settings_Instance_ActionOnDoubleClick_Kill"
    ];

    private static readonly string[] ThemeKeys = ["auto", "light", "dark"];
    private static readonly string[] ThemeResourceKeys =
        ["Settings_AppTheme_Auto", "Settings_AppTheme_Light", "Settings_AppTheme_Dark"];

    private readonly SettingsStore _settings;
    private readonly ILocalizationService _localization;
    private bool _initializing = true;
    private bool _attached;

    public SettingsViewModel(SettingsStore settings, ILocalizationService localization)
    {
        _settings = settings;
        _localization = localization;

        var current = settings.Current;
        MinecraftJavaAutoAcceptEula = current.InstanceCreation.MinecraftJavaAutoAcceptEula;
        MinecraftJavaAutoSwitchOnlineMode = current.InstanceCreation.MinecraftJavaAutoSwitchOnlineMode;
        MinecraftBedrockAutoSwitchOnlineMode = current.InstanceCreation.MinecraftBedrockAutoSwitchOnlineMode;
        UseMirrorForMinecraftForgeInstall = current.InstanceCreation.UseMirrorForMinecraftForgeInstall;
        UseMirrorForMinecraftNeoForgeInstall = current.InstanceCreation.UseMirrorForMinecraftNeoForgeInstall;
        UseMirrorForMinecraftFabricInstall = current.InstanceCreation.UseMirrorForMinecraftFabricInstall;
        UseMirrorForMinecraftQuiltInstall = current.InstanceCreation.UseMirrorForMinecraftQuiltInstall;
        DownloadSourceIndex = IndexOf(DownloadSourceKeys, current.Download.DownloadSource, "FastMirror");
        DownloadThreadCount = Math.Clamp(current.Download.ThreadCnt, 1, 256);
        ActionWhenDownloadErrorIndex = IndexOf(DownloadErrorKeys, current.Download.ActionWhenDownloadError, "stop");
        AutoRefreshInterval = Math.Clamp(current.Instance.AutoRefreshInterval, 0, 5);
        ActionOnDoubleClickIndex = IndexOf(DoubleClickKeys, current.Instance.ActionOnDoubleClick, "Console");
        LauncherThemeIndex = IndexOf(ThemeKeys, current.App.Theme, "auto");
        LauncherLanguageIndex = Math.Max(0, IndexOf(
            localization.LanguageCodes,
            current.App.Language,
            "zh-CN"));
        FollowStartup = current.App.FollowStartup;
        AutoCheckUpdate = current.App.AutoCheckUpdate;
        LoadBuildInfo();
        RefreshLocalizedItems();
        _initializing = false;
    }

    public ObservableCollection<string> DownloadSourceItems { get; } = [];
    public ObservableCollection<string> ActionWhenDownloadErrorItems { get; } = [];
    public ObservableCollection<string> ActionOnDoubleClickItems { get; } = [];
    public ObservableCollection<string> ThemeItems { get; } = [];
    public IReadOnlyList<string> LanguageNames => _localization.LanguageNames;
    public ObservableCollection<SettingsLinkItem> Acknowledgments { get; } = [];
    public ObservableCollection<SettingsLinkItem> Components { get; } = [];

    [ObservableProperty] public partial bool MinecraftJavaAutoAcceptEula { get; set; }
    [ObservableProperty] public partial bool MinecraftJavaAutoSwitchOnlineMode { get; set; }
    [ObservableProperty] public partial bool MinecraftBedrockAutoSwitchOnlineMode { get; set; }
    [ObservableProperty] public partial bool UseMirrorForMinecraftForgeInstall { get; set; }
    [ObservableProperty] public partial bool UseMirrorForMinecraftNeoForgeInstall { get; set; }
    [ObservableProperty] public partial bool UseMirrorForMinecraftFabricInstall { get; set; }
    [ObservableProperty] public partial bool UseMirrorForMinecraftQuiltInstall { get; set; }
    [ObservableProperty] public partial int DownloadSourceIndex { get; set; }
    [ObservableProperty] public partial int DownloadThreadCount { get; set; }
    [ObservableProperty] public partial int ActionWhenDownloadErrorIndex { get; set; }
    [ObservableProperty] public partial int AutoRefreshInterval { get; set; }
    [ObservableProperty] public partial int ActionOnDoubleClickIndex { get; set; }
    [ObservableProperty] public partial int LauncherThemeIndex { get; set; }
    [ObservableProperty] public partial int LauncherLanguageIndex { get; set; }
    [ObservableProperty] public partial bool FollowStartup { get; set; }
    [ObservableProperty] public partial bool AutoCheckUpdate { get; set; }
    [ObservableProperty] public partial string BuildInfoText { get; private set; } = string.Empty;
    [ObservableProperty] public partial string VersionText { get; private set; } = string.Empty;

    public string SelectedTheme => ThemeKeys[Math.Clamp(LauncherThemeIndex, 0, ThemeKeys.Length - 1)];

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

    partial void OnMinecraftJavaAutoAcceptEulaChanged(bool value) =>
        Save(setting => setting.InstanceCreation.MinecraftJavaAutoAcceptEula = value);

    partial void OnMinecraftJavaAutoSwitchOnlineModeChanged(bool value) =>
        Save(setting => setting.InstanceCreation.MinecraftJavaAutoSwitchOnlineMode = value);

    partial void OnMinecraftBedrockAutoSwitchOnlineModeChanged(bool value) =>
        Save(setting => setting.InstanceCreation.MinecraftBedrockAutoSwitchOnlineMode = value);

    partial void OnUseMirrorForMinecraftForgeInstallChanged(bool value) =>
        Save(setting => setting.InstanceCreation.UseMirrorForMinecraftForgeInstall = value);

    partial void OnUseMirrorForMinecraftNeoForgeInstallChanged(bool value) =>
        Save(setting => setting.InstanceCreation.UseMirrorForMinecraftNeoForgeInstall = value);

    partial void OnUseMirrorForMinecraftFabricInstallChanged(bool value) =>
        Save(setting => setting.InstanceCreation.UseMirrorForMinecraftFabricInstall = value);

    partial void OnUseMirrorForMinecraftQuiltInstallChanged(bool value) =>
        Save(setting => setting.InstanceCreation.UseMirrorForMinecraftQuiltInstall = value);

    partial void OnDownloadSourceIndexChanged(int value)
    {
        if (value < 0 || value >= DownloadSourceKeys.Length) return;
        Save(setting => setting.Download.DownloadSource = DownloadSourceKeys[value]);
    }

    partial void OnDownloadThreadCountChanged(int value)
    {
        var normalized = Math.Clamp(value, 1, 256);
        if (value != normalized)
        {
            DownloadThreadCount = normalized;
            return;
        }

        Save(setting => setting.Download.ThreadCnt = normalized);
    }

    partial void OnActionWhenDownloadErrorIndexChanged(int value)
    {
        if (value < 0 || value >= DownloadErrorKeys.Length) return;
        Save(setting => setting.Download.ActionWhenDownloadError = DownloadErrorKeys[value]);
    }

    partial void OnAutoRefreshIntervalChanged(int value)
    {
        var normalized = Math.Clamp(value, 0, 5);
        if (value != normalized)
        {
            AutoRefreshInterval = normalized;
            return;
        }

        Save(setting => setting.Instance.AutoRefreshInterval = normalized);
    }

    partial void OnActionOnDoubleClickIndexChanged(int value)
    {
        if (value < 0 || value >= DoubleClickKeys.Length) return;
        Save(setting => setting.Instance.ActionOnDoubleClick = DoubleClickKeys[value]);
    }

    partial void OnLauncherThemeIndexChanged(int value)
    {
        if (value < 0 || value >= ThemeKeys.Length) return;
        Save(setting => setting.App.Theme = ThemeKeys[value]);
        OnPropertyChanged(nameof(SelectedTheme));
    }

    partial void OnLauncherLanguageIndexChanged(int value)
    {
        if (_initializing || value < 0 || value >= _localization.LanguageCodes.Count) return;
        if (!_settings.Current.App.IsFirstSetupFinished) return;

        var language = _localization.LanguageCodes[value];
        _settings.Current.App.Language = language;
        _localization.ChangeLanguage(language);
        _ = _settings.SaveAsync();
    }

    partial void OnFollowStartupChanged(bool value) =>
        Save(setting => setting.App.FollowStartup = value);

    partial void OnAutoCheckUpdateChanged(bool value) =>
        Save(setting => setting.App.AutoCheckUpdate = value);

    private void Save(Action<SettingsDocument> update)
    {
        if (_initializing) return;
        update(_settings.Current);
        _ = _settings.SaveAsync();
    }

    private void Localization_LanguageChanged(object? sender, EventArgs e) => RefreshLocalizedItems();

    private void RefreshLocalizedItems()
    {
        Replace(DownloadSourceItems, DownloadSourceResourceKeys.Select(_localization.Get));
        Replace(ActionWhenDownloadErrorItems, DownloadErrorResourceKeys.Select(_localization.Get));
        Replace(ActionOnDoubleClickItems, DoubleClickResourceKeys.Select(_localization.Get));
        Replace(ThemeItems, ThemeResourceKeys.Select(_localization.Get));

        Replace(Acknowledgments,
        [
            new SettingsLinkItem
            {
                Title = "bangbang93",
                Description = _localization.Get("Settings_Acknowledgments_BMCLAPI_Description"),
                ActionText = _localization.Get("Donate"),
                Uri = "https://afdian.com/a/bangbang93/",
                ImageSource = "Resources/bangbang93.jpg"
            },
            new SettingsLinkItem
            {
                Title = "iNKORE Studios",
                Description = _localization.Get("Settings_Acknowledgments_iNKORE_Description"),
                ActionText = _localization.Get("Donate"),
                Uri = "https://inkore.net/",
                ImageSource = "Resources/iNKORE.png"
            },
            new SettingsLinkItem
            {
                Title = "BakaXL",
                Description = _localization.Get("Settings_Acknowledgments_BakaXL_Description"),
                ActionText = _localization.Get("Donate"),
                Uri = "https://afdian.com/a/TT702/",
                ImageSource = "Resources/BakaXL.png"
            },
            new SettingsLinkItem
            {
                Title = _localization.Get("Settings_Acknowledgments_MCSLQQ_Title"),
                Description = _localization.Get("Settings_Acknowledgments_MCSLQQ_Description"),
                ActionText = _localization.Get("JoinGroup"),
                Uri = "https://qm.qq.com/q/JSEU56DdmK"
            }
        ]);

        var more = _localization.Get("More");
        Replace(Components,
        [
            Component("System.Text.Json", "High-performance JSON framework built into .NET.", "https://learn.microsoft.com/dotnet/standard/serialization/system-text-json", more),
            Component("WinUIIslands", "Hosts WinUI 2 XAML in an unpackaged desktop process.", "https://www.nuget.org/packages/WinUIIslands", more),
            Component("Microsoft.UI.Xaml", "WinUI 2 controls and Fluent resources.", "https://www.nuget.org/packages/Microsoft.UI.Xaml", more),
            Component("WinUIEdit.Uwp", "Scintilla-based text editor for WinUI 2.", "https://www.nuget.org/packages/WinUIEdit.Uwp", more),
            Component("CommunityToolkit.Mvvm", "MVVM source generators and observable infrastructure.", "https://www.nuget.org/packages/CommunityToolkit.Mvvm", more),
            Component("Serilog", "Structured application logging.", "https://serilog.net/", more),
            Component("Downloader", "Multipart download support.", "https://github.com/bezzad/Downloader", more)
        ]);

        // In-place item-text replacement above can reset the selection of the bound
        // ComboBox / RadioButtons controls: they drop SelectedIndex to -1 and the
        // TwoWay bindings push that -1 back into the ViewModel, leaving the controls
        // blank. Re-assert each selected index from the stored settings so the
        // selection is preserved with the newly localized item text.
        DownloadSourceIndex = IndexOf(DownloadSourceKeys, _settings.Current.Download.DownloadSource, "FastMirror");
        ActionWhenDownloadErrorIndex = IndexOf(DownloadErrorKeys, _settings.Current.Download.ActionWhenDownloadError, "stop");
        ActionOnDoubleClickIndex = IndexOf(DoubleClickKeys, _settings.Current.Instance.ActionOnDoubleClick, "Console");
        LauncherThemeIndex = IndexOf(ThemeKeys, _settings.Current.App.Theme, "auto");
        LauncherLanguageIndex = Math.Max(0, IndexOf(
            _localization.LanguageCodes,
            _settings.Current.App.Language,
            "zh-CN"));
    }

    private void LoadBuildInfo()
    {
        var suffix = "REL";
#if DEBUG
        suffix = "DBG";
#endif
        VersionText = $"v{Assembly.GetExecutingAssembly().GetName().Version}-{suffix}";

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("MCServerLauncher.WinUI.Resources.BuildInfo");
        if (stream is null) return;

        try
        {
            var info = JsonSerializer.Deserialize<BuildInfoModel>(stream);
            if (info is not null)
                BuildInfoText = $"Build Time: {info.BuildTime}\nBuild Info: {info.Branch}-{App.AppVersion}-{info.CommitHash}";
        }
        catch
        {
            // Build information is optional and must not block settings.
        }
    }

    private static SettingsLinkItem Component(string title, string description, string uri, string actionText) => new()
    {
        Title = title,
        Description = description,
        ActionText = actionText,
        Uri = uri
    };

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        var list = values.ToList();
        for (var i = 0; i < target.Count && i < list.Count; i++)
        {
            if (!Equals(target[i], list[i])) target[i] = list[i];
        }

        while (target.Count > list.Count) target.RemoveAt(target.Count - 1);
        while (target.Count < list.Count) target.Add(list[target.Count]);
    }

    private static int IndexOf(IReadOnlyList<string> values, string? selectedValue, string defaultValue)
    {
        var value = string.IsNullOrWhiteSpace(selectedValue) ? defaultValue : selectedValue;
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal)) return index;
        }

        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], defaultValue, StringComparison.Ordinal)) return index;
        }

        return 0;
    }

    private sealed class BuildInfoModel
    {
        [JsonPropertyName("buildTime")] public string? BuildTime { get; set; }
        [JsonPropertyName("commitHash")] public string? CommitHash { get; set; }
        [JsonPropertyName("branch")] public string? Branch { get; set; }
    }
}
