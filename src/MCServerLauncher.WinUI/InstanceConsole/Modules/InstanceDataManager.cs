using MCServerLauncher.Common.ProtoType.Event;
using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.DaemonClient;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.Models;
using Serilog;

namespace MCServerLauncher.WinUI.InstanceConsole.Modules;

/// <summary>
/// Owns the live report and event subscription for one console window.
/// </summary>
public sealed class InstanceDataManager : IAsyncDisposable
{
    private readonly IDaemonConnectionService _connections;
    private readonly DaemonConfigModel _config;
    private readonly Guid _instanceId;
    private readonly CancellationTokenSource _dispose = new();
    private IDaemon? _daemon;
    private Task? _refreshLoop;
    private bool _disposed;

    public InstanceDataManager(
        IDaemonConnectionService connections,
        DaemonConfigModel config,
        Guid instanceId)
    {
        _connections = connections;
        _config = config;
        _instanceId = instanceId;
    }

    public Guid InstanceId => _instanceId;
    public IDaemon? Daemon => _daemon;
    public InstanceReport? CurrentReport { get; private set; }
    public bool IsConnected => _daemon?.Online == true;
    public event EventHandler<InstanceReport?>? ReportUpdated;
    public event EventHandler<string>? LogReceived;

    public async Task InitializeAsync()
    {
        ThrowIfDisposed();
        _daemon = await _connections.GetAsync(_config, _dispose.Token)
            ?? throw new InvalidOperationException("Failed to connect to daemon.");

        _daemon.InstanceLogEvent += OnInstanceLog;
        await _daemon.SubscribeEvent(
            EventType.InstanceLog,
            new InstanceLogEventMeta { InstanceId = _instanceId });
        await RefreshAsync();
        _refreshLoop = RefreshLoopAsync(_dispose.Token);
    }

    public async Task RefreshAsync()
    {
        if (_disposed || _daemon?.Online != true) return;
        try
        {
            CurrentReport = await _daemon.GetInstanceReportAsync(_instanceId);
            ReportUpdated?.Invoke(this, CurrentReport);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Failed to refresh instance report {InstanceId}", _instanceId);
        }
    }

    public Task StartAsync() => RequireDaemon().StartInstanceAsync(_instanceId);
    public Task StopAsync() => RequireDaemon().StopInstanceAsync(_instanceId);
    public Task RestartAsync() => RequireDaemon().RestartInstanceAsync(_instanceId);
    public Task KillAsync() => RequireDaemon().KillInstanceAsync(_instanceId);
    public Task SendCommandAsync(string command) => RequireDaemon().SentToInstanceAsync(_instanceId, command);
    public Task<string[]> GetLogHistoryAsync() => RequireDaemon().GetInstanceLogHistoryAsync(_instanceId);
    public async Task<long?> GetDaemonLatencyAsync()
    {
        if (_disposed || _daemon?.Online != true) return null;
        try
        {
            return await _daemon.PingAsync();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[WinUI] Failed to read daemon latency {InstanceId}", _instanceId);
            return null;
        }
    }
    public Task<List<MCServerLauncher.Common.ProtoType.EventTrigger.EventRule>> GetEventRulesAsync() =>
        RequireDaemon().GetEventRulesAsync(_instanceId);
    public Task SaveEventRulesAsync(List<MCServerLauncher.Common.ProtoType.EventTrigger.EventRule> rules) =>
        RequireDaemon().SaveEventRulesAsync(_instanceId, rules);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _dispose.Cancel();

        if (_refreshLoop is not null)
        {
            try { await _refreshLoop; } catch (OperationCanceledException) { }
        }

        if (_daemon is not null)
        {
            _daemon.InstanceLogEvent -= OnInstanceLog;
            try
            {
                await _daemon.UnSubscribeEvent(
                    EventType.InstanceLog,
                    new InstanceLogEventMeta { InstanceId = _instanceId });
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[WinUI] Failed to unsubscribe instance log {InstanceId}", _instanceId);
            }
        }

        _dispose.Dispose();
        _daemon = null;
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await RefreshAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void OnInstanceLog(Guid instanceId, string text)
    {
        if (instanceId == _instanceId) LogReceived?.Invoke(this, text);
    }

    private IDaemon RequireDaemon() => _daemon?.Online == true
        ? _daemon
        : throw new InvalidOperationException("Daemon connection is unavailable.");

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InstanceDataManager));
    }
}
