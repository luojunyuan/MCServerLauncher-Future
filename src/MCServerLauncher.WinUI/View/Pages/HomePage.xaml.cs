using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.Views.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
    }

    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public string AnnouncementText =>
        "MCServerLauncher Future 是 MCSL开发组 全新的项目！\n" +
        "本客户端仅仅是其中的一部分，需要 Daemon 配合使用！\n" +
        "同时我们也在制作 Web 版，可作为桌面客户端的替代！";
}
