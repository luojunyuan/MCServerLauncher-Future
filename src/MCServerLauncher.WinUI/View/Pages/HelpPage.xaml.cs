using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.Views.Pages;

public sealed partial class HelpPage : Page
{
    public HelpPage()
    {
        NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
    }
    public LocalizedStrings Texts => App.Services.Localization.Texts;
}
