using System.Collections.Concurrent;
using MCServerLauncher.DaemonClient;
using MCServerLauncher.DaemonClient.Connection;
using MCServerLauncher.WinUI.Models;
using Serilog;

namespace MCServerLauncher.WinUI.Core.Services;

public sealed class DaemonConnectionService : IDaemonConnectionService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, IDaemon> _connections = new(StringComparer.Ordinal);

    public async Task<IDaemon?> GetAsync(DaemonConfigModel config, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.EndPoint) || config.Port <= 0 || string.IsNullOrWhiteSpace(config.Token))
        {
            return null;
        }

        var key = Key(config);
        if (_connections.TryGetValue(key, out var existing) && existing.Online)
        {
            return existing;
        }

        if (_connections.TryRemove(key, out var dead))
        {
            try
            {
                await dead.CloseAsync();
            }
            catch
            {
                // The dead connection is already unusable.
            }
            dead.Dispose();
        }

        try
        {
            var daemon = await Daemon.OpenAsync(
                config.EndPoint,
                config.Port,
                config.Token,
                config.IsSecure,
                new ClientConnectionConfig
                {
                    MaxFailCount = 3,
                    PendingRequestCapacity = 100,
                    HeartBeatTick = TimeSpan.FromSeconds(5),
                    PingTimeout = 5000
                },
                cancellationToken: cancellationToken);
            _connections[key] = daemon;
            Log.Information("[WinUI] Connected to daemon {Address}", config.DisplayName);
            return daemon;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Failed to connect to daemon {Address}", config.DisplayName);
            return null;
        }
    }

    public async Task RemoveAsync(DaemonConfigModel config)
    {
        if (!_connections.TryRemove(Key(config), out var daemon)) return;
        try
        {
            await daemon.CloseAsync();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[WinUI] Error closing daemon connection");
        }
        finally
        {
            daemon.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var pair in _connections.ToArray())
        {
            if (_connections.TryRemove(pair.Key, out var daemon))
            {
                try { await daemon.CloseAsync(); } catch { }
                daemon.Dispose();
            }
        }
    }

    private static string Key(DaemonConfigModel config) =>
        $"{config.FriendlyName}|{config.EndPoint}|{config.Port}|{config.IsSecure}|{config.Token}";
}
