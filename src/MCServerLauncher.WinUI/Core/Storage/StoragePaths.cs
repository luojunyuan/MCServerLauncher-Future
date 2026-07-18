namespace MCServerLauncher.WinUI.Core.Storage;

public sealed class StoragePaths
{
    private StoragePaths(string dataRoot)
    {
        DataRoot = dataRoot;
        ConfigurationRoot = Path.Combine(dataRoot, "Configuration", "MCSL");
        LogsRoot = Path.Combine(dataRoot, "Logs", "WinUI");
        DownloadsRoot = Path.Combine(dataRoot, "Downloads");
        SettingsFile = Path.Combine(ConfigurationRoot, "Settings.json");
        DaemonsFile = Path.Combine(ConfigurationRoot, "Daemons.json");
    }

    public string DataRoot { get; }
    public string ConfigurationRoot { get; }
    public string LogsRoot { get; }
    public string DownloadsRoot { get; }
    public string SettingsFile { get; }
    public string DaemonsFile { get; }

    public static StoragePaths Initialize()
    {
        var legacyRoot = Path.Combine(AppContext.BaseDirectory, "Data");
        var legacyConfiguration = Path.Combine(legacyRoot, "Configuration", "MCSL");
        var legacySettings = Path.Combine(legacyConfiguration, "Settings.json");
        var legacyDaemons = Path.Combine(legacyConfiguration, "Daemons.json");

        var legacy = new StoragePaths(legacyRoot);
        if (CanWrite(legacy.ConfigurationRoot)) return legacy;

        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MCServerLauncher-Future",
            "Data");
        var paths = new StoragePaths(localRoot);
        Directory.CreateDirectory(paths.ConfigurationRoot);
        Directory.CreateDirectory(paths.LogsRoot);

        CopyLegacyFile(legacySettings, paths.SettingsFile);
        CopyLegacyFile(legacyDaemons, paths.DaemonsFile);
        return paths;
    }

    private static bool CanWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyLegacyFile(string source, string destination)
    {
        if (File.Exists(source) && !File.Exists(destination))
        {
            try
            {
                File.Copy(source, destination, overwrite: false);
            }
            catch
            {
                // A failed import is non-fatal; the stores will create defaults.
            }
        }
    }
}
