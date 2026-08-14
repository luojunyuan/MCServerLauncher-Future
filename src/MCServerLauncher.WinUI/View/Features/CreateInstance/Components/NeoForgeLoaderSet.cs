using MCServerLauncher.Common.Minecraft.InstallSource;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public sealed partial class NeoForgeLoaderSet : LoaderSetStep
{
    private List<string> _versions = [];
    private List<string> _minecraft = [];

    public NeoForgeLoaderSet() : base("NeoForgeVersion", "CreateInstance_NeoForgeVersion_Description", showStableMinecraft: false) { }

    protected override async Task FetchMinecraftVersionsAsync()
    {
        var data = await NeoForge.GetData(UseMirror("NeoForge"));
        _versions = data.NeoForgeVersions ?? [];
        _minecraft = data.MinecraftVersions ?? [];
        SetMinecraftVersions(_minecraft);
    }

    protected override Task FetchLoaderVersionsAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedMinecraftVersion)) return Task.CompletedTask;
        var values = SelectedMinecraftVersion == "1.20.1"
            ? _versions.Where(value => value.StartsWith("47", StringComparison.Ordinal)).ToList()
            : _versions.Where(value => value.StartsWith(SelectedMinecraftVersion.Length > 2 ? SelectedMinecraftVersion[2..] : SelectedMinecraftVersion, StringComparison.Ordinal)).ToList();
        SetLoaderVersions(values);
        LoaderVersionBox.IsEnabled = true;
        return Task.CompletedTask;
    }

    protected override Task MinecraftVersionChangedAsync() => RefreshLoaderAsync();
}
