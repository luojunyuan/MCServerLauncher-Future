using System.Diagnostics;
using System.Security.Principal;
using Serilog;

namespace MCServerLauncher.WinUI.Core.Services;

/// <summary>
///     Best-effort elevation helpers.
///     NOTE: WPF's Initializer auto-elevates (and restarts) at startup. WinUI
///     intentionally does NOT: relaunching under UAC would disrupt a running
///     instance. Everything here is opt-in only — callers must invoke
///     <see cref="RelaunchAsAdministrator"/> explicitly (e.g. from a settings
///     toggle backed by <see cref="SettingsStore"/>.Current.App.IsRunAsAdmin).
/// </summary>
public static class ElevationHelper
{
    /// <summary>True when the current process is running with administrator privileges.</summary>
    public static bool IsElevated
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return identity is not null
                    && new WindowsPrincipal(identity)
                        .IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[WinUI] Failed to determine elevation state");
                return false;
            }
        }
    }

    /// <summary>
    ///     Relaunches the current executable with administrator privileges via a
    ///     UAC prompt. The caller is responsible for exiting the current instance
    ///     if desired. Never throws.
    /// </summary>
    public static void RelaunchAsAdministrator()
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable)) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };

            var args = Environment.GetCommandLineArgs().Skip(1).ToList();
            if (args.Count > 0)
            {
                startInfo.Arguments = string.Join(" ", args.Select(QuoteArgument));
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            // Win32Exception is thrown when the user cancels the UAC prompt; the
            // current (unelevated) instance keeps running either way.
            Log.Warning(ex, "[WinUI] Failed to relaunch as administrator");
        }
    }

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
}
