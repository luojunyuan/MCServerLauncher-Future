using MCServerLauncher.Common.Minecraft.InstallSource;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public sealed class FabricLoaderSet : LoaderSetStep
{
    private List<Fabric.FabricUniversalVersion> _minecraft = [];
    private List<Fabric.FabricUniversalVersion> _loaders = [];

    public FabricLoaderSet() : base("FabricVersion", "CreateInstance_FabricVersion_Description", showStableMinecraft: true, showStableLoader: true) { }

    protected override async Task FetchMinecraftVersionsAsync()
    {
        _minecraft = await Fabric.GetMinecraftVersions(UseMirror("Fabric")) ?? [];
        SetMinecraftVersions(_minecraft.Select(value => value.Version));
    }

    protected override async Task FetchLoaderVersionsAsync()
    {
        _loaders = await Fabric.GetFabricVersions(UseMirror("Fabric")) ?? [];
        SetLoaderVersions(_loaders.Select(value => value.Version));
        LoaderVersionBox.IsEnabled = true;
    }

    protected override Task MinecraftVersionChangedAsync() => Task.CompletedTask;
}
