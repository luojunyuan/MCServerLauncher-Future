using Serilog;

namespace MCServerLauncher.WinUI.Core;

/// <summary>
/// Helpers for fire-and-forget tasks. Replaces bare `async void` handlers:
/// keep the event handler as a plain <see langword="void"/> that calls an
/// <c>async Task</c> method and routes errors through <see cref="FireAndForget"/>.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Observes a fire-and-forget task and logs any unhandled failure. The
    /// continuation runs on a thread-pool thread, so it is safe to call from
    /// event handlers regardless of the UI synchronization context.
    /// </summary>
    /// <param name="task">The task to observe. Must not be null.</param>
    /// <param name="operation">Optional human-readable description used in the log line.</param>
    public static void FireAndForget(this Task task, string? operation = null)
    {
        _ = task.ContinueWith(
            static (completed, state) =>
                Log.Error(completed.Exception, "[WinUI] Fire-and-forget task failed: {Operation}", state),
            operation,
            TaskContinuationOptions.OnlyOnFaulted);
    }
}
