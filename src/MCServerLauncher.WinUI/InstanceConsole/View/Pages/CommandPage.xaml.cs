using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;

namespace MCServerLauncher.WinUI.InstanceConsole.View.Pages;

public sealed partial class CommandPage : UserControl, INotifyPropertyChanged
{
    private InstanceStatus _status = InstanceStatus.Stopped;
    private bool _isFullscreen;

    public CommandPage() => InitializeComponent();

    public event PropertyChangedEventHandler? PropertyChanged;
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public TextBox Input => CommandTextBox;
    public TextBlock Output => LogOutput;
    public ScrollViewer LogViewer => LogScrollViewer;
    public event EventHandler? SendCommandRequested;
    public event EventHandler? StartRequested;
    public event EventHandler? StopRequested;
    public event EventHandler? RestartRequested;
    public event EventHandler? KillRequested;
    public event EventHandler? FullscreenRequested;

    public bool CanSendCommand => _status == InstanceStatus.Running;
    public bool CanStart => _status is InstanceStatus.Stopped or InstanceStatus.Crashed;
    public bool CanStop => _status == InstanceStatus.Running;
    public bool CanRestart => _status == InstanceStatus.Running;
    public bool CanKill => _status == InstanceStatus.Running;
    public string FullscreenText => Texts[_isFullscreen
        ? "ConsoleCommand_ExitFullScreenConsole"
        : "ConsoleCommand_EnterFullScreenConsole"];

    public void UpdateStatus(InstanceStatus status)
    {
        _status = status;
        OnPropertyChanged(nameof(CanSendCommand));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRestart));
        OnPropertyChanged(nameof(CanKill));
    }

    public void SetFullscreen(bool value)
    {
        if (_isFullscreen == value) return;
        _isFullscreen = value;
        OnPropertyChanged(nameof(FullscreenText));
    }

    public void AppendLog(string text)
    {
        LogOutput.Text += text + Environment.NewLine;
        LogScrollViewer.ChangeView(null, double.MaxValue, null);
    }

    private void SendCommand_Click(object sender, RoutedEventArgs e) => SendCommandRequested?.Invoke(this, EventArgs.Empty);

    private void Start_Click(object sender, RoutedEventArgs e) => StartRequested?.Invoke(this, EventArgs.Empty);
    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);
    private void Restart_Click(object sender, RoutedEventArgs e) => RestartRequested?.Invoke(this, EventArgs.Empty);
    private void Kill_Click(object sender, RoutedEventArgs e) => KillRequested?.Invoke(this, EventArgs.Empty);
    private void ToggleFullscreen_Click(object sender, RoutedEventArgs e) => FullscreenRequested?.Invoke(this, EventArgs.Empty);

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(LogOutput.SelectedText)) return;
        App.Services.Clipboard.SetText(LogOutput.SelectedText);
    }

    private void SelectAllLog_Click(object sender, RoutedEventArgs e) => LogOutput.SelectAll();

    private void CommandTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        SendCommandRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
