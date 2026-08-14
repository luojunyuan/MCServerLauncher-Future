using System.Collections.ObjectModel;
using System.Text.Json;
using MCServerLauncher.WinUI.Core.Storage;
using Serilog;

namespace MCServerLauncher.WinUI.InstanceConsole.Modules;

/// <summary>
/// A single console-log entry with a per-instance monotonic sequence id.
/// </summary>
public sealed record LogEntry(long Sequence, string Text, DateTimeOffset Timestamp);

/// <summary>
/// Bounded, per-instance console log buffer that provides data virtualization for the
/// console output.
///
/// The newest <see cref="MemoryCap"/> entries are kept in memory (the <see cref="Display"/>
/// collection that a virtualized ListView binds to). Entries evicted from that window are
/// staged in an in-memory <see cref="WriteBufferCap"/>-entry write buffer and written to the
/// per-instance JSONL cache file at <c>{LogsRoot}/Consoles/{instanceId}.jsonl</c> in a single
/// batch every time the buffer fills, which keeps syscalls and disk I/O low on chatty logs.
/// The cache file is append-only, so it retains the full evicted history for the instance.
///
/// Every entry carries a monotonically increasing per-instance <see cref="LogEntry.Sequence"/>
/// id. That id guarantees the retained window is complete and contiguous (no gaps or
/// duplicates) and, because each instance writes to its own file named by its instance id,
/// logs from different instances are fully isolated from one another.
///
/// The daemon is the authoritative store of full log history; <see cref="SeedHistory"/>
/// re-derives the window from that history each time a console opens, so reopening a console
/// never duplicates entries left in the cache file by a previous session.
///
/// All mutation methods must be called on the UI thread (the <see cref="Display"/> collection
/// raises <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> on the calling
/// thread). Batch file writes are synchronous but infrequent — at most one write per
/// <see cref="WriteBufferCap"/> evictions.
/// </summary>
public sealed class ConsoleLogStore : IDisposable
{
    /// <summary>Maximum number of entries held in memory and shown in the console.</summary>
    public const int MemoryCap = 1000;

    /// <summary>In-memory write-buffer size; flushed to the cache file in one batch when full.</summary>
    public const int WriteBufferCap = 1000;

    private readonly object _gate = new();
    private readonly string _filePath;
    private readonly List<LogEntry> _writeBuffer = new(WriteBufferCap);
    private long _sequence;

    public ConsoleLogStore(Guid instanceId, string logsRoot)
    {
        _filePath = Path.Combine(logsRoot, "Consoles", $"{instanceId}.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    /// <summary>The newest retained window (oldest first), at most <see cref="MemoryCap"/> entries.</summary>
    public ObservableCollection<LogEntry> Display { get; } = [];

    public string FilePath => _filePath;

    /// <summary>
    /// Replaces the whole window with authoritative daemon history and discards any cache
    /// file written by a previous session. Call once per console open, on the UI thread.
    /// </summary>
    public void SeedHistory(IEnumerable<string> lines)
    {
        lock (_gate)
        {
            Display.Clear();
            _sequence = 0;
            _writeBuffer.Clear();
            DeleteCacheFile();
        }

        foreach (var line in lines)
        {
            bool shouldFlush;
            lock (_gate) { shouldFlush = AppendCore(line); }
            if (shouldFlush) FlushBuffer();
        }

        // Persist the partial tail of the seed that has not filled the write buffer yet.
        FlushBuffer();
    }

    /// <summary>Appends one live log line. Must be called on the UI thread.</summary>
    public void Append(string text)
    {
        bool shouldFlush;
        lock (_gate) { shouldFlush = AppendCore(text); }
        if (shouldFlush) FlushBuffer();
    }

    /// <summary>Snapshot of the retained window, oldest first.</summary>
    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return Display.ToArray();
        }
    }

    public void Flush() => FlushBuffer();

    public void Dispose() => FlushBuffer();

    /// <summary>Returns true when the caller should flush the write buffer to disk.</summary>
    private bool AppendCore(string? text)
    {
        var entry = new LogEntry(++_sequence, text ?? string.Empty, DateTimeOffset.UtcNow);
        if (Display.Count == MemoryCap)
        {
            var evicted = Display[0];
            Display.RemoveAt(0);
            _writeBuffer.Add(evicted);
        }
        Display.Add(entry);
        return _writeBuffer.Count >= WriteBufferCap;
    }

    private void FlushBuffer()
    {
        List<LogEntry>? batch;
        lock (_gate)
        {
            if (_writeBuffer.Count == 0) return;
            batch = new List<LogEntry>(_writeBuffer);
            _writeBuffer.Clear();
        }

        try
        {
            File.AppendAllLines(_filePath, batch.Select(Serialize));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Failed to write console log cache {Path}", _filePath);
        }
    }

    private void DeleteCacheFile()
    {
        try
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[WinUI] Failed to delete console log cache {Path}", _filePath);
        }
    }

    private static string Serialize(LogEntry entry) =>
        JsonSerializer.Serialize(entry, WinUiJsonContext.Default.LogEntry);
}
