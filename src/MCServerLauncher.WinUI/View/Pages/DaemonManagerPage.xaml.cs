using System.ComponentModel;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Models;
using MCServerLauncher.WinUI.ViewModels;
using MCServerLauncher.WinUI.Views.Components.DaemonManager;

namespace MCServerLauncher.WinUI.Views.Pages;

public sealed partial class DaemonManagerPage : Page
{
    public const string OpenConnectionParameter = "open_connection";
    private DispatcherQueueTimer? _refreshTimer;
    private bool _isPageLoaded;
    private bool _openConnectionOnLoad;

    public DaemonManagerPage()
    {
        NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;
        ViewModel = new DaemonManagerViewModel(
            App.Services.Daemons,
            App.Services.Settings,
            App.Services.DaemonConnections,
            App.Services.Notifications,
            App.Services.Localization);
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public DaemonManagerViewModel ViewModel { get; }
    public LocalizedStrings Texts => App.Services.Localization.Texts;

    public Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public Task OpenAddConnectionAsync() => ShowConnectionDialogAsync(null);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _openConnectionOnLoad = string.Equals(
            e.Parameter?.ToString(),
            OpenConnectionParameter,
            StringComparison.Ordinal);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = true;
        ViewModel.Attach();
        await ViewModel.RefreshAsync();
        StartAutoRefresh();
        if (_openConnectionOnLoad)
        {
            _openConnectionOnLoad = false;
            await OpenAddConnectionAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = false;
        StopAutoRefresh();
        ViewModel.Detach();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DaemonManagerViewModel.AutoRefreshEnabled)
            or nameof(DaemonManagerViewModel.RefreshIntervalSeconds))
        {
            StartAutoRefresh();
        }
    }

    private void StartAutoRefresh()
    {
        if (!ViewModel.AutoRefreshEnabled || !_isPageLoaded)
        {
            StopAutoRefresh();
            return;
        }

        _refreshTimer ??= DispatcherQueue.GetForCurrentThread().CreateTimer();
        _refreshTimer.Stop();
        _refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, ViewModel.RefreshIntervalSeconds));
        _refreshTimer.IsRepeating = true;
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
    }

    private void StopAutoRefresh() => _refreshTimer?.Stop();

    private async void RefreshTimer_Tick(DispatcherQueueTimer sender, object args) =>
        await ViewModel.AutoRefreshAsync();

    private async void AddConnection_Click(object sender, RoutedEventArgs e) =>
        await ShowConnectionDialogAsync(null);

    private async void EditDaemon_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DaemonCardModel card)
            await ShowConnectionDialogAsync(card);
    }

    private async void DeleteDaemon_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not DaemonCardModel card || XamlRoot is null) return;
        var confirmed = await App.Services.Dialogs.ConfirmCountdownAsync(
            XamlRoot,
            Texts["ConfirmDelete"],
            string.Format(Texts["ConfirmDeleteDaemonMessage"], card.FriendlyName),
            Texts["Delete"],
            Texts["Cancel"],
            isDestructive: true);
        if (confirmed) await ViewModel.DeleteConnectionAsync(card);
    }

    private async void ShowDaemonError_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not DaemonCardModel card || XamlRoot is null) return;
        await App.Services.Dialogs.ShowErrorAsync(
            XamlRoot,
            Texts["ConnectDaemonFailedTip"],
            string.IsNullOrWhiteSpace(card.LastErrorMessage)
                ? Texts["ConnectDaemonFailedSubTip"]
                : card.LastErrorMessage);
    }

    private async Task ShowConnectionDialogAsync(DaemonCardModel? existing)
    {
        if (XamlRoot is null) return;
        var input = new NewDaemonConnectionInput();
        if (existing is not null) input.Load(existing.Config);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Texts[existing is null ? "ConnectDaemon" : "EditDaemon"],
            Content = input,
            PrimaryButtonText = Texts[existing is null ? "Connect" : "Save"],
            CloseButtonText = Texts["Cancel"],
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                if (!input.TryCreateConfig(out var config))
                {
                    args.Cancel = true;
                    return;
                }

                var error = existing is null
                    ? await ViewModel.AddConnectionAsync(config)
                    : await ViewModel.EditConnectionAsync(existing, config);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    input.ShowConnectionError(error);
                    args.Cancel = true;
                }
            }
            finally
            {
                deferral.Complete();
            }
        };

        try { await dialog.ShowAsync(); }
        catch { }
    }
}
