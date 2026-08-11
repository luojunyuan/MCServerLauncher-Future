using Windows.System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using MCServerLauncher.WinUI.Core;
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
    private readonly List<DispatcherQueueTimer> _toastTimers = new();
    private bool _themeInitialized;
    private bool _viewInitialized;
    private bool _notificationsSubscribed;
    private bool _dimmed;
    private bool _pointerOver;

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

        // Dim the download-history button's foreground when the window is unfocused;
        // pointer-over and theme changes re-evaluate the effective colour.
        DownloadHistoryButton.PointerEntered += (_, _) => { _pointerOver = true; UpdateDownloadHistoryButtonForeground(); };
        DownloadHistoryButton.PointerExited += (_, _) => { _pointerOver = false; UpdateDownloadHistoryButtonForeground(); };
        DownloadHistoryButton.ActualThemeChanged += (_, _) => UpdateDownloadHistoryButtonForeground();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string ProductName => Core.AppInfo.ProductName;
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public ResourceDownloadViewModel DownloadHistoryViewModel { get; }
    public FrameworkElement TitleBarElement => AppTitleText;

    public void SetDownloadHistoryButtonDimmed(bool dimmed)
    {
        _dimmed = dimmed;
        UpdateDownloadHistoryButtonForeground();
    }

    private void UpdateDownloadHistoryButtonForeground()
    {
        // Pointer-over takes priority: restore the normal foreground so hovering the
        // button is unaffected while the window is unfocused.
        if (_pointerOver)
        {
            DownloadHistoryButton.ClearValue(Control.ForegroundProperty);
            return;
        }

        if (_dimmed)
        {
            var isLight = DownloadHistoryButton.ActualTheme == ElementTheme.Light;
            var value = (byte)(isLight ? 143 : 109);
            DownloadHistoryButton.Foreground = new SolidColorBrush(Color.FromArgb(255, value, value, value));
        }
        else
        {
            DownloadHistoryButton.ClearValue(Control.ForegroundProperty);
        }
    }

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

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        OnLoadedCore().FireAndForget("MainPage.OnLoaded");

    private async Task OnLoadedCore()
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

        DownloadHistoryViewModel.Attach();

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

    private void DownloadHistoryFlyout_Opened(object sender, object e)
    {
        // The flyout presenter renders in a separate popup root that does not inherit
        // the root's RequestedTheme (microsoft-ui-xaml#6622), and Flyout.PresenterStyle
        // is not exposed in the WinUIIslands projection. Theme the content directly and
        // re-theme the open presenter through FrameworkElement.RequestedTheme.
        var theme = GetCurrentElementTheme();

        var contentThemed = false;
        if (DownloadHistoryFlyout.Content is FrameworkElement content)
        {
            content.RequestedTheme = theme;
            contentThemed = true;
        }

        var presenterCount = 0;
        var xamlRoots = new[] { DownloadHistoryFlyout.XamlRoot, App.Window.RootPage.XamlRoot }
            .Where(root => root is not null)
            .Distinct();
        foreach (var xamlRoot in xamlRoots)
        {
            foreach (var popup in Windows.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
            {
                if (popup.Child is FrameworkElement presenter)
                {
                    presenter.RequestedTheme = theme;
                    presenterCount++;
                }
            }
        }

        Serilog.Log.Debug(
            "[WinUI] Download flyout themed: content={Content}, presenters={Presenters}",
            contentThemed,
            presenterCount);
    }

    private static ElementTheme GetCurrentElementTheme() => App.Services.Settings.Current.App.Theme switch
    {
        "light" => ElementTheme.Light,
        "dark" => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    private void RetryDownload_Click(object sender, RoutedEventArgs e) =>
        RetryDownload_ClickCore(sender).FireAndForget("MainPage.RetryDownload_Click");

    private async Task RetryDownload_ClickCore(object sender)
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
        DownloadHistoryViewModel.Detach();
        _notificationTimer?.Stop();
        foreach (var timer in _toastTimers)
        {
            timer.Stop();
        }
        _toastTimers.Clear();
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
            // Informational messages keep the single top InfoBar ("bar"
            // severity); the more actionable severities surface as in-window
            // toast cards in the 4-position host.
            if (message.Severity == NotificationSeverity.Informational)
            {
                ShowInfoBarNotification(message);
            }
            else
            {
                ShowToast(message);
            }
        });
    }

    private void ShowInfoBarNotification(NotificationMessage message)
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
    }

    private void ShowToast(NotificationMessage message)
    {
        // The event payload carries no position field and the interface does
        // not expose one, so toasts use the default Top slot (matching the shared
        // default). The other three panels are wired up and ready to receive
        // toasts if a position is introduced later.
        var panel = ToastPanelTop;
        var card = CreateToastCard(message);

        panel.Children.Add(card);
        card.Opacity = 0;
        AnimateOpacity(card, 0, 1, 200);

        var timer = App.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(Math.Max(4000, message.DurationMs));
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _toastTimers.Remove(timer);
            DismissToast(card);
        };
        timer.Start();
        _toastTimers.Add(timer);
    }

    private Border CreateToastCard(NotificationMessage message)
    {
        var accent = GetSeverityBrush(message.Severity);

        var accentBar = new Border
        {
            Width = 4,
            Background = accent,
            CornerRadius = new CornerRadius(2),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var title = new TextBlock
        {
            Text = message.Title,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var textStack = new StackPanel { Spacing = 2 };
        textStack.Children.Add(title);
        if (!string.IsNullOrEmpty(message.Message))
        {
            textStack.Children.Add(new TextBlock
            {
                Text = message.Message,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8
            });
        }

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(accentBar, 0);
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(accentBar);
        grid.Children.Add(textStack);

        Border card = null!;

        if (message.IsClosable)
        {
            var closeButton = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 12 },
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(4),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0)
            };
            AutomationProperties.SetName(closeButton, Texts["Close"]);
            closeButton.Click += (_, _) => DismissToast(card);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(closeButton, 2);
            grid.Children.Add(closeButton);
        }

        card = new Border
        {
            Style = GetCardStyle(),
            Padding = new Thickness(12, 10, 12, 10),
            MaxWidth = 360,
            IsHitTestVisible = true
        };
        if (card.Style is null)
        {
            // CardBorderStyle unavailable - fall back to a solid theme surface.
            card.Background = TryResolveBrush("CardBackgroundFillColorDefaultBrush")
                              ?? new SolidColorBrush(Colors.White);
            card.BorderBrush = TryResolveBrush("CardStrokeColorDefaultBrush");
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(4);
        }
        card.Child = grid;

        // ThemeShadow needs a composition target; XAML Islands may not provide
        // one, so fall back to a flat card rather than failing to show it.
        try
        {
            card.Shadow = new ThemeShadow();
        }
        catch
        {
            // Drop shadow unsupported in this host - flat card is fine.
        }

        return card;
    }

    private static Style? GetCardStyle()
    {
        try
        {
            return Application.Current.Resources["CardBorderStyle"] as Style;
        }
        catch
        {
            return null;
        }
    }

    private static Brush? TryResolveBrush(string key)
    {
        try
        {
            return Application.Current.Resources[key] as Brush;
        }
        catch
        {
            return null;
        }
    }

    private static SolidColorBrush GetSeverityBrush(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Success => new SolidColorBrush(Color.FromArgb(255, 16, 124, 16)),
        NotificationSeverity.Warning => new SolidColorBrush(Color.FromArgb(255, 200, 150, 0)),
        NotificationSeverity.Error => new SolidColorBrush(Color.FromArgb(255, 196, 43, 28)),
        _ => new SolidColorBrush(Color.FromArgb(255, 0, 120, 212))
    };

    private static void DismissToast(Border card)
    {
        if (card.Tag is true) return;
        card.Tag = true;
        if (card.Parent is not StackPanel panel) return;

        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, card);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) =>
        {
            if (card.Parent == panel) panel.Children.Remove(card);
        };
        storyboard.Begin();
    }

    private static void AnimateOpacity(UIElement element, double from, double to, int durationMs)
    {
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
        storyboard.Begin();
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
