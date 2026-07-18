using MCServerLauncher.DaemonClient;
using MCServerLauncher.WinUI.Models;
using Serilog;

namespace MCServerLauncher.WinUI.InstanceConsole.Modules;

public sealed record ComponentScanResult(
    bool HasMods,
    bool HasPlugins,
    IReadOnlyList<ComponentFileModel> Mods,
    IReadOnlyList<ComponentFileModel> Plugins)
{
    public bool SupportsComponents => HasMods || HasPlugins;
}

public static class ComponentScanner
{
    public static async Task<ComponentScanResult> ScanAsync(IDaemon daemon, Guid instanceId)
    {
        var root = $"/instances/{instanceId}";
        var hasMods = await ExistsAsync(daemon, $"{root}/mods");
        var hasPlugins = await ExistsAsync(daemon, $"{root}/plugins");
        var mods = hasMods ? await LoadAsync(daemon, $"{root}/mods", ComponentKind.Mod) : [];
        var plugins = hasPlugins ? await LoadAsync(daemon, $"{root}/plugins", ComponentKind.Plugin) : [];
        return new ComponentScanResult(hasMods, hasPlugins, mods, plugins);
    }

    public static async Task RenameAsync(IDaemon daemon, ComponentFileModel item, string newName)
    {
        await daemon.RenameFileAsync(item.VirtualPath, newName);
        var slash = item.VirtualPath.LastIndexOf('/');
        var folder = slash < 0 ? string.Empty : item.VirtualPath[..(slash + 1)];
        item.FileName = newName;
        item.VirtualPath = folder + newName;
        item.IsEnabled = !newName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
    }

    public static Task DisableAsync(IDaemon daemon, ComponentFileModel item) =>
        item.IsEnabled ? RenameAsync(daemon, item, item.FileName + ".disabled") : Task.CompletedTask;

    public static Task EnableAsync(IDaemon daemon, ComponentFileModel item)
    {
        if (item.IsEnabled) return Task.CompletedTask;
        var name = item.FileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? item.FileName[..^".disabled".Length]
            : item.FileName;
        return RenameAsync(daemon, item, name);
    }

    private static async Task<bool> ExistsAsync(IDaemon daemon, string path)
    {
        try { await daemon.GetDirectoryInfoAsync(path); return true; }
        catch { return false; }
    }

    private static async Task<List<ComponentFileModel>> LoadAsync(IDaemon daemon, string folder, ComponentKind kind)
    {
        var result = new List<ComponentFileModel>();
        var (_, files, _) = await daemon.GetDirectoryInfoAsync(folder);
        foreach (var file in files)
        {
            var name = file.Name;
            var lower = name.ToLowerInvariant();
            if (!lower.EndsWith(".jar", StringComparison.Ordinal) && !lower.EndsWith(".jar.disabled", StringComparison.Ordinal)) continue;
            var item = new ComponentFileModel
            {
                FileName = name,
                VirtualPath = $"{folder}/{name}",
                FileSize = file.Meta.Size,
                IsEnabled = !lower.EndsWith(".disabled", StringComparison.Ordinal),
                Kind = kind
            };
            var temp = Path.Combine(Path.GetTempPath(), $"mcsl_jar_{Guid.NewGuid():N}_{name}");
            try
            {
                var download = await daemon.DownloadFileAsync(item.VirtualPath, temp, 1024 * 1024);
                if (download.NetworkLoadTask is not null) await download.NetworkLoadTask;
                var metadata = JarMetadataParser.Parse(temp);
                if (metadata is not null)
                {
                    item.DisplayName = metadata.DisplayName;
                    item.Version = metadata.Version;
                    item.IsClientSideOnly = metadata.IsClientSideOnly;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[WinUI] Failed to read component metadata {Path}", item.VirtualPath);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
            result.Add(item);
        }
        return result;
    }
}

public enum ComponentKind
{
    Mod,
    Plugin
}
