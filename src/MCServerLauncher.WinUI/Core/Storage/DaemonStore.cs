using System.Text.Json;
using MCServerLauncher.WinUI.Models;
using Serilog;

namespace MCServerLauncher.WinUI.Core.Storage;

public sealed class DaemonStore
{
    private readonly StoragePaths _paths;
    private readonly object _gate = new();
    private List<DaemonConfigModel> _items;

    public DaemonStore(StoragePaths paths)
    {
        _paths = paths;
        _items = Load();
    }

    public IReadOnlyList<DaemonConfigModel> Items
    {
        get
        {
            lock (_gate) return _items.Select(Clone).ToArray();
        }
    }

    public void Add(DaemonConfigModel config)
    {
        lock (_gate)
        {
            _items.Add(Clone(config));
            SaveLocked();
        }
    }

    public void Remove(DaemonConfigModel config)
    {
        lock (_gate)
        {
            _items.RemoveAll(item => Same(item, config));
            SaveLocked();
        }
    }

    public void Replace(DaemonConfigModel original, DaemonConfigModel replacement)
    {
        lock (_gate)
        {
            var index = _items.FindIndex(item => Same(item, original));
            if (index >= 0) _items[index] = Clone(replacement);
            SaveLocked();
        }
    }

    private List<DaemonConfigModel> Load()
    {
        try
        {
            if (File.Exists(_paths.DaemonsFile))
            {
                var loaded = JsonSerializer.Deserialize(
                    File.ReadAllBytes(_paths.DaemonsFile), WinUiJsonContext.Default.ListDaemonConfigModel);
                if (loaded is not null) return loaded;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Failed to read daemon list, using an empty list");
        }

        var defaults = new List<DaemonConfigModel>();
        try
        {
            Directory.CreateDirectory(_paths.ConfigurationRoot);
            AtomicWrite(
                _paths.DaemonsFile,
                JsonSerializer.SerializeToUtf8Bytes(defaults, WinUiJsonContext.Default.ListDaemonConfigModel));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Failed to create default daemon list");
        }

        return defaults;
    }

    private void SaveLocked()
    {
        try
        {
            Directory.CreateDirectory(_paths.ConfigurationRoot);
            AtomicWrite(
                _paths.DaemonsFile,
                JsonSerializer.SerializeToUtf8Bytes(_items, WinUiJsonContext.Default.ListDaemonConfigModel));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Failed to save daemon list");
        }
    }

    private static void AtomicWrite(string path, byte[] payload)
    {
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
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

    private static bool Same(DaemonConfigModel left, DaemonConfigModel right) =>
        string.Equals(left.EndPoint, right.EndPoint, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port
        && left.IsSecure == right.IsSecure
        && string.Equals(left.Token, right.Token, StringComparison.Ordinal)
        && string.Equals(left.FriendlyName, right.FriendlyName, StringComparison.Ordinal);

    private static DaemonConfigModel Clone(DaemonConfigModel source) => new()
    {
        EndPoint = source.EndPoint,
        Port = source.Port,
        Token = source.Token,
        FriendlyName = source.FriendlyName,
        IsSecure = source.IsSecure
    };
}
