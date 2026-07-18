using MCServerLauncher.DaemonClient;
using MCServerLauncher.WinUI.Models;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Models;

public sealed record CreateInstanceSession(DaemonConfigModel DaemonConfig, IDaemon Daemon);
