using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }
    public ResourceDownloadViewModel ViewModel { get; }
    public LocalizedStrings Texts => App.Services.Localization.Texts;

    private async void Provider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.SelectedProviderIndex < 0) return;
        await ViewModel.SelectProviderAsync(ViewModel.SelectedProviderIndex);
        await ViewModel.RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshAsync();

    private async void Core_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        await ViewModel.SelectCoreAsync((sender as ListView)?.SelectedItem as ResourceCoreItem);

    private async void MinecraftVersion_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        await ViewModel.SelectVersionAsync((sender as ComboBox)?.SelectedItem?.ToString());

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ResourceVersionItem item && XamlRoot is not null)
            await ViewModel.DownloadItemAsync(item, XamlRoot);
    }

    private async void HomePage_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string url &&
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
            await Windows.System.Launcher.LaunchUriAsync(uri);
    }

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
