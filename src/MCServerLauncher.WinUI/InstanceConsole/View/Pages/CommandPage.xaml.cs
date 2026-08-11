using System.Collections.Specialized;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.InstanceConsole.Modules;

namespace MCServerLauncher.WinUI.InstanceConsole.View.Pages;

public sealed partial class CommandPage : UserControl, INotifyPropertyChanged
{
    private InstanceStatus _status = InstanceStatus.Stopped;
    private bool _isFullscreen;
    private ConsoleLogStore? _logStore;
    private ScrollViewer? _logScroller;
    private bool _isPinnedToBottom = true;
    private bool _scrollerWired;

    public CommandPage()
    {
        InitializeComponent();
        LogList.Loaded += LogList_Loaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public TextBox Input => CommandTextBox;
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

    /// <summary>
    /// Wires the page to the console's log store. The store is owned by the console view /
    /// data manager; the page only renders its <see cref="ConsoleLogStore.Display"/> window.
    /// </summary>
    public void BindLogStore(ConsoleLogStore store)
    {
        if (_logStore is not null) _logStore.Display.CollectionChanged -= LogCollection_CollectionChanged;
        _logStore = store;
        LogList.ItemsSource = store.Display;
        store.Display.CollectionChanged += LogCollection_CollectionChanged;
    }

    /// <summary>Appends a log line through the bound store. Must be called on the UI thread.</summary>
    public void AppendLog(string text) => _logStore?.Append(text);

    private void LogList_Loaded(object sender, RoutedEventArgs e)
    {
        if (_scrollerWired) return;
        _scrollerWired = true;
        _logScroller = FindDescendantScrollViewer(LogList);
        if (_logScroller is not null)
            _logScroller.ViewChanged += LogScroller_ViewChanged;
        ScrollToBottom();
    }

    private void LogCollection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _isPinnedToBottom)
            ScrollToBottom();
    }

    private void LogScroller_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_logScroller is null) return;
        _isPinnedToBottom = _logScroller.VerticalOffset >= _logScroller.ScrollableHeight - 8;
    }

    private void ScrollToBottom()
    {
        _logScroller ??= FindDescendantScrollViewer(LogList);
        if (_logScroller is null || _logScroller.ScrollableHeight <= 0) return;
        _logScroller.ChangeView(null, _logScroller.ScrollableHeight, null, disableAnimation: true);
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer scroller) return scroller;
            if (FindDescendantScrollViewer(child) is { } nested) return nested;
        }
        return null;
    }

    private string GetAllLogText() =>
        _logStore is null
            ? string.Empty
            : string.Join(Environment.NewLine, _logStore.Snapshot().Select(entry => entry.Text));

    private void SendCommand_Click(object sender, RoutedEventArgs e) => SendCommandRequested?.Invoke(this, EventArgs.Empty);

    private void Start_Click(object sender, RoutedEventArgs e) => StartRequested?.Invoke(this, EventArgs.Empty);
    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);
    private void Restart_Click(object sender, RoutedEventArgs e) => RestartRequested?.Invoke(this, EventArgs.Empty);
    private void Kill_Click(object sender, RoutedEventArgs e) => KillRequested?.Invoke(this, EventArgs.Empty);
    private void ToggleFullscreen_Click(object sender, RoutedEventArgs e) => FullscreenRequested?.Invoke(this, EventArgs.Empty);

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        var text = LogList.SelectedItem is LogEntry selected
            ? selected.Text
            : GetAllLogText();
        if (text.Length > 0) App.Services.Clipboard.SetText(text);
    }

    private void SelectAllLog_Click(object sender, RoutedEventArgs e)
    {
        var text = GetAllLogText();
        if (text.Length > 0) App.Services.Clipboard.SetText(text);
    }

    private void CommandTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        SendCommandRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
