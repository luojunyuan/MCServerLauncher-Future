using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.ViewModels;

namespace MCServerLauncher.WinUI.Views.Pages;

public sealed partial class SettingsPage : Page
{
    private int _debugClickCount;

    public SettingsPage()
    {
        NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Serilog.Log.Debug("[WinUI] Settings page created");
        ViewModel = new SettingsViewModel(App.Services.Settings, App.Services.Localization);
        InitializeComponent();
        Loaded += (_, _) => ViewModel.Attach();
        Unloaded += (_, _) => ViewModel.Detach();
    }

    public SettingsViewModel ViewModel { get; }
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public string CopyrightText => "Copyright © 2022-2026 MCSLTeam. All rights reserved.";
    public string GitHubLabel => "GitHub";

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.LauncherThemeIndex < 0) return;
        App.Services.Themes.Apply(App.Window.RootPage, ViewModel.SelectedTheme);
    }

    private void SettingsTitle_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _debugClickCount++;
        if (_debugClickCount >= 5)
            App.Window.RootPage.ShowDebugItem();
    }

    private async void OpenUri_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string value
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return;

        try
        {
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            App.Services.Notifications.Push(
                Texts["Status_Error"],
                ex.Message,
                NotificationSeverity.Error);
        }
    }
}
