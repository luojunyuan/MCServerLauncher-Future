using System.Text.Json.Serialization;
using MCServerLauncher.WinUI.InstanceConsole.Modules;
using MCServerLauncher.WinUI.Models;

namespace MCServerLauncher.WinUI.Core.Storage;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SettingsDocument))]
[JsonSerializable(typeof(List<DaemonConfigModel>))]
[JsonSerializable(typeof(List<DownloadHistoryItem>))]
[JsonSerializable(typeof(LogEntry))]
internal partial class WinUiJsonContext : JsonSerializerContext
{
}
