using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.PreCreate;

public sealed partial class PreCreateInstance : UserControl
{
    private readonly CreateInstancePage _owner;

    public PreCreateInstance(CreateInstancePage owner)
    {
        _owner = owner;
        InitializeComponent();
    }

    public LocalizedStrings Texts => App.Services.Localization.Texts;

    private async void Minecraft_Click(object sender, RoutedEventArgs e) =>
        await _owner.ShowMinecraftTypesAsync();

    private async void Terraria_Click(object sender, RoutedEventArgs e) =>
        await _owner.OpenProviderAsync((session) => new CreateTerrariaInstanceProvider(_owner, session));

    private async void Other_Click(object sender, RoutedEventArgs e) =>
        await _owner.OpenProviderAsync((session) => new CreateOtherExecutableInstanceProvider(_owner, session));
}
