using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.Models;
using MCServerLauncher.WinUI.ViewModels;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI;

public sealed partial class MainPage : Page
{
    private readonly NavigationService _navigation = new();
    private readonly Exception? _startupError;
    private DispatcherQueueTimer? _notificationTimer;
    private bool _themeInitialized;
    private bool _viewInitialized;
    private bool _notificationsSubscribed;

    public MainPage(Exception? startupError = null)
    {
        _startupError = startupError;
        DownloadHistoryViewModel = new ResourceDownloadViewModel(
            App.Services.Paths,
            App.Services.Daemons,
            App.Services.DaemonConnections,
            App.Services.Localization,
            App.Services.Notifications);
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string ProductName => "MCServerLauncher Future";
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public ResourceDownloadViewModel DownloadHistoryViewModel { get; }
    public FrameworkElement TitleBarElement => AppTitleBar;

    public bool IsDebugItemVisible => DebugItem.Visibility == Visibility.Visible;

    public bool Navigate(Type pageType, object? parameter = null)
    {
        var item = GetNavigationItem(pageType);
        if (item is not null) NavView.SelectedItem = item;
        return _navigation.Navigate(pageType, parameter);
    }

    public void ShowShell()
    {
        FullScreenFrame.Content = null;
        FullScreenFrame.Visibility = Visibility.Collapsed;
        NavView.Visibility = Visibility.Visible;
        NavView.Opacity = 1;
        DownloadHistoryButton.Visibility = Visibility.Visible;
        _navigation.Attach(ContentFrame);

        if (ContentFrame.Content is null)
        {
            Navigate(typeof(HomePage));
        }
    }

    public async Task CompleteFirstSetupAsync()
    {
        if (FullScreenFrame.Visibility == Visibility.Visible)
        {
            await AnimateOpacityAsync(FullScreenFrame, 1, 0, 200);
        }

        NavView.Opacity = 0;
        ShowShell();
        NavView.Opacity = 0;
        await AnimateOpacityAsync(NavView, 0, 1, 200);
    }

    public void ShowFirstSetupForDebug()
    {
        var setup = new FirstSetupPage();
        setup.RestartForDebug();
        FullScreenFrame.Content = setup;
        FullScreenFrame.Opacity = 1;
        FullScreenFrame.Visibility = Visibility.Visible;
        NavView.Visibility = Visibility.Collapsed;
        DownloadHistoryButton.Visibility = Visibility.Collapsed;
    }

    public void ContinueAfterStartupError()
    {
        if (App.Services.Settings.Current.App.IsFirstSetupFinished)
        {
            ShowShell();
            return;
        }

        FullScreenFrame.Content = new FirstSetupPage();
        FullScreenFrame.Opacity = 1;
        FullScreenFrame.Visibility = Visibility.Visible;
        NavView.Visibility = Visibility.Collapsed;
        DownloadHistoryButton.Visibility = Visibility.Collapsed;
    }

    public void ShowDebugItem() => DebugItem.Visibility = Visibility.Visible;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_themeInitialized)
        {
            _themeInitialized = true;
            App.Services.Themes.Apply(this, App.Services.Settings.Current.App.Theme);
        }

        if (!_notificationsSubscribed)
        {
            App.Services.Notifications.NotificationRaised += OnNotificationRaised;
            _notificationsSubscribed = true;
        }

        if (_viewInitialized) return;
        _viewInitialized = true;

        if (_startupError is not null)
        {
            FullScreenFrame.Content = new StartupErrorPage(_startupError);
            DownloadHistoryButton.Visibility = Visibility.Collapsed;
        }
        else if (!App.Services.Settings.Current.App.IsFirstSetupFinished)
        {
            FullScreenFrame.Content = new FirstSetupPage();
            DownloadHistoryButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ShowShell();
        }

        await Task.Delay(1500);
        await AnimateOpacityAsync(LoadingOverlay, 1, 0, 400);
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private void DownloadHistoryButton_Click(object sender, RoutedEventArgs e) =>
        DownloadHistoryViewModel.ReloadHistory();

    private async void RetryDownload_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DownloadHistoryItem item)
            await DownloadHistoryViewModel.RetryCommand.ExecuteAsync(item);
    }

    private void CopyDownloadUrl_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DownloadProgressEntry entry)
            DownloadHistoryViewModel.CopyUrl(entry);
    }

    private void PauseResumeDownload_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not DownloadProgressEntry entry) return;
        if (entry.IsPaused)
            DownloadHistoryViewModel.ResumeDownload(entry);
        else
            DownloadHistoryViewModel.PauseDownload(entry);
    }

    private void CancelDownload_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DownloadProgressEntry entry)
            DownloadHistoryViewModel.CancelDownload(entry);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_notificationsSubscribed)
        {
            App.Services.Notifications.NotificationRaised -= OnNotificationRaised;
            _notificationsSubscribed = false;
        }
        _notificationTimer?.Stop();
    }

    private void NavigationView_ItemInvoked(
        Microsoft.UI.Xaml.Controls.NavigationView sender,
        Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
    {
        var tag = (args.InvokedItemContainer as Microsoft.UI.Xaml.Controls.NavigationViewItem)?.Tag?.ToString();
        var page = tag switch
        {
            "Home" => typeof(HomePage),
            "Create" => typeof(CreateInstancePage),
            "Instances" => typeof(InstanceManagerPage),
            "Daemons" => typeof(DaemonManagerPage),
            "Resources" => typeof(ResourceDownloadPage),
            "Help" => typeof(HelpPage),
            "Debug" => typeof(DebugPage),
            "Settings" => typeof(SettingsPage),
            _ => null
        };

        if (page is not null)
            Navigate(page);
    }

    private Microsoft.UI.Xaml.Controls.NavigationViewItem? GetNavigationItem(Type pageType) => pageType switch
    {
        _ when pageType == typeof(HomePage) => HomeNavigationItem,
        _ when pageType == typeof(CreateInstancePage) => CreateNavigationItem,
        _ when pageType == typeof(InstanceManagerPage) => InstanceManagerNavigationItem,
        _ when pageType == typeof(DaemonManagerPage) => DaemonManagerNavigationItem,
        _ when pageType == typeof(ResourceDownloadPage) => ResourceDownloadNavigationItem,
        _ when pageType == typeof(HelpPage) => HelpNavigationItem,
        _ when pageType == typeof(DebugPage) => DebugItem,
        _ when pageType == typeof(SettingsPage) => SettingsNavigationItem,
        _ => null
    };

    private void OnNotificationRaised(object? sender, NotificationMessage message)
    {
        App.DispatcherQueue.TryEnqueue(() =>
        {
            NotificationBar.Title = message.Title;
            NotificationBar.Message = message.Message;
            NotificationBar.IsClosable = message.IsClosable;
            NotificationBar.Severity = message.Severity switch
            {
                NotificationSeverity.Success => Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success,
                NotificationSeverity.Warning => Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
                NotificationSeverity.Error => Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error,
                _ => Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational
            };
            NotificationBar.IsOpen = true;

            _notificationTimer?.Stop();
            _notificationTimer = App.DispatcherQueue.CreateTimer();
            _notificationTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(500, message.DurationMs));
            _notificationTimer.IsRepeating = false;
            _notificationTimer.Tick += (_, _) => NotificationBar.IsOpen = false;
            _notificationTimer.Start();
        });
    }

    private static Task AnimateOpacityAsync(UIElement element, double from, double to, int durationMs)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) =>
        {
            element.Opacity = to;
            completion.TrySetResult();
        };
        storyboard.Begin();
        return completion.Task;
    }
}
