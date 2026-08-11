using System.ComponentModel;
using System.IO;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using MCServerLauncher.WinUI.Core;
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
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += (_, _) => ViewModel.Attach();
        Unloaded += (_, _) => ViewModel.Detach();
    }

    public SettingsViewModel ViewModel { get; }
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public string CopyrightText => "Copyright © 2022-2026 MCSLTeam. All rights reserved.";
    public string GitHubLabel => "GitHub";

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.LauncherThemeIndex))
            ApplyTheme();
    }

    private void ApplyTheme()
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

    private void OpenUri_Click(object sender, RoutedEventArgs e) =>
        OpenUri_ClickCore(sender).FireAndForget("SettingsPage.OpenUri_Click");

    private async Task OpenUri_ClickCore(object sender)
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

    /// <summary>
    ///     x:Bind helper: resolves an acknowledgment avatar (either "Resources/foo.jpg"
    ///     or a bare file name) to a BitmapImage from the output Resources directory.
    ///     A plain string → Image.Source x:Bind yields a base-less relative URI that
    ///     cannot resolve in the unpackaged host, so the file path is built explicitly.
    /// </summary>
    public static ImageSource? ToImageSource(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var fileName = path.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase)
                ? path["Resources/".Length..]
                : path;
            var fullPath = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
            return new BitmapImage(new Uri(fullPath.Replace('\\', '/')));
        }
        catch
        {
            return null;
        }
    }
}
