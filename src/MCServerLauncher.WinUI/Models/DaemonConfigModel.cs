using System.ComponentModel;

namespace MCServerLauncher.WinUI.Models;

public sealed class DaemonConfigModel : INotifyPropertyChanged
{
    public bool IsSecure { get; set; }
    public string? EndPoint { get; set; }
    public int Port { get; set; }
    public string? Token { get; set; }
    public string? FriendlyName { get; set; }

    public string EditText => App.Services.Localization.Get("Edit");
    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshLocalizedText() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditText)));

    public string DisplayName =>
        $"{FriendlyName ?? EndPoint ?? "Daemon"} [{(IsSecure ? "wss" : "ws")}://{EndPoint}:{Port}]";
}
