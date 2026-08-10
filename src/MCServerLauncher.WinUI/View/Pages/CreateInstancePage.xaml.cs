using System.Windows.Input;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;
using MCServerLauncher.WinUI.View.Features.CreateInstance.PreCreate;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;
using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.DaemonClient.Serialization;

namespace MCServerLauncher.WinUI.Views.Pages;

public sealed partial class CreateInstancePage : Page
{
    private bool _initialized;
    private bool _returnToMinecraftTypes;

    public CreateInstancePage()
    {
        NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public LocalizedStrings Texts => App.Services.Localization.Texts;

    public CreateInstanceSession? Session { get; private set; }

    public void ShowPreCreate()
    {
        Session = null;
        _returnToMinecraftTypes = false;
        UpdateAvailability();
        WizardFrame.Content = new PreCreateInstance(this);
    }

    public async Task ShowMinecraftTypesAsync()
    {
        var session = await SelectDaemonAsync();
        if (session is null) return;
        Session = session;
        _returnToMinecraftTypes = false;
        WizardFrame.Content = new PreCreateMinecraftInstance(this, session);
    }

    public async Task OpenProviderAsync(Func<CreateInstanceSession, CreateInstanceProviderPage> factory)
    {
        var session = await SelectDaemonAsync();
        if (session is null) return;
        Session = session;
        _returnToMinecraftTypes = false;
        WizardFrame.Content = factory(session);
    }

    public void OpenMinecraftProvider(
        CreateInstanceSession session,
        Func<CreateInstanceSession, CreateInstanceProviderPage> factory)
    {
        Session = session;
        _returnToMinecraftTypes = true;
        WizardFrame.Content = factory(session);
    }

    private async Task<CreateInstanceSession?> SelectDaemonAsync()
    {
        var configs = App.Services.Daemons.Items.ToArray();
        if (configs.Length == 0)
        {
            UpdateAvailability();
            return null;
        }

        var labels = configs.Select(config => config.DisplayName).ToArray();
        while (true)
        {
            var list = new ListView
            {
                ItemsSource = labels,
                SelectedIndex = 0,
                SelectionMode = ListViewSelectionMode.Single,
                MinWidth = 420
            };
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = Texts["PleaseSelectDaemon"],
                Content = list,
                PrimaryButtonText = Texts["Continue"],
                CloseButtonText = Texts["Cancel"],
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

            var selected = configs[Math.Clamp(list.SelectedIndex, 0, configs.Length - 1)];
            var daemon = await App.Services.DaemonConnections.GetAsync(selected);
            if (daemon is not null)
            {
                AvailabilityTip.Visibility = Visibility.Collapsed;
                WizardFrame.Visibility = Visibility.Visible;
                return new CreateInstanceSession(selected, daemon);
            }

            var retry = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = Texts["ConnectDaemonFailedTip"],
                Content = Texts["ConnectDaemonFailedSubTip"],
                PrimaryButtonText = Texts["SelectOtherDaemon"],
                CloseButtonText = Texts["Cancel"],
                DefaultButton = ContentDialogButton.Primary
            };
            if (await retry.ShowAsync() != ContentDialogResult.Primary) return null;
        }
    }

    public async Task GoBackFromProviderAsync(bool hasInput)
    {
        if (hasInput)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = Texts["AreYouSure"],
                Content = Texts["GoBackLostTip"],
                PrimaryButtonText = Texts["Back"],
                SecondaryButtonText = Texts["Cancel"],
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        }

        if (_returnToMinecraftTypes && Session is not null)
        {
            WizardFrame.Content = new PreCreateMinecraftInstance(this, Session);
            return;
        }

        ShowPreCreate();
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string content,
        InstanceFactorySetting? debugSetting = null)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 500
            },
            PrimaryButtonText = Texts["Continue"],
            SecondaryButtonText = Texts["Cancel"],
            DefaultButton = ContentDialogButton.Secondary
        };

        if (debugSetting is not null && App.Window.RootPage.IsDebugItemVisible)
        {
            dialog.CloseButtonText = Texts["DebugCopyConfig"];
            dialog.CloseButtonClick += (_, _) =>
            {
                var json = JsonSerializer.Serialize(
                    debugSetting,
                    DaemonClientRpcJsonBoundary.CreateStjOptions(writeIndented: true));
                App.Services.Clipboard.SetText(json);
                App.Services.Notifications.Push(
                    Texts["Success"],
                    Texts["InstanceConfigCopied"],
                    NotificationSeverity.Success,
                    durationMs: 3000,
                    showSystemNotification: false);
            };
        }

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
        {
            _initialized = true;
            ShowPreCreate();
            return;
        }

        UpdateAvailability();
    }

    private ICommand? _connectDaemonCommand;
    private ICommand ConnectDaemonCommand => _connectDaemonCommand ??= new RelayCommand(() =>
        App.Window.RootPage.Navigate(typeof(DaemonManagerPage), DaemonManagerPage.OpenConnectionParameter));

    private void UpdateAvailability()
    {
        var unavailable = App.Services.Daemons.Items.Count == 0;
        if (unavailable)
        {
            AvailabilityTip.Symbol = "❌";
            AvailabilityTip.StopTip = Texts["FuncDisabled"];
            AvailabilityTip.StopDescription = Texts["FuncDisabledReason_NoDaemon"];
            AvailabilityTip.ButtonText = Texts["ConnectDaemon"];
            AvailabilityTip.ButtonCommand = ConnectDaemonCommand;
        }
        AvailabilityTip.Visibility = unavailable ? Visibility.Visible : Visibility.Collapsed;
        WizardFrame.Visibility = unavailable ? Visibility.Collapsed : Visibility.Visible;
    }

    public void Push(
        string title,
        string message,
        NotificationSeverity severity = NotificationSeverity.Informational,
        bool isClosable = true,
        int durationMs = 1500,
        bool showSystemNotification = true)
    {
        App.Services.Notifications.Push(
            title,
            message,
            severity,
            isClosable,
            durationMs,
            showSystemNotification);
    }
}
