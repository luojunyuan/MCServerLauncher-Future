using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core;
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

    private void Minecraft_Click(object sender, RoutedEventArgs e) =>
        _owner.ShowMinecraftTypesAsync().FireAndForget("PreCreateInstance.Minecraft_Click");

    private void Terraria_Click(object sender, RoutedEventArgs e) =>
        _owner.OpenProviderAsync((session) => new CreateTerrariaInstanceProvider(_owner, session))
            .FireAndForget("PreCreateInstance.Terraria_Click");

    private void Other_Click(object sender, RoutedEventArgs e) =>
        _owner.OpenProviderAsync((session) => new CreateOtherExecutableInstanceProvider(_owner, session))
            .FireAndForget("PreCreateInstance.Other_Click");
}
