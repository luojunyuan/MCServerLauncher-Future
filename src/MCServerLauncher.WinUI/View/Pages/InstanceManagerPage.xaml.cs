using System.ComponentModel;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Models;
using MCServerLauncher.WinUI.ViewModels;

namespace MCServerLauncher.WinUI.Views.Pages;

public sealed partial class InstanceManagerPage : Page
{
    private DispatcherQueueTimer? _refreshTimer;
    private bool _isPageLoaded;

    public InstanceManagerPage()
    {
        NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;
        ViewModel = new InstanceManagerViewModel(
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

    public InstanceManagerViewModel ViewModel { get; }
    public LocalizedStrings Texts => App.Services.Localization.Texts;

    public Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = true;
        ViewModel.Attach();
        ViewModel.LoadDaemonFilterItems();
        await ViewModel.RefreshAsync();
        UpdateErrorState();
        StartAutoRefresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = false;
        StopAutoRefresh();
        ViewModel.Detach();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(InstanceManagerViewModel.ErrorState):
                UpdateErrorState();
                break;
            case nameof(InstanceManagerViewModel.AutoRefreshEnabled):
            case nameof(InstanceManagerViewModel.RefreshIntervalSeconds):
                StartAutoRefresh();
                break;
        }
    }

    private void StartAutoRefresh()
    {
        if (!_isPageLoaded || !ViewModel.AutoRefreshEnabled)
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

    private async void DaemonFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPageLoaded) await ViewModel.RefreshAsync();
    }

    private void OpenConsole_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is InstanceCardModel card)
            ViewModel.OpenConsole(card);
    }

    private async void StartInstance_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not InstanceCardModel card) return;
        if (await ConfirmAsync(card, "InstanceCard_StartConfirmTitle", "InstanceCard_StartConfirmContent", "Start"))
            await ViewModel.StartInstanceAsync(card);
    }

    private async void StopInstance_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not InstanceCardModel card) return;
        if (await ConfirmAsync(card, "InstanceCard_StopConfirmTitle", "InstanceCard_StopConfirmContent", "Stop"))
            await ViewModel.StopInstanceAsync(card);
    }

    private async void RestartInstance_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not InstanceCardModel card) return;
        if (await ConfirmAsync(card, "InstanceCard_RestartConfirmTitle", "InstanceCard_RestartConfirmContent", "Restart"))
            await ViewModel.RestartInstanceAsync(card);
    }

    private async void KillInstance_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not InstanceCardModel card || XamlRoot is null) return;
        var confirmed = await App.Services.Dialogs.ConfirmCountdownAsync(
            XamlRoot,
            Texts["InstanceCard_KillConfirmTitle"],
            string.Format(Texts["InstanceCard_KillConfirmContent"], card.InstanceName),
            Texts["Kill"],
            Texts["Cancel"],
            isDestructive: true);
        if (confirmed) await ViewModel.KillInstanceAsync(card);
    }

    private async void DeleteInstance_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not InstanceCardModel card) return;
        if (await ConfirmAsync(card, "InstanceCard_DeleteConfirmTitle", "InstanceCard_DeleteConfirmContent", "Delete", isDestructive: true))
            await ViewModel.DeleteInstanceAsync(card);
    }

    private async Task<bool> ConfirmAsync(
        InstanceCardModel card,
        string titleKey,
        string contentKey,
        string actionKey,
        bool isDestructive = false)
    {
        if (XamlRoot is null) return false;
        return await App.Services.Dialogs.ConfirmAsync(
            XamlRoot,
            Texts[titleKey],
            string.Format(Texts[contentKey], card.InstanceName),
            Texts[actionKey],
            Texts["Cancel"],
            isDestructive);
    }

    private async void ErrorAction_Click(object sender, RoutedEventArgs e)
    {
        switch (ViewModel.ErrorState)
        {
            case "no_daemon":
                App.Window.RootPage.Navigate(typeof(DaemonManagerPage), DaemonManagerPage.OpenConnectionParameter);
                break;
            case "load_error":
                await ViewModel.RefreshAsync();
                break;
        }
    }

    private void UpdateErrorState()
    {
        ErrorLayer.Visibility = Visibility.Collapsed;
        InstanceScrollViewer.Visibility = Visibility.Visible;
        FilterBar.Visibility = Visibility.Visible;
        ErrorActionButton.IsEnabled = true;

        switch (ViewModel.ErrorState)
        {
            case "no_daemon":
                FilterBar.Visibility = Visibility.Collapsed;
                ShowError("❌", Texts["FuncDisabled"], Texts["FuncDisabledReason_NoDaemon"], Texts["ConnectDaemon"]);
                break;
            case "no_instance":
                ErrorActionButton.IsEnabled = false;
                ShowError("🤔", Texts["NothingHere"], Texts["TryAddSomething"], Texts["Main_CreateInstanceNavMenu"]);
                break;
            case "load_error":
                ShowError("❌", Texts["ConnectDaemonFailedTip"], Texts["ConnectDaemonFailedSubTip"], Texts["Refresh"]);
                break;
        }
    }

    private void ShowError(string symbol, string title, string description, string action)
    {
        InstanceScrollViewer.Visibility = Visibility.Collapsed;
        ErrorSymbol.Text = symbol;
        ErrorTitle.Text = title;
        ErrorDescription.Text = description;
        ErrorActionButton.Content = action;
        ErrorLayer.Visibility = Visibility.Visible;
    }
}
