using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core;
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

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        OnLoadedCore().FireAndForget("InstanceManagerPage.OnLoaded");

    private async Task OnLoadedCore()
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

    private void RefreshTimer_Tick(DispatcherQueueTimer sender, object args) =>
        ViewModel.AutoRefreshAsync().FireAndForget("InstanceManagerPage.RefreshTimer_Tick");

    private void DaemonFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        DaemonFilter_SelectionChangedCore().FireAndForget("InstanceManagerPage.DaemonFilter_SelectionChanged");

    private async Task DaemonFilter_SelectionChangedCore()
    {
        if (_isPageLoaded) await ViewModel.RefreshAsync();
    }

    private void OpenConsole_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is InstanceCardModel card)
            ViewModel.OpenConsole(card);
    }

    private void StartInstance_Click(object sender, RoutedEventArgs e) =>
        StartInstance_ClickCore(sender).FireAndForget("InstanceManagerPage.StartInstance_Click");

    private async Task StartInstance_ClickCore(object sender)
    {
        if ((sender as FrameworkElement)?.Tag is not InstanceCardModel card) return;
        if (await ConfirmAsync(card, "InstanceCard_StartConfirmTitle", "InstanceCard_StartConfirmContent", "Start"))
            await ViewModel.StartInstanceAsync(card);
    }

    private void StopInstance_Click(object sender, RoutedEventArgs e) =>
        StopInstance_ClickCore(sender).FireAndForget("InstanceManagerPage.StopInstance_Click");

    private async Task StopInstance_ClickCore(object sender)
    {
        if ((sender as FrameworkElement)?.Tag is not InstanceCardModel card) return;
        if (await ConfirmAsync(card, "InstanceCard_StopConfirmTitle", "InstanceCard_StopConfirmContent", "Stop"))
            await ViewModel.StopInstanceAsync(card);
    }

    private void RestartInstance_Click(object sender, RoutedEventArgs e) =>
        RestartInstance_ClickCore(sender).FireAndForget("InstanceManagerPage.RestartInstance_Click");

    private async Task RestartInstance_ClickCore(object sender)
    {
        if ((sender as FrameworkElement)?.Tag is not InstanceCardModel card) return;
        if (await ConfirmAsync(card, "InstanceCard_RestartConfirmTitle", "InstanceCard_RestartConfirmContent", "Restart"))
            await ViewModel.RestartInstanceAsync(card);
    }

    private void KillInstance_Click(object sender, RoutedEventArgs e) =>
        KillInstance_ClickCore(sender).FireAndForget("InstanceManagerPage.KillInstance_Click");

    private async Task KillInstance_ClickCore(object sender)
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

    private void DeleteInstance_Click(object sender, RoutedEventArgs e) =>
        DeleteInstance_ClickCore(sender).FireAndForget("InstanceManagerPage.DeleteInstance_Click");

    private async Task DeleteInstance_ClickCore(object sender)
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

    private ICommand? _connectDaemonCommand;
    private ICommand? _createInstanceCommand;

    private ICommand ConnectDaemonCommand => _connectDaemonCommand ??= new RelayCommand(() =>
        App.Window.RootPage.Navigate(typeof(DaemonManagerPage), DaemonManagerPage.OpenConnectionParameter));

    private ICommand CreateInstanceCommand => _createInstanceCommand ??= new RelayCommand(() =>
        App.Window.RootPage.Navigate(typeof(CreateInstancePage)));

    private void UpdateErrorState()
    {
        TipLayer.Visibility = Visibility.Collapsed;
        InstanceScrollViewer.Visibility = Visibility.Visible;
        FilterBar.Visibility = Visibility.Visible;

        switch (ViewModel.ErrorState)
        {
            case "no_daemon":
                FilterBar.Visibility = Visibility.Collapsed;
                ShowTip("❌", Texts["FuncDisabled"], Texts["FuncDisabledReason_NoDaemon"], Texts["ConnectDaemon"], ConnectDaemonCommand);
                break;
            case "no_instance":
                ShowTip("🤔", Texts["NothingHere"], Texts["TryAddSomething"], Texts["Main_CreateInstanceNavMenu"], CreateInstanceCommand);
                break;
            case "load_error":
                ShowTip("❌", Texts["ConnectDaemonFailedTip"], Texts["ConnectDaemonFailedSubTip"], Texts["Refresh"], ViewModel.RefreshCommand);
                break;
        }
    }

    private void ShowTip(string symbol, string title, string description, string buttonText, ICommand? command)
    {
        InstanceScrollViewer.Visibility = Visibility.Collapsed;
        TipLayer.Symbol = symbol;
        TipLayer.StopTip = title;
        TipLayer.StopDescription = description;
        TipLayer.ButtonText = buttonText;
        TipLayer.ButtonCommand = command;
        TipLayer.Visibility = Visibility.Visible;
    }
}
