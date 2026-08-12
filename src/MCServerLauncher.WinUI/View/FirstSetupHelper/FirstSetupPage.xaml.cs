using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Models;
using MCServerLauncher.WinUI.Views.Components.DaemonManager;

namespace MCServerLauncher.WinUI.Views.Pages;

/// <summary>
/// The first-run flow intentionally follows the shared four-step state
/// machine. Only the current navigation item is enabled, so a user cannot
/// bypass the language, EULA, or daemon steps accidentally.
/// </summary>
public sealed partial class FirstSetupPage : Page, INotifyPropertyChanged
{
    private const int AcceptCountdownSeconds = 15;
    private readonly DispatcherTimer _acceptCountdownTimer;
    private bool _initializing;
    private bool _ignoreNavigation;
    private int _acceptCountdownRemaining = AcceptCountdownSeconds;
    private int _step;
    private int _languageIndex;
    private string _acceptButtonText = string.Empty;
    private string _daemonError = string.Empty;
    private bool _isDebugSession;

    public FirstSetupPage()
    {
        LanguageNames = App.Services.Localization.LanguageNames;
        LanguageIndex = Math.Max(0, Array.FindIndex(
            App.Services.Localization.LanguageCodes.ToArray(),
            code => string.Equals(code, App.Services.Settings.Current.App.Language, StringComparison.OrdinalIgnoreCase)));
        AddedDaemons = new ObservableCollection<DaemonConfigModel>(App.Services.Daemons.Items);

        _acceptCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _acceptCountdownTimer.Tick += AcceptCountdownTimerTick;

        InitializeComponent();
        App.Services.Localization.LanguageChanged += Localization_LanguageChanged;
        Unloaded += (_, _) =>
        {
            _acceptCountdownTimer.Stop();
            App.Services.Localization.LanguageChanged -= Localization_LanguageChanged;
        };

        _initializing = true;
        LanguageIndex = Math.Max(0, Array.FindIndex(
            App.Services.Localization.LanguageCodes.ToArray(),
            code => string.Equals(code, App.Services.Settings.Current.App.Language, StringComparison.OrdinalIgnoreCase)));
        _initializing = false;

        AcceptButtonText = Texts["FirstSetup_EulaContinueCountdown"].Replace("{0}", AcceptCountdownSeconds.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);
        RefreshNavMenu(0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public IReadOnlyList<string> LanguageNames { get; }
    public ObservableCollection<DaemonConfigModel> AddedDaemons { get; }
    public string AcceptButtonText
    {
        get => _acceptButtonText;
        private set
        {
            if (_acceptButtonText == value) return;
            _acceptButtonText = value;
            OnPropertyChanged();
        }
    }

    public string ContinueButtonText => Texts["Continue"];
    public int LanguageIndex
    {
        get => _languageIndex;
        private set
        {
            if (_languageIndex == value) return;
            _languageIndex = value;
            OnPropertyChanged();
        }
    }

    public string DaemonError
    {
        get => _daemonError;
        private set
        {
            if (_daemonError == value) return;
            _daemonError = value;
            DaemonErrorText.Text = value;
            OnPropertyChanged();
        }
    }

    public bool IsDebugSession => _isDebugSession;

    internal static string GetEulaUrl(string? language) => language switch
    {
        "en-US" => "https://future.mcsl.com.cn/en/eula.html",
        "ja-JP" => "https://future.mcsl.com.cn/ja/eula.html",
        "ru-RU" => "https://future.mcsl.com.cn/ru/eula.html",
        "zh-HK" or "zh-TW" => "https://future.mcsl.com.cn/zh-hant/eula.html",
        _ => "https://future.mcsl.com.cn/eula.html"
    };

    private string CurrentEulaUrl => GetEulaUrl(App.Services.Settings.Current.App.Language);

    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || LanguageIndex < 0 || LanguageIndex >= App.Services.Localization.LanguageCodes.Count)
            return;

        var language = App.Services.Localization.LanguageCodes[LanguageIndex];
        App.Services.Settings.Current.App.Language = language;
        App.Services.Localization.ChangeLanguage(language);
        App.Services.Settings.SaveAsync().FireAndForget("LanguageChanged");
        RefreshEulaUrl();
    }

    private void StepNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_ignoreNavigation || args.SelectedItemContainer is not NavigationViewItem item ||
            !int.TryParse(item.Tag?.ToString(), out var selectedStep))
            return;

        if (selectedStep <= _step)
        {
            NavigateToStep(selectedStep);
        }
        else
        {
            _ignoreNavigation = true;
            StepNavigation.SelectedItem = StepNavigation.MenuItems[_step];
            _ignoreNavigation = false;
        }
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case 0:
                GoEulaSetup();
                break;
            case 2:
                GoWelcomeSetup();
                break;
            case 3:
                CompleteSetupAsync().FireAndForget("Continue_Click");
                break;
        }
    }

    private void RefreshNavMenu(int newIndex)
    {
        _step = newIndex;
        _ignoreNavigation = true;
        StepNavigation.SelectedItem = StepNavigation.MenuItems[newIndex];
        _ignoreNavigation = false;

        for (var index = 0; index < StepNavigation.MenuItems.Count; index++)
        {
            if (StepNavigation.MenuItems[index] is NavigationViewItem item)
                item.IsEnabled = index == newIndex;
        }

        NavigateToStep(newIndex);
    }

    private void NavigateToStep(int step)
    {
        if (step < 0 || step > 3) return;
        _step = step;
        LanguageStep.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        EulaStep.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        DaemonStep.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        WelcomeStep.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;

        RefuseButton.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        AcceptButton.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        DaemonSkipButton.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        ContinueButton.Visibility = step is 0 or 2 or 3 ? Visibility.Visible : Visibility.Collapsed;
        ContinueButton.IsEnabled = step != 2;

        if (step == 1)
        {
            if (App.Services.Settings.Current.App.IsAppEulaAccepted && !_isDebugSession)
            {
                GoDaemonSetup();
                return;
            }

            RefreshEulaUrl();
            ResetAcceptCountdown();
        }
        else
        {
            _acceptCountdownTimer.Stop();
        }

        if (step == 2 && RemoteSetupPanel.Visibility == Visibility.Visible)
            LoadExistingDaemonsAsync().FireAndForget("NavigateToStep");
    }

    private void RefreshEulaUrl() => EulaUrlTextBlock.Text = CurrentEulaUrl;

    private void ResetAcceptCountdown()
    {
        _acceptCountdownRemaining = AcceptCountdownSeconds;
        AcceptButton.IsEnabled = false;
        AcceptButtonText = string.Format(CultureInfo.CurrentCulture, Texts["FirstSetup_EulaContinueCountdown"], _acceptCountdownRemaining);
        _acceptCountdownTimer.Stop();
        _acceptCountdownTimer.Start();
    }

    private void AcceptCountdownTimerTick(object? sender, object e)
    {
        _acceptCountdownRemaining--;
        if (_acceptCountdownRemaining <= 0)
        {
            _acceptCountdownTimer.Stop();
            AcceptButton.IsEnabled = true;
            AcceptButtonText = Texts["Agree"];
            return;
        }

        AcceptButtonText = string.Format(CultureInfo.CurrentCulture, Texts["FirstSetup_EulaContinueCountdown"], _acceptCountdownRemaining);
    }

    private void OpenEulaInBrowser(object sender, RoutedEventArgs e)
        => OpenEulaInBrowserCoreAsync().FireAndForget("OpenEulaInBrowser");

    private async Task OpenEulaInBrowserCoreAsync()
    {
        try { await global::Windows.System.Launcher.LaunchUriAsync(new Uri(CurrentEulaUrl)); }
        catch (Exception ex) { DaemonError = ex.Message; }
    }

    private void RefuseEula(object sender, RoutedEventArgs e)
        => RefuseEulaCoreAsync().FireAndForget("RefuseEula");

    private async Task RefuseEulaCoreAsync()
    {
        var dialog = CreateDialog(
            Texts["AreYouSure"],
            Texts["FirstSetup_EulaDisagreeTip"],
            Texts["NotNow"],
            Texts["Disagree"]);
        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Secondary)
                App.Window.Close();
        }
        catch { }
    }

    private void AcceptEula(object sender, RoutedEventArgs e)
        => AcceptEulaCoreAsync().FireAndForget("AcceptEula");

    private async Task AcceptEulaCoreAsync()
    {
        var dialog = CreateDialog(
            Texts["AreYouSure"],
            Texts["FirstSetup_EulaAgreeTip"],
            Texts["Agree"],
            Texts["NotNow"]);
        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                GoDaemonSetup();
        }
        catch { }
    }

    private void UseLocalDaemon(object sender, RoutedEventArgs e)
        => UseLocalDaemonCoreAsync().FireAndForget("UseLocalDaemon");

    private async Task UseLocalDaemonCoreAsync()
    {
        var dialog = CreateDialog(
            Texts["FirstSetup_DaemonLocalDownload"],
            Texts["FirstSetup_DaemonLocalDownloadUnavailable"],
            Texts["OK"],
            null);
        try { await dialog.ShowAsync(); } catch { }
        await AskAddRemoteHostAfterLocalAsync();
    }

    private void UseRemoteDaemon(object sender, RoutedEventArgs e) => ShowRemoteSetupAsync().FireAndForget("UseRemoteDaemon");

    private async Task ShowRemoteSetupAsync()
    {
        LocalChoicePanel.Visibility = Visibility.Collapsed;
        RemoteSetupPanel.Visibility = Visibility.Visible;
        await LoadExistingDaemonsAsync();
    }

    private void AddDaemonConnection(object sender, RoutedEventArgs e)
        => AddRemoteDaemonConnectionAsync(null).FireAndForget("AddDaemonConnection");

    private void EditDaemonConnection(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is DaemonConfigModel config)
            AddRemoteDaemonConnectionAsync(config).FireAndForget("EditDaemonConnection");
    }

    private async Task AddRemoteDaemonConnectionAsync(DaemonConfigModel? existing)
    {
        var config = await ShowDaemonDialogAsync(existing);
        if (config is null) return;

        DaemonError = string.Empty;
        var daemon = await App.Services.DaemonConnections.GetAsync(config);
        if (daemon is null)
        {
            DaemonError = Texts["ConnectDaemonFailedTip"];
            return;
        }

        if (existing is not null)
        {
            var index = AddedDaemons.IndexOf(existing);
            if (index >= 0) AddedDaemons[index] = config;
            App.Services.Daemons.Replace(existing, config);
        }
        else if (!AddedDaemons.Any(item => string.Equals(item.DisplayName, config.DisplayName, StringComparison.Ordinal)))
        {
            AddedDaemons.Add(config);
            App.Services.Daemons.Add(config);
        }

        ContinueButton.IsEnabled = AddedDaemons.Count > 0;
        if (existing is null)
            await AskAddAnotherHostAsync();
    }

    private async Task<DaemonConfigModel?> ShowDaemonDialogAsync(DaemonConfigModel? existing)
    {
        var input = new NewDaemonConnectionInput();
        if (existing is not null) input.Load(existing);

        DaemonConfigModel? config = null;
        var dialog = CreateDialog(Texts["ConnectDaemon"], input, Texts["ConnectDaemon"], Texts["Back"]);
        dialog.XamlRoot = XamlRoot;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (input.TryCreateConfig(out var value))
            {
                config = value;
                return;
            }

            args.Cancel = true;
        };
        try
        {
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        }
        catch { return null; }
        return config;
    }

    private Task LoadExistingDaemonsAsync()
    {
        AddedDaemons.Clear();
        foreach (var config in App.Services.Daemons.Items)
            AddedDaemons.Add(config);
        ContinueButton.IsEnabled = AddedDaemons.Count > 0;
        OnPropertyChanged(nameof(AddedDaemons));
        return Task.CompletedTask;
    }

    private void SkipDaemon(object sender, RoutedEventArgs e)
        => SkipDaemonCoreAsync().FireAndForget("SkipDaemon");

    private async Task SkipDaemonCoreAsync()
    {
        var dialog = CreateDialog(
            Texts["AreYouSure"],
            Texts["FirstSetup_SkipConnectDaemonTip"],
            Texts["TempSkip"],
            Texts["Back"]);
        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                GoWelcomeSetup();
        }
        catch { }
    }

    private async Task AskAddAnotherHostAsync()
    {
        var dialog = CreateDialog(
            Texts["FirstSetup_DaemonAddAnotherHostTitle"],
            Texts["FirstSetup_DaemonAddAnotherHostTip"],
            Texts["FirstSetup_DaemonAddAnotherHost"],
            Texts["FirstSetup_DaemonFinishAdding"]);
        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Secondary)
                GoWelcomeSetup();
        }
        catch { }
    }

    private async Task AskAddRemoteHostAfterLocalAsync()
    {
        var dialog = CreateDialog(
            Texts["FirstSetup_DaemonAddAnotherHostTitle"],
            Texts["FirstSetup_DaemonAddAnotherHostTip"],
            Texts["FirstSetup_DaemonAddAnotherHost"],
            Texts["FirstSetup_DaemonFinishAdding"]);
        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await ShowRemoteSetupAsync();
            else
                GoWelcomeSetup();
        }
        catch { }
    }

    public void GoEulaSetup() => RefreshNavMenu(1);

    public void GoDaemonSetup()
    {
        if (!_isDebugSession)
        {
            App.Services.Settings.Current.App.IsAppEulaAccepted = true;
            App.Services.Settings.SaveAsync().FireAndForget("GoDaemonSetup");
        }

        RefreshNavMenu(2);
    }

    public void GoWelcomeSetup() => RefreshNavMenu(3);

    private async Task CompleteSetupAsync()
    {
        if (!_isDebugSession)
        {
            App.Services.Settings.Current.App.IsFirstSetupFinished = true;
            await App.Services.Settings.SaveAsync();
        }

        App.Services.ConnectConfiguredDaemonsAsync().FireAndForget("CompleteSetupAsync");
        await App.Window.RootPage.CompleteFirstSetupAsync();
    }

    public void RestartForDebug()
    {
        _isDebugSession = true;
        Opacity = 1;
        RefreshNavMenu(0);
    }

    private static ContentDialog CreateDialog(string title, object content, string? primaryButton, string? secondaryButton)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButton ?? string.Empty,
            SecondaryButtonText = secondaryButton ?? string.Empty,
            DefaultButton = ContentDialogButton.Primary,
            FullSizeDesired = false
        };
        return dialog;
    }

    private ContentDialog CreateDialog(string title, string content, string? primaryButton, string? secondaryButton)
    {
        var dialog = CreateDialog(title, new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap }, primaryButton, secondaryButton);
        dialog.XamlRoot = XamlRoot;
        return dialog;
    }

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        RefreshEulaUrl();
        AcceptButtonText = _acceptCountdownRemaining <= 0
            ? Texts["Agree"]
            : string.Format(CultureInfo.CurrentCulture, Texts["FirstSetup_EulaContinueCountdown"], _acceptCountdownRemaining);
        OnPropertyChanged(nameof(Texts));
        OnPropertyChanged(nameof(ContinueButtonText));
        foreach (var daemon in AddedDaemons)
            daemon.RefreshLocalizedText();
        OnPropertyChanged(nameof(AddedDaemons));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
