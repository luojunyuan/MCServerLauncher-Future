using MCServerLauncher.Common.Minecraft.InstallSource;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public sealed partial class QuiltLoaderSet : LoaderSetStep
{
    private List<Quilt.QuiltMinecraftVersion> _minecraft = [];

    public QuiltLoaderSet() : base("QuiltVersion", "CreateInstance_QuiltVersion_Description", showStableMinecraft: true) { }

    protected override async Task FetchMinecraftVersionsAsync()
    {
        _minecraft = await Quilt.GetMinecraftVersions(UseMirror("Quilt")) ?? [];
        SetMinecraftVersions(_minecraft.Select(value => value.MinecraftVersion));
    }

    protected override async Task FetchLoaderVersionsAsync()
    {
        SetLoaderVersions(await Quilt.GetQuiltVersions(UseMirror("Quilt")) ?? []);
        LoaderVersionBox.IsEnabled = true;
    }

    protected override Task MinecraftVersionChangedAsync() => Task.CompletedTask;
}
