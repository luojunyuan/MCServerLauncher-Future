using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.PreCreate;

public sealed partial class PreCreateMinecraftInstance : UserControl
{
    private readonly CreateInstancePage _owner;
    private readonly CreateInstanceSession _session;

    public PreCreateMinecraftInstance(CreateInstancePage owner, CreateInstanceSession session)
    {
        _owner = owner;
        _session = session;
        InitializeComponent();
    }

    public LocalizedStrings Texts => App.Services.Localization.Texts;

    private void Back_Click(object sender, RoutedEventArgs e) => _owner.ShowPreCreate();
    private void Java_Click(object sender, RoutedEventArgs e) => _owner.OpenMinecraftProvider(_session, s => new CreateMinecraftJavaInstanceProvider(_owner, s));
    private void Forge_Click(object sender, RoutedEventArgs e) => _owner.OpenMinecraftProvider(_session, s => new CreateMinecraftForgeInstanceProvider(_owner, s));
    private void NeoForge_Click(object sender, RoutedEventArgs e) => _owner.OpenMinecraftProvider(_session, s => new CreateMinecraftNeoForgeInstanceProvider(_owner, s));
    private void Fabric_Click(object sender, RoutedEventArgs e) => _owner.OpenMinecraftProvider(_session, s => new CreateMinecraftFabricInstanceProvider(_owner, s));
    private void Quilt_Click(object sender, RoutedEventArgs e) => _owner.OpenMinecraftProvider(_session, s => new CreateMinecraftQuiltInstanceProvider(_owner, s));
    private void Bedrock_Click(object sender, RoutedEventArgs e) => _owner.OpenMinecraftProvider(_session, s => new CreateMinecraftBedrockInstanceProvider(_owner, s));
}
