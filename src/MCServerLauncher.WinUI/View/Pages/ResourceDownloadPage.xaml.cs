using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using MCServerLauncher.WinUI.Core;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Models;
using MCServerLauncher.WinUI.ViewModels;

namespace MCServerLauncher.WinUI.Views.Pages;

public sealed partial class ResourceDownloadPage : Page
{
    public ResourceDownloadPage()
    {
        NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;
        ViewModel = new ResourceDownloadViewModel(
            App.Services.Paths,
            App.Services.Daemons,
            App.Services.DaemonConnections,
            App.Services.Localization,
            App.Services.Notifications);
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    public ResourceDownloadViewModel ViewModel { get; }
    public LocalizedStrings Texts => App.Services.Localization.Texts;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Attach();
        ViewModel.RefreshAsync().FireAndForget("ResourceDownloadPage.OnLoaded");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => ViewModel.Detach();

    private void Refresh_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RefreshAsync().FireAndForget("ResourceDownloadPage.Refresh_Click");

    private void Core_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.SelectCoreAsync((sender as ListView)?.SelectedItem as ResourceCoreItem)
            .FireAndForget("ResourceDownloadPage.Core_SelectionChanged");

    private void MinecraftVersion_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.SelectVersionAsync((sender as ComboBox)?.SelectedItem?.ToString())
            .FireAndForget("ResourceDownloadPage.MinecraftVersion_SelectionChanged");

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ResourceVersionItem item && XamlRoot is not null)
            ViewModel.DownloadItemAsync(item, XamlRoot).FireAndForget("ResourceDownloadPage.Download_Click");
    }

    private void HomePage_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string url &&
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
            OpenHomePageAsync(uri).FireAndForget("ResourceDownloadPage.HomePage_Click");
    }

    private async Task OpenHomePageAsync(Uri uri) =>
        await Windows.System.Launcher.LaunchUriAsync(uri);

    /// <summary>
    ///     x:Bind helper: golden gradient background for recommended cores (e.g. Arclight,
    ///     Paper in FastMirror). Ordinary cores use the card background applied in XAML
    ///     via {ThemeResource CardBackgroundFillColorDefaultBrush}.
    /// </summary>
    public static Brush? RecommendBackground(bool recommend) =>
        recommend
            ? new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0.5, 0.85),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0xFF, 0xF3, 0xBC, 0x00), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0xFF, 0xEF, 0x95, 0x00), Offset = 1 }
                }
            }
            : null;

    /// <summary>
    ///     x:Bind helper: shows the golden recommend highlight and the white name overlay
    ///     only for recommended cores.
    /// </summary>
    public static Visibility RecommendVisibility(bool recommend) =>
        recommend ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    ///     x:Bind helper: collapses a core-card line/button while its homepage URL is empty.
    /// </summary>
    public static Visibility StringToVisibility(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    ///     x:Bind helper: localized "open home page" text, reusing the shared resource key.
    /// </summary>
    public static string OpenHomePageText() => App.Services.Localization.Texts["ResDownload_OpenHomePage"];
}
