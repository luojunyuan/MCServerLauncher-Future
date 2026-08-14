using System.Text.Json;
using MCServerLauncher.WinUI.Models;
using Serilog;

namespace MCServerLauncher.WinUI.Core.Storage;

public sealed class SettingsDocument
{
    public InstanceCreationSettings InstanceCreation { get; set; } = new();
    public ResDownloadSettings Download { get; set; } = new();
    public InstanceSettings Instance { get; set; } = new();
    public AppSettings App { get; set; } = new();
}

public sealed class InstanceCreationSettings
{
    public bool MinecraftJavaAutoAcceptEula { get; set; }
    public bool MinecraftJavaAutoSwitchOnlineMode { get; set; }
    public bool MinecraftBedrockAutoSwitchOnlineMode { get; set; }
    public bool UseMirrorForMinecraftForgeInstall { get; set; } = true;
    public bool UseMirrorForMinecraftNeoForgeInstall { get; set; } = true;
    public bool UseMirrorForMinecraftFabricInstall { get; set; } = true;
    public bool UseMirrorForMinecraftQuiltInstall { get; set; } = true;
}

public sealed class ResDownloadSettings
{
    public string DownloadSource { get; set; } = "FastMirror";
    public int ThreadCnt { get; set; } = 16;
    public string ActionWhenDownloadError { get; set; } = "stop";
}

public sealed class InstanceSettings
{
    public List<string?> FollowStart { get; set; } = [];
    public int AutoRefreshInterval { get; set; } = 3;
    public string ActionOnDoubleClick { get; set; } = "Console";
}

public sealed class AppSettings
{
    public string Theme { get; set; } = "auto";
    public string Language { get; set; } = "zh-CN";
    public bool FollowStartup { get; set; }
    public bool AutoCheckUpdate { get; set; } = true;
    public bool IsFontInstalled { get; set; }
    // Opt-in for ElevationHelper.RelaunchAsAdministrator. Default-off: unlike WPF's
    // Initializer, WinUI never auto-elevates at startup. The settings page can
    // surface this toggle; nothing here consumes it automatically.
    public bool IsRunAsAdmin { get; set; }
    public bool IsAppEulaAccepted { get; set; }
    public bool IsFirstSetupFinished { get; set; }
    public Dictionary<string, bool> HideTips { get; set; } = [];
}

public sealed class SettingsStore
{
    private readonly StoragePaths _paths;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public SettingsStore(StoragePaths paths)
    {
        _paths = paths;
        Current = Load();
    }

    public SettingsDocument Current { get; private set; }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            byte[] payload;
            lock (_gate)
            {
                payload = JsonSerializer.SerializeToUtf8Bytes(Current, WinUiJsonContext.Default.SettingsDocument);
            }

            await AtomicWriteAsync(_paths.SettingsFile, payload, cancellationToken);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private SettingsDocument Load()
    {
        try
        {
            if (File.Exists(_paths.SettingsFile))
            {
                var loaded = JsonSerializer.Deserialize(
                    File.ReadAllBytes(_paths.SettingsFile), WinUiJsonContext.Default.SettingsDocument);
                if (loaded is not null)
                {
                    Normalize(loaded);
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Failed to read settings, using defaults");
        }

        var defaults = new SettingsDocument();
        Normalize(defaults);
        try
        {
            Directory.CreateDirectory(_paths.ConfigurationRoot);
            AtomicWrite(
                _paths.SettingsFile,
                JsonSerializer.SerializeToUtf8Bytes(defaults, WinUiJsonContext.Default.SettingsDocument));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Failed to create default settings");
        }

        return defaults;
    }

    private static void Normalize(SettingsDocument document)
    {
        document.InstanceCreation ??= new();
        document.Download ??= new();
        document.Instance ??= new();
        document.App ??= new();
        document.Instance.FollowStart ??= [];
        document.App.HideTips ??= [];
        document.App.Language = string.IsNullOrWhiteSpace(document.App.Language) ? "zh-CN" : document.App.Language;
        document.App.Theme = string.IsNullOrWhiteSpace(document.App.Theme) ? "auto" : document.App.Theme;
    }

    private static async Task AtomicWriteAsync(string path, byte[] payload, CancellationToken cancellationToken)
    {
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(payload, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch
            {
                // Cleanup is best effort.
            }
        }
    }

    private static void AtomicWrite(string path, byte[] payload)
    {
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(temp, payload);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch
            {
                // Cleanup is best effort.
            }
        }
    }
}
