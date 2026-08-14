using MCServerLauncher.Common.Minecraft.InstallSource;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public sealed partial class ForgeLoaderSet : LoaderSetStep
{
    private List<Forge.ForgeBuild> _builds = [];

    public ForgeLoaderSet() : base("ForgeVersion", "CreateInstance_ForgeVersion_Description", showStableMinecraft: false) { }

    protected override async Task FetchMinecraftVersionsAsync()
    {
        SetMinecraftVersions(await Forge.GetMinecraftVersions(UseMirror("Forge")) ?? []);
    }

    protected override async Task FetchLoaderVersionsAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedMinecraftVersion)) return;
        _builds = await Forge.GetForgeVersions(SelectedMinecraftVersion, UseMirror("Forge")) ?? [];
        SetLoaderVersions(_builds.Select(build => build.ForgeVersion));
        LoaderVersionBox.IsEnabled = true;
    }

    protected override Task MinecraftVersionChangedAsync() => RefreshLoaderAsync();
}
