using System.Text;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using MCServerLauncher.Common.ProtoType.Action;
using MCServerLauncher.Common.ProtoType.Event;
using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.DaemonClient;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.InstanceConsole.Editing;
using MCServerLauncher.WinUI.InstanceConsole.Modules;
using MCServerLauncher.WinUI.InstanceConsole.View.Pages;
using MCServerLauncher.WinUI.Models;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Components;
using Serilog;
using Windows.Storage.Streams;
using WinUIIslands;
using WinUIEditor;

namespace MCServerLauncher.WinUI.InstanceConsole;

public sealed partial class InstanceConsoleView : UserControl
{
    private const uint WmClose = 0x0010;
    private readonly InstanceConsoleWindow _hostWindow;
    private readonly DaemonConfigModel _daemonConfig;
    private readonly Guid _instanceId;
    private readonly bool _isDebugMode;
    private readonly InstanceDataManager _dataManager;
    private readonly IEditorAdapter _editor;
    private IDaemon? _daemon;
    private Encoding _encoding = Encoding.UTF8;
    private string? _temporaryFile;
    private int _zoom;
    private bool _initialized;
    private DispatcherQueueTimer? _latencyTimer;
    private MCServerLauncher.Common.ProtoType.Instance.InstanceType _instanceType;
    private InstanceStatus _instanceStatus = InstanceStatus.Stopped;
    private string _instanceName = string.Empty;
    private readonly List<string> _directoryHistory = ["/"];
    private int _directoryHistoryIndex;
    private bool _restoringDirectoryHistory;
    private bool _isFullscreen;
    private bool _closeAllowed;
    private bool _closePromptPending;
    private GCHandle _closeHookHandle;
    private nuint _closeHookId;
    private nint _normalWindowStyle;

    public InstanceConsoleView(
        InstanceConsoleWindow hostWindow,
        DaemonConfigModel daemonConfig,
        Guid instanceId,
        bool isDebugMode = false)
    {
        _hostWindow = hostWindow;
        _daemonConfig = daemonConfig;
        _instanceId = instanceId;
        _isDebugMode = isDebugMode;
        _dataManager = new InstanceDataManager(App.Services.DaemonConnections, daemonConfig, instanceId);
        InitializeComponent();
        _editor = new WinUIEditAdapter(EditorControl);
        _editor.Modified += (_, _) => UpdateEditorState();
        CommandPageControl.SendCommandRequested += (_, _) => _ = SendCommandAsync(CommandTabTextBox.Text);
        CommandPageControl.StartRequested += (_, _) => _ = StartInstanceAsync();
        CommandPageControl.StopRequested += (_, _) => _ = StopInstanceAsync();
        CommandPageControl.RestartRequested += (_, _) => _ = RestartInstanceAsync();
        CommandPageControl.KillRequested += (_, _) => _ = KillInstanceAsync();
        CommandPageControl.FullscreenRequested += (_, _) => ToggleFullscreen();
        FileManagerPageControl.LoadFileRequested += (_, _) => _ = LoadFileFromPageAsync();
        FileManagerPageControl.SaveFileRequested += (_, _) => _ = SaveFileAsync();
        FileManagerPageControl.ReloadFileRequested += (_, _) => _ = ReloadFileFromPageAsync();
        FileManagerPageControl.EncodingChanged += (_, _) => _ = UpdateEncodingSelectionAsync();
        FileManagerPageControl.SearchRequested += (_, _) => FindNext_Click(this, new RoutedEventArgs());
        FileManagerPageControl.UndoRequested += (_, _) => Undo_Click(this, new RoutedEventArgs());
        FileManagerPageControl.RedoRequested += (_, _) => Redo_Click(this, new RoutedEventArgs());
        FileManagerPageControl.CopyRequested += (_, _) => Copy_Click(this, new RoutedEventArgs());
        FileManagerPageControl.PasteRequested += (_, _) => Paste_Click(this, new RoutedEventArgs());
        FileManagerPageControl.SelectAllRequested += (_, _) => SelectAll_Click(this, new RoutedEventArgs());
        FileManagerPageControl.ZoomOutRequested += (_, _) => ZoomOut_Click(this, new RoutedEventArgs());
        FileManagerPageControl.ZoomInRequested += (_, _) => ZoomIn_Click(this, new RoutedEventArgs());
        FileManagerPageControl.RefreshDirectoryRequested += (_, _) => _ = RefreshDirectoryAsync();
        FileManagerPageControl.OpenItemRequested += (_, _) => _ = OpenSelectedItemAsync();
        FileManagerPageControl.DownloadRequested += (_, _) => _ = DownloadSelectedFileAsync();
        FileManagerPageControl.UploadRequested += (_, _) => _ = UploadFileAsync();
        FileManagerPageControl.RenameRequested += (_, _) => _ = RenameSelectedFileAsync();
        FileManagerPageControl.DeleteFileRequested += (_, _) => _ = DeleteSelectedFileAsync();
        FileManagerPageControl.CreateDirectoryRequested += (_, _) => _ = CreateDirectoryAsync();
        FileManagerPageControl.NavigateUpRequested += (_, _) => _ = NavigateUpAsync();
        FileManagerPageControl.NavigateBackRequested += (_, _) => _ = NavigateBackAsync();
        FileManagerPageControl.NavigateForwardRequested += (_, _) => _ = NavigateForwardAsync();
        ComponentManagerPageControl.LoadRequested += (_, _) => _ = LoadComponentsAsync();
        ComponentManagerPageControl.AddRequested += (_, _) => AddComponent_Click(this, new RoutedEventArgs());
        ComponentManagerPageControl.ToggleRequested += ToggleComponent_Click;
        ComponentManagerPageControl.LocateRequested += LocateComponent_Click;
        ComponentManagerPageControl.DeleteRequested += DeleteComponent_Click;
        ComponentManagerPageControl.FilesDropped += ComponentManagerPageControl_FilesDropped;
        InstanceSettingsPageControl.SaveRequested += (_, _) => SaveInstanceSettings_Click(this, new RoutedEventArgs());
        InstanceSettingsPageControl.ReloadRequested += (_, _) => _ = LoadInstanceSettingsAsync();
        InstanceSettingsPageControl.ScanJavaRequested += (_, _) => _ = ScanJavaAsync();
        InstanceSettingsPageControl.SelectReplacementCoreRequested += (_, _) => _ = SelectReplacementCoreAsync();
        InstanceSettingsPageControl.ClearReplacementCoreRequested += (_, _) => InstanceSettingsPageControl.SetReplacementCore(string.Empty);
        InstanceSettingsPageControl.HelperRequested += (_, _) => _ = ShowJvmArgumentHelperAsync();
        EventTriggerPageControl.SaveRequested += (_, _) => SaveEventRules_Click(this, new RoutedEventArgs());
        EventTriggerPageControl.ReloadRequested += (_, _) => _ = LoadEventRulesAsync();
        App.Services.Localization.LanguageChanged += Localization_LanguageChanged;
        _dataManager.LogReceived += OnDataLogReceived;
        _dataManager.ReportUpdated += OnReportUpdated;
        _hostWindow.Closed += OnClosed;
        _hostWindow.Activated += OnActivated;
        InstallCloseHook();
    }

    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public string WindowTitle { get; private set; } = "MCServerLauncher Future";
    public UIElement TitleBarElement => TitleBarHost;

    private TextBox CommandTabTextBox => CommandPageControl.Input;
    private TextBox RemotePathTextBox => FileManagerPageControl.RemotePath;
    private TextBox SearchTextBox => FileManagerPageControl.SearchInput;
    private CodeEditorControl EditorControl => FileManagerPageControl.Editor;
    private TextBlock EditorStateText => FileManagerPageControl.StateText;
    private TextBlock ComponentsStateText => ComponentManagerPageControl.StateText;
    private TextBox InstanceNameSettingsTextBox => InstanceSettingsPageControl.NameInput;
    private TextBox JavaPathSettingsTextBox => InstanceSettingsPageControl.JavaInput;
    private TextBox VersionSettingsTextBox => InstanceSettingsPageControl.VersionInput;
    private TextBlock InstanceSettingsStateText => InstanceSettingsPageControl.StateText;
    private Button SaveInstanceSettingsButton => InstanceSettingsPageControl.SaveButton;

    private async void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        if (_isDebugMode)
        {
            SetWindowTitle(Texts["ConsoleTitle"]);
            CommandPageControl.AppendLog("Instance console debug mode.");
            return;
        }

        try
        {
            await _dataManager.InitializeAsync();
            _daemon = _dataManager.Daemon;
            if (_daemon is null) throw new InvalidOperationException(Texts["ConnectDaemonFailedTip"]);
            var history = await _dataManager.GetLogHistoryAsync();
            foreach (var line in history) CommandPageControl.AppendLog(line);
            var report = await _daemon.GetInstanceReportAsync(_instanceId);
            _instanceType = report.Config.InstanceType;
            _instanceStatus = report.Status;
            _instanceName = report.Config.Name;
            UpdateWindowTitle();
            CommandPageControl.UpdateStatus(_instanceStatus);
            BoardPageControl.UpdateReport(report);
            BoardPageControl.UpdateLatency(await _dataManager.GetDaemonLatencyAsync());
            _latencyTimer = App.DispatcherQueue.CreateTimer();
            _latencyTimer.Interval = TimeSpan.FromSeconds(5);
            _latencyTimer.IsRepeating = true;
            _latencyTimer.Tick += LatencyTimer_Tick;
            _latencyTimer.Start();
            _editor.SetLineNumbers(true);
            UpdateEditorState();
            await LoadEventRulesAsync();
            await LoadInstanceSettingsAsync();
            await LoadComponentsAsync();
            await WarnAboutClientSideModsAsync();
            await RefreshDirectoryAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Failed to initialize instance console {InstanceId}", _instanceId);
            AppendLog(ex.Message);
        }
    }

    private void OnDataLogReceived(object? sender, string text)
    {
        App.DispatcherQueue.TryEnqueue(() => CommandPageControl.AppendLog(text));
    }

    private void OnReportUpdated(object? sender, MCServerLauncher.Common.ProtoType.Instance.InstanceReport? report)
    {
        if (report is null) return;
        App.DispatcherQueue.TryEnqueue(() =>
        {
            _instanceType = report.Config.InstanceType;
            _instanceStatus = report.Status;
            _instanceName = report.Config.Name;
            UpdateWindowTitle();
            CommandPageControl.UpdateStatus(_instanceStatus);
            BoardPageControl.UpdateReport(report);
        });
    }

    private void AppendLog(string text)
        => CommandPageControl.AppendLog(text);

    private async Task SendCommandAsync(string command)
    {
        var trimmedCommand = command.Trim();
        if (string.IsNullOrWhiteSpace(trimmedCommand)) return;
        if (_daemon is null || _instanceStatus != InstanceStatus.Running)
        {
            PushUnavailable("ConsoleCommand_SendUnavailable");
            return;
        }
        try
        {
            await _daemon.SentToInstanceAsync(_instanceId, trimmedCommand);
            CommandTabTextBox.Text = string.Empty;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Failed to send command to instance {InstanceId}: {Command}", _instanceId, trimmedCommand);
            App.Services.Notifications.Push(
                Texts["Error"],
                string.Format(Texts["SendCommandFailed"], ex.Message),
                NotificationSeverity.Error);
        }
    }

    private async Task StartInstanceAsync()
    {
        if (_daemon is null || _instanceStatus is not (InstanceStatus.Stopped or InstanceStatus.Crashed))
        {
            PushUnavailable("InstanceCard_StartUnavailable");
            return;
        }

        if (!await ConfirmInstanceActionAsync("InstanceCard_StartConfirmTitle", "InstanceCard_StartConfirmContent", "Start")) return;
        try
        {
            await _daemon.StartInstanceAsync(_instanceId);
            App.Services.Notifications.Push(
                Texts["Success"],
                Texts["StartCommandSentSuccess"],
                NotificationSeverity.Success,
                isClosable: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Failed to start instance {InstanceId}", _instanceId);
            App.Services.Notifications.Push(Texts["Error"], string.Format(Texts["InstanceCard_StartFailed"], ex.Message), NotificationSeverity.Error);
        }
    }

    private async Task StopInstanceAsync()
    {
        if (_daemon is null || _instanceStatus != InstanceStatus.Running)
        {
            PushUnavailable("InstanceCard_StopUnavailable");
            return;
        }

        if (!await ConfirmInstanceActionAsync("InstanceCard_StopConfirmTitle", "InstanceCard_StopConfirmContent", "Stop")) return;
        try
        {
            await _daemon.StopInstanceAsync(_instanceId);
            App.Services.Notifications.Push(
                Texts["Success"],
                Texts["StopCommandSentSuccess"],
                NotificationSeverity.Success,
                isClosable: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Failed to stop instance {InstanceId}", _instanceId);
            App.Services.Notifications.Push(Texts["Error"], string.Format(Texts["InstanceCard_StopFailed"], ex.Message), NotificationSeverity.Error);
        }
    }

    private async Task RestartInstanceAsync()
    {
        if (_daemon is null || _instanceStatus != InstanceStatus.Running)
        {
            PushUnavailable("InstanceCard_RestartUnavailable");
            return;
        }

        if (!await ConfirmInstanceActionAsync("InstanceCard_RestartConfirmTitle", "InstanceCard_RestartConfirmContent", "Restart")) return;
        try
        {
            await _daemon.RestartInstanceAsync(_instanceId);
            App.Services.Notifications.Push(
                Texts["Success"],
                Texts["RestartCommandSentSuccess"],
                NotificationSeverity.Success,
                isClosable: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Failed to restart instance {InstanceId}", _instanceId);
            App.Services.Notifications.Push(Texts["Error"], string.Format(Texts["InstanceCard_RestartFailed"], ex.Message), NotificationSeverity.Error);
        }
    }

    private async Task KillInstanceAsync()
    {
        if (_daemon is null || _instanceStatus != InstanceStatus.Running)
        {
            PushUnavailable("InstanceCard_KillUnavailable");
            return;
        }

        if (XamlRoot is null) return;
        var confirmed = await App.Services.Dialogs.ConfirmCountdownAsync(
            XamlRoot,
            Texts["InstanceCard_KillConfirmTitle"],
            string.Format(Texts["InstanceCard_KillConfirmContent"], _instanceName),
            Texts["Kill"],
            Texts["Cancel"],
            isDestructive: true);
        if (!confirmed) return;

        try
        {
            await _daemon.KillInstanceAsync(_instanceId);
            App.Services.Notifications.Push(
                Texts["Success"],
                Texts["KillCommandSentSuccess"],
                NotificationSeverity.Success,
                isClosable: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Failed to kill instance {InstanceId}", _instanceId);
            App.Services.Notifications.Push(Texts["Error"], string.Format(Texts["InstanceCard_KillFailed"], ex.Message), NotificationSeverity.Error);
        }
    }

    private async Task<bool> ConfirmInstanceActionAsync(string titleKey, string contentKey, string actionKey)
    {
        if (XamlRoot is null) return false;
        return await App.Services.Dialogs.ConfirmAsync(
            XamlRoot,
            Texts[titleKey],
            string.Format(Texts[contentKey], _instanceName),
            Texts[actionKey],
            Texts["Cancel"]);
    }

    private void PushUnavailable(string key) =>
        App.Services.Notifications.Push(
            Texts["Warning"],
            string.Format(Texts[key], _instanceName),
            NotificationSeverity.Warning,
            isClosable: false);

    private void ToggleFullscreen()
    {
        var handle = WinUIIslands.Windowing.WindowNative.GetWindowHandle(_hostWindow);
        if (handle == 0) return;

        if (!_isFullscreen)
        {
            _normalWindowStyle = GetWindowLongPtr(handle, GwlStyle);
            SetWindowLongPtr(handle, GwlStyle, WsPopup | WsVisible);
            ShowWindow(handle, SwMaximize);
            SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpFrameChanged | SwpShowWindow);
            _isFullscreen = true;
        }
        else
        {
            SetWindowLongPtr(handle, GwlStyle, _normalWindowStyle);
            SetWindowPos(handle, HwndNotTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpFrameChanged | SwpShowWindow);
            ShowWindow(handle, SwRestore);
            _isFullscreen = false;
        }

        CommandPageControl.SetFullscreen(_isFullscreen);
    }

    private async void LoadFile_Click(object sender, RoutedEventArgs e) => await LoadFileFromPageAsync();

    private async Task LoadFileFromPageAsync()
    {
        if (_daemon is null || string.IsNullOrWhiteSpace(RemotePathTextBox.Text)) return;
        if (!await ConfirmDiscardAsync()) return;
        await LoadFileAsync();
    }

    private async void ReloadFile_Click(object sender, RoutedEventArgs e) => await ReloadFileFromPageAsync();

    private async Task ReloadFileFromPageAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        await LoadFileAsync();
    }

    private async Task LoadFileAsync(bool showLargeFileWarning = true)
    {
        if (_daemon is null || string.IsNullOrWhiteSpace(RemotePathTextBox.Text)) return;
        try
        {
            var virtualPath = NormalizeVirtualPath(RemotePathTextBox.Text);
            if (virtualPath == "/") return;
            var remotePath = ToDaemonPath(virtualPath);
            if (showLargeFileWarning)
            {
                try
                {
                    var metadata = await _daemon.GetFileInfoAsync(remotePath);
                    if (metadata.Size > 5 * 1024 * 1024 && EditorControl.XamlRoot is not null)
                    {
                        var proceed = await App.Services.Dialogs.ConfirmAsync(
                            EditorControl.XamlRoot,
                            Texts["LargeFileWarning"],
                            string.Format(Texts["LargeFileWarningPrompt"], metadata.Size / 1024 / 1024),
                            Texts["Continue"],
                            Texts["Cancel"]);
                        if (!proceed) return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[WinUI] Could not read remote file metadata for {Path}", remotePath);
                }
            }

            _temporaryFile ??= Path.Combine(Path.GetTempPath(), $"mcsl-{Guid.NewGuid():N}.tmp");
            var context = await _daemon.DownloadFileAsync(remotePath, _temporaryFile, 1024 * 1024);
            if (context.NetworkLoadTask is not null) await context.NetworkLoadTask;
            var bytes = await File.ReadAllBytesAsync(_temporaryFile);
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new StreamReader(stream, _encoding, detectEncodingFromByteOrderMarks: true);
            _editor.LoadText(await reader.ReadToEndAsync());
            _editor.SetEncoding(_encoding);
            _editor.SetLanguage(Path.GetExtension(virtualPath));
            RemotePathTextBox.Text = virtualPath;
            UpdateEditorState();
        }
        catch (Exception ex)
        {
            EditorStateText.Text = ex.Message;
        }
    }

    private async void SaveFile_Click(object sender, RoutedEventArgs e) => await SaveFileAsync();

    private async Task<bool> SaveFileAsync()
    {
        if (_daemon is null || string.IsNullOrWhiteSpace(RemotePathTextBox.Text)) return false;
        while (true)
        {
            try
            {
                var virtualPath = NormalizeVirtualPath(RemotePathTextBox.Text);
                if (virtualPath == "/") return false;
                var text = _editor.ReadText();
                _temporaryFile ??= Path.Combine(Path.GetTempPath(), $"mcsl-{Guid.NewGuid():N}.tmp");
                await using (var writer = new StreamWriter(_temporaryFile, append: false, _encoding))
                    await writer.WriteAsync(text);
                var context = await _daemon.UploadFileAsync(_temporaryFile, ToDaemonPath(virtualPath), 1024 * 1024);
                if (context.NetworkLoadTask is not null) await context.NetworkLoadTask;
                _editor.MarkSaved();
                UpdateEditorState();
                return true;
            }
            catch (Exception ex)
            {
                EditorStateText.Text = ex.Message;
                if (EditorControl.XamlRoot is null) return false;
                var retry = await App.Services.Dialogs.ConfirmAsync(
                    EditorControl.XamlRoot,
                    Texts["SaveFileFailed"],
                    ex.Message,
                    Texts["Retry"],
                    Texts["Cancel"]);
                if (!retry) return false;
            }
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => _editor.Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => _editor.Redo();
    private void Copy_Click(object sender, RoutedEventArgs e) => _editor.Copy();
    private void Paste_Click(object sender, RoutedEventArgs e) => _editor.Paste();
    private void SelectAll_Click(object sender, RoutedEventArgs e) => _editor.SelectAll();
    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Max(-10, _zoom - 1);
        _editor.SetZoom(_zoom);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Min(30, _zoom + 1);
        _editor.SetZoom(_zoom);
    }

    private async Task UpdateEncodingSelectionAsync()
    {
        _encoding = Encoding.GetEncoding(FileManagerPageControl.SelectedEncodingCodePage);
        _editor.SetEncoding(_encoding);
        if (_temporaryFile is not null) await LoadFileAsync(showLargeFileWarning: false);
    }

    private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        FindNext_Click(sender, e);
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        EditorStateText.Text = _editor.Find(SearchTextBox.Text)
            ? Texts["Status_OK"]
            : Texts["Status_NotLoaded"];
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!_editor.IsModified || EditorControl.XamlRoot is null) return true;
        var dialog = new ContentDialog
        {
            XamlRoot = EditorControl.XamlRoot,
            Title = Texts["Prompt"],
            Content = Texts["FileModifiedSavePrompt"],
            PrimaryButtonText = Texts["Yes"],
            SecondaryButtonText = Texts["No"],
            CloseButtonText = Texts["Cancel"],
            DefaultButton = ContentDialogButton.Primary
        };
        return (await dialog.ShowAsync()) switch
        {
            ContentDialogResult.Primary => await SaveFileAsync(),
            ContentDialogResult.Secondary => true,
            _ => false
        };
    }

    private unsafe void InstallCloseHook()
    {
        var handle = WinUIIslands.Windowing.WindowNative.GetWindowHandle(_hostWindow);
        _closeHookHandle = GCHandle.Alloc(this);
        _closeHookId = (nuint)GCHandle.ToIntPtr(_closeHookHandle);
        if (SetWindowSubclass(handle, &WindowSubclassProc, _closeHookId, _closeHookId) != 0) return;
        _closeHookHandle.Free();
        throw new InvalidOperationException("Failed to install the instance-console close guard.");
    }

    private void RequestClose()
    {
        if (_closePromptPending) return;
        _closePromptPending = true;
        _ = ConfirmCloseAsync();
    }

    private async Task ConfirmCloseAsync()
    {
        try
        {
            if (!await ConfirmDiscardAsync()) return;
            _closeAllowed = true;
            _hostWindow.Close();
        }
        finally
        {
            _closePromptPending = false;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint WindowSubclassProc(
        nint handle,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        var target = GCHandle.FromIntPtr((nint)referenceData).Target as InstanceConsoleView;
        if (message == WmClose && target is not null && !target._closeAllowed)
        {
            target.RequestClose();
            return 0;
        }

        return DefSubclassProc(handle, message, wParam, lParam);
    }

    private async Task RefreshDirectoryAsync()
    {
        if (_daemon is null) return;
        try
        {
            var virtualPath = NormalizeVirtualPath(FileManagerPageControl.CurrentPath);
            var (directories, files, _) = await _daemon.GetDirectoryInfoAsync(ToDaemonPath(virtualPath));
            FileManagerPageControl.Files.Clear();
            if (virtualPath != "/")
            {
                var parent = ParentPath(virtualPath);
                FileManagerPageControl.Files.Add(new RemoteFileModel { Name = "..", VirtualPath = parent, IsDirectory = true });
            }

            foreach (var directory in directories)
            {
                var child = ChildPath(virtualPath, directory.Name);
                FileManagerPageControl.Files.Add(new RemoteFileModel
                {
                    Name = directory.Name,
                    VirtualPath = child,
                    IsDirectory = true,
                    ModifiedTime = directory.Meta.LastWriteTime
                });
            }

            foreach (var file in files)
            {
                FileManagerPageControl.Files.Add(new RemoteFileModel
                {
                    Name = file.Name,
                    VirtualPath = ChildPath(virtualPath, file.Name),
                    SizeBytes = file.Meta.Size,
                    ModifiedTime = file.Meta.LastWriteTime
                });
            }

            FileManagerPageControl.CurrentPath = virtualPath;
            FileManagerPageControl.SetNavigationState(_directoryHistoryIndex > 0, _directoryHistoryIndex < _directoryHistory.Count - 1);
        }
        catch (Exception ex)
        {
            FileManagerPageControl.StateText.Text = ex.Message;
        }
    }

    private async Task OpenSelectedItemAsync()
    {
        var item = FileManagerPageControl.SelectedFile;
        if (item is null) return;
        if (item.IsDirectory)
        {
            await NavigateDirectoryAsync(item.VirtualPath);
            return;
        }

        if (!await ConfirmDiscardAsync()) return;
        RemotePathTextBox.Text = item.VirtualPath;
        await LoadFileAsync();
    }

    private async Task NavigateUpAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        await NavigateDirectoryAsync(ParentPath(NormalizeVirtualPath(FileManagerPageControl.CurrentPath)));
    }

    private async Task NavigateBackAsync()
    {
        if (_directoryHistoryIndex <= 0 || !await ConfirmDiscardAsync()) return;
        _directoryHistoryIndex--;
        await NavigateDirectoryAsync(_directoryHistory[_directoryHistoryIndex], addHistory: false);
    }

    private async Task NavigateForwardAsync()
    {
        if (_directoryHistoryIndex >= _directoryHistory.Count - 1 || !await ConfirmDiscardAsync()) return;
        _directoryHistoryIndex++;
        await NavigateDirectoryAsync(_directoryHistory[_directoryHistoryIndex], addHistory: false);
    }

    private async Task NavigateDirectoryAsync(string path, bool addHistory = true)
    {
        var normalized = NormalizeVirtualPath(path);
        if (addHistory && !_restoringDirectoryHistory)
        {
            var current = NormalizeVirtualPath(FileManagerPageControl.CurrentPath);
            if (!string.Equals(current, normalized, StringComparison.Ordinal))
            {
                if (_directoryHistoryIndex < _directoryHistory.Count - 1)
                    _directoryHistory.RemoveRange(_directoryHistoryIndex + 1, _directoryHistory.Count - _directoryHistoryIndex - 1);
                _directoryHistory.Add(normalized);
                _directoryHistoryIndex = _directoryHistory.Count - 1;
            }
        }

        _restoringDirectoryHistory = !addHistory;
        FileManagerPageControl.CurrentPath = normalized;
        try { await RefreshDirectoryAsync(); }
        finally { _restoringDirectoryHistory = false; }
    }

    private async Task UploadFileAsync()
    {
        if (_daemon is null) return;
        var file = await App.Services.Files.PickFileAsync(WinUIIslands.Windowing.WindowNative.GetWindowHandle(_hostWindow));
        if (file is null) return;
        try
        {
            var directory = NormalizeVirtualPath(FileManagerPageControl.CurrentPath);
            var target = ChildPath(directory, Path.GetFileName(file.Path));
            var upload = await _daemon.UploadFileAsync(file.Path, ToDaemonPath(target), 1024 * 1024);
            if (upload.NetworkLoadTask is not null) await upload.NetworkLoadTask;
            await RefreshDirectoryAsync();
        }
        catch (Exception ex) { FileManagerPageControl.StateText.Text = ex.Message; }
    }

    private async Task DownloadSelectedFileAsync()
    {
        if (_daemon is null || FileManagerPageControl.SelectedFile is not { IsDirectory: false } item) return;
        var file = await App.Services.Files.PickSaveFileAsync(WinUIIslands.Windowing.WindowNative.GetWindowHandle(_hostWindow), item.Name);
        if (file is null) return;
        try
        {
            var download = await _daemon.DownloadFileAsync(ToDaemonPath(item.VirtualPath), file.Path, 1024 * 1024);
            if (download.NetworkLoadTask is not null) await download.NetworkLoadTask;
            FileManagerPageControl.StateText.Text = Texts["DownloadFinished"];
        }
        catch (Exception ex) { FileManagerPageControl.StateText.Text = ex.Message; }
    }

    private async Task RenameSelectedFileAsync()
    {
        if (_daemon is null || FileManagerPageControl.SelectedFile is not { Name: not ".." } item || XamlRoot is null) return;
        var name = await ShowTextInputAsync(Texts["Rename"], item.Name);
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return;
        try
        {
            if (item.IsDirectory)
                await _daemon.RenameDirectoryAsync(ToDaemonPath(item.VirtualPath), name.Trim());
            else
                await _daemon.RenameFileAsync(ToDaemonPath(item.VirtualPath), name.Trim());
            await RefreshDirectoryAsync();
        }
        catch (Exception ex) { FileManagerPageControl.StateText.Text = ex.Message; }
    }

    private async Task DeleteSelectedFileAsync()
    {
        if (_daemon is null || FileManagerPageControl.SelectedFile is not { Name: not ".." } item || XamlRoot is null) return;
        var confirmed = await App.Services.Dialogs.ConfirmAsync(
            XamlRoot,
            Texts["ConfirmDelete"],
            string.Format(Texts["ComponentManager_ConfirmDeleteMessage"], item.Name),
            Texts["Delete"],
            Texts["Cancel"],
            isDestructive: true);
        if (!confirmed) return;
        try
        {
            if (item.IsDirectory) await _daemon.DeleteDirectoryAsync(ToDaemonPath(item.VirtualPath));
            else await _daemon.DeleteFileAsync(ToDaemonPath(item.VirtualPath));
            await RefreshDirectoryAsync();
        }
        catch (Exception ex) { FileManagerPageControl.StateText.Text = ex.Message; }
    }

    private async Task CreateDirectoryAsync()
    {
        if (_daemon is null || XamlRoot is null) return;
        var name = await ShowTextInputAsync(Texts["CreateDirectory"], string.Empty);
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return;
        try
        {
            var path = ChildPath(NormalizeVirtualPath(FileManagerPageControl.CurrentPath), name.Trim());
            await _daemon.CreateDirectoryAsync(ToDaemonPath(path));
            await RefreshDirectoryAsync();
        }
        catch (Exception ex) { FileManagerPageControl.StateText.Text = ex.Message; }
    }

    private async Task<string?> ShowTextInputAsync(string title, string value)
    {
        var input = new TextBox { Text = value, MinWidth = 360 };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = input,
            PrimaryButtonText = Texts["Continue"],
            CloseButtonText = Texts["Cancel"],
            DefaultButton = ContentDialogButton.Primary
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text : null;
    }

    private string ToDaemonPath(string virtualPath) =>
        $"/instances/{_instanceId}{(virtualPath == "/" ? string.Empty : NormalizeVirtualPath(virtualPath))}";

    private static string NormalizeVirtualPath(string path)
    {
        var stack = new Stack<string>();
        foreach (var part in (path ?? string.Empty).Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..") { if (stack.Count > 0) stack.Pop(); continue; }
            stack.Push(part);
        }
        if (stack.Count == 0) return "/";
        var values = stack.ToArray();
        Array.Reverse(values);
        return "/" + string.Join('/', values);
    }

    private static string ParentPath(string path)
    {
        path = NormalizeVirtualPath(path);
        if (path == "/") return "/";
        var slash = path.LastIndexOf('/');
        return slash <= 0 ? "/" : path[..slash];
    }

    private static string ChildPath(string parent, string name) =>
        NormalizeVirtualPath(parent) == "/" ? $"/{name}" : $"{NormalizeVirtualPath(parent)}/{name}";

    private async Task LoadEventRulesAsync()
    {
        if (_daemon is null)
        {
            App.Services.Notifications.Push(
                Texts["Error"],
                Texts["FuncDisabledReason_NoDaemon"],
                NotificationSeverity.Error);
            return;
        }
        try
        {
            var rules = await _daemon.GetEventRulesAsync(_instanceId);
            EventTriggerPageControl.SetRules(rules);
        }
        catch (Exception ex)
        {
            App.Services.Notifications.Push(
                Texts["Error"],
                string.Format(Texts["EventTrigger_LoadRulesFailed"], ex.Message),
                NotificationSeverity.Error);
        }
    }

    private async void LoadComponents_Click(object sender, RoutedEventArgs e) => await LoadComponentsAsync();

    private async void ComponentKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialized) await LoadComponentsAsync();
    }

    private async Task LoadComponentsAsync()
    {
        if (_daemon is null) return;
        try
        {
            var scan = await ComponentScanner.ScanAsync(_daemon, _instanceId);
            ComponentManagerPageControl.SetItems(scan.Mods, scan.Plugins, scan.HasMods, scan.HasPlugins);
            ComponentManagerPageControl.ApplySupportState(scan.SupportsComponents);
            var folder = ComponentFolder;
            var values = folder == "plugins" ? scan.Plugins : scan.Mods;

            ComponentsStateText.Text = !scan.SupportsComponents
                ? Texts["ComponentManager_NoTargetFolder"]
                : values.Count == 0
                ? Texts[folder == "mods" ? "ComponentManager_EmptyMods" : "ComponentManager_EmptyPlugins"]
                : string.Empty;
        }
        catch (Exception ex)
        {
            ComponentManagerPageControl.SetItems([], [], false, false);
            ComponentsStateText.Text = ex.Message;
            App.Services.Notifications.Push(Texts["Error"], ex.Message, NotificationSeverity.Error);
        }
    }

    private async Task WarnAboutClientSideModsAsync()
    {
        if (_daemon is null) return;
        var root = ComponentManagerPageControl.XamlRoot ?? XamlRoot;
        if (root is null) return;

        try
        {
            var scan = await ComponentScanner.ScanAsync(_daemon, _instanceId);
            var clientSideMods = scan.Mods
                .Where(item => item.IsEnabled && item.IsClientSideOnly)
                .ToArray();
            if (clientSideMods.Length == 0) return;

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = string.Format(Texts["ComponentManager_ClientSideModsWarning"], clientSideMods.Length),
                TextWrapping = TextWrapping.Wrap
            });
            var list = new ListView
            {
                ItemsSource = clientSideMods.Select(item => item.Title).ToArray(),
                MaxHeight = 240,
                SelectionMode = ListViewSelectionMode.None
            };
            panel.Children.Add(list);

            var dialog = new ContentDialog
            {
                XamlRoot = root,
                Title = Texts["ComponentManager_ClientSideModsFound"],
                Content = panel,
                PrimaryButtonText = Texts["ComponentManager_DisableClientSideMods"],
                CloseButtonText = Texts["Ignore"],
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            foreach (var item in clientSideMods)
                await ComponentScanner.DisableAsync(_daemon, item);

            App.Services.Notifications.Push(
                Texts["Success"],
                string.Format(Texts["ComponentManager_DisabledClientSideMods"], clientSideMods.Length),
                NotificationSeverity.Success,
                isClosable: false);
            await LoadComponentsAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Failed to scan client-side mods");
        }
    }

    private async void LatencyTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        try
        {
            BoardPageControl.UpdateLatency(await _dataManager.GetDaemonLatencyAsync());
        }
        catch (Exception exception)
        {
            Log.Debug(exception, "[WinUI] Failed to refresh console daemon latency");
        }
    }

    private async void AddComponent_Click(object sender, RoutedEventArgs e)
    {
        if (_daemon is null) return;
        var files = await App.Services.Files.PickFilesAsync(WinUIIslands.Windowing.WindowNative.GetWindowHandle(_hostWindow));
        await UploadComponentsAsync(files);
    }

    private async void ComponentManagerPageControl_FilesDropped(object? sender, IReadOnlyList<Windows.Storage.StorageFile> files) =>
        await UploadComponentsAsync(files);

    private async Task UploadComponentsAsync(IEnumerable<Windows.Storage.StorageFile> files)
    {
        if (_daemon is null) return;
        if (ComponentFolder is not ("mods" or "plugins"))
        {
            ComponentsStateText.Text = Texts["ComponentManager_NoTargetFolder"];
            App.Services.Notifications.Push(
                Texts["Error"],
                Texts["ComponentManager_NoTargetFolder"],
                NotificationSeverity.Warning);
            return;
        }

        var localFiles = files
            .Where(file => file.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (localFiles.Length == 0)
        {
            ComponentsStateText.Text = Texts["ComponentManager_NoJarFiles"];
            App.Services.Notifications.Push(
                Texts["Error"],
                Texts["ComponentManager_NoJarFiles"],
                NotificationSeverity.Warning);
            return;
        }

        var uploaded = 0;
        foreach (var file in localFiles)
        {
            try
            {
                if (ComponentFolder == "mods" && JarMetadataParser.IsClientSideMod(file.Path))
                {
                    ComponentsStateText.Text = Texts["ComponentManager_ClientSideModsWarning"];
                    App.Services.Notifications.Push(
                        Texts["Warning"],
                        string.Format(Texts["ComponentManager_ClientSideModBlocked"], Path.GetFileName(file.Path)),
                        NotificationSeverity.Warning);
                    continue;
                }
                var target = $"/instances/{_instanceId}/{ComponentFolder}/{Path.GetFileName(file.Path)}";
                var upload = await _daemon.UploadFileAsync(file.Path, target, 1024 * 1024);
                if (upload.NetworkLoadTask is not null) await upload.NetworkLoadTask;
                uploaded++;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[WinUI] Failed to upload component {Path}", file.Path);
            }
        }

        ComponentsStateText.Text = string.Format(Texts["ComponentManager_AddedCount"], uploaded, localFiles.Length);
        App.Services.Notifications.Push(
            Texts["Success"],
            ComponentsStateText.Text,
            NotificationSeverity.Success,
            isClosable: false);
        await LoadComponentsAsync();
    }

    private async void ToggleComponent_Click(object sender, RoutedEventArgs e)
    {
        if (_daemon is null || (sender as Button)?.Tag is not ComponentFileModel item) return;
        try
        {
            if (item.IsEnabled) await ComponentScanner.DisableAsync(_daemon, item);
            else await ComponentScanner.EnableAsync(_daemon, item);
            App.Services.Notifications.Push(
                Texts["Success"],
                Texts[item.IsEnabled ? "ComponentManager_Enabled" : "ComponentManager_Disabled"],
                NotificationSeverity.Success,
                isClosable: false);
        }
        catch (Exception ex)
        {
            ComponentsStateText.Text = ex.Message;
            App.Services.Notifications.Push(Texts["Error"], ex.Message, NotificationSeverity.Error);
        }
    }

    private void LocateComponent_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ComponentFileModel item)
        {
            App.Services.Clipboard.SetText(item.VirtualPath);
            App.Services.Notifications.Push(
                Texts["ComponentManager_PathCopied"],
                item.VirtualPath,
                NotificationSeverity.Informational);
        }
    }

    private async void DeleteComponent_Click(object sender, RoutedEventArgs e)
    {
        if (_daemon is null || (sender as Button)?.Tag is not ComponentFileModel item || ComponentManagerPageControl.XamlRoot is null) return;
        var confirmed = await App.Services.Dialogs.ConfirmAsync(
            ComponentManagerPageControl.XamlRoot,
            Texts["ConfirmDelete"],
            string.Format(Texts["ComponentManager_ConfirmDeleteMessage"], item.FileName),
            Texts["Delete"],
            Texts["Cancel"],
            isDestructive: true);
        if (!confirmed) return;
        try
        {
            await _daemon.DeleteFileAsync(item.VirtualPath);
            App.Services.Notifications.Push(
                Texts["Success"],
                Texts["ComponentManager_Deleted"],
                NotificationSeverity.Success,
                isClosable: false);
            await LoadComponentsAsync();
        }
        catch (Exception ex)
        {
            ComponentsStateText.Text = ex.Message;
            App.Services.Notifications.Push(Texts["Error"], ex.Message, NotificationSeverity.Error);
        }
    }

    private string ComponentFolder => ComponentManagerPageControl.SelectedFolder;

    private async void LoadInstanceSettings_Click(object sender, RoutedEventArgs e) => await LoadInstanceSettingsAsync();

    private async Task LoadInstanceSettingsAsync()
    {
        if (_daemon is null) return;
        InstanceSettingsPageControl.BeginLoad();
        try
        {
            var result = await _daemon.GetInstanceSettingsAsync(_instanceId);
            _instanceType = result.Config.InstanceType;
            InstanceSettingsPageControl.InstanceIdText.Text = _instanceId.ToString();
            InstanceNameSettingsTextBox.Text = result.Config.Name;
            JavaPathSettingsTextBox.Text = result.Config.JavaPath ?? string.Empty;
            VersionSettingsTextBox.Text = result.Config.Version ?? string.Empty;
            InstanceSettingsPageControl.SetArguments(result.Config.Arguments ?? []);
            InstanceSettingsPageControl.SetReplacementCore(string.Empty);
            InstanceSettingsPageControl.ForceRerunInstallerInput.IsChecked = false;
            var editableTypes = Enum.GetValues<MCServerLauncher.Common.ProtoType.Instance.InstanceType>()
                .Where(type => type == _instanceType || type.IsMinecraftJavaRuntimeType())
                .ToArray();
            InstanceSettingsPageControl.SetInstanceTypeOptions(editableTypes, _instanceType);
            InstanceSettingsPageControl.ApplyEditState(
                result.CanEdit,
                _instanceType.IsMinecraftJavaRuntimeType(),
                _instanceType is MCServerLauncher.Common.ProtoType.Instance.InstanceType.MCForge
                    or MCServerLauncher.Common.ProtoType.Instance.InstanceType.MCNeoForge
                    or MCServerLauncher.Common.ProtoType.Instance.InstanceType.MCCleanroom);
            InstanceSettingsPageControl.TargetInfo.Text =
                $"{Texts["CorePath"]}: {result.Config.Target}\n{Texts["File"]}: {result.WorkingDirectory}";
            InstanceSettingsStateText.Text = result.CanEdit
                ? string.Empty
                : result.EditBlockedReason ?? Texts["Status_NotLoaded"];
            InstanceSettingsPageControl.MarkLoaded();
        }
        catch (Exception ex)
        {
            InstanceSettingsStateText.Text = ex.Message;
            App.Services.Notifications.Push(Texts["Error"], ex.Message, NotificationSeverity.Error);
        }
    }

    private async void SaveInstanceSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_daemon is null || !SaveInstanceSettingsButton.IsEnabled) return;
        InstanceSettingsStateText.Text = Texts["PleaseWait"];
        try
        {
            var instanceName = InstanceNameSettingsTextBox.Text.Trim();
            var javaPath = JavaPathSettingsTextBox.Text.Trim();
            var replacementPath = InstanceSettingsPageControl.ReplacementCorePath;
            if (!ValidateInstanceSettings(instanceName, javaPath, replacementPath)) return;

            InstanceCoreReplacementRequest? replacement = null;
            if (!string.IsNullOrWhiteSpace(replacementPath))
            {
                var uploadPath = $"/instances/{_instanceId}/uploads/{Path.GetFileName(replacementPath)}";
                var upload = await _daemon.UploadFileAsync(replacementPath, uploadPath, 1024 * 1024);
                if (upload.NetworkLoadTask is not null) await upload.NetworkLoadTask;
                replacement = new InstanceCoreReplacementRequest
                {
                    UploadedSourcePath = uploadPath,
                    PreferredTargetName = Path.GetFileName(replacementPath)
                };
            }

            _ = await _daemon.UpdateInstanceSettingsAsync(new UpdateInstanceSettingsParameter
            {
                Id = _instanceId,
                Name = instanceName,
                InstanceType = InstanceSettingsPageControl.SelectedInstanceType,
                JavaPath = javaPath,
                Version = VersionSettingsTextBox.Text.Trim(),
                Arguments = InstanceSettingsPageControl.GetArguments(),
                ReplacementCore = replacement,
                ForceRerunInstaller = InstanceSettingsPageControl.ForceRerunInstaller
            });
            InstanceSettingsStateText.Text = Texts["SettingsSaveSuccess"];
            App.Services.Notifications.Push(
                Texts["Success"],
                Texts["SettingsSaveSuccess"],
                NotificationSeverity.Success,
                isClosable: false);
            await LoadInstanceSettingsAsync();
        }
        catch (Exception ex)
        {
            InstanceSettingsStateText.Text = ex.Message;
            App.Services.Notifications.Push(Texts["Error"], ex.Message, NotificationSeverity.Error);
        }
    }

    private bool ValidateInstanceSettings(string instanceName, string javaPath, string replacementPath)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            return FailInstanceSettingsValidation(Texts["CreateInstanceMissingDataError"]);

        if (instanceName is "." or ".."
            || instanceName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || instanceName.Any(char.IsControl))
        {
            return FailInstanceSettingsValidation(
                $"{Texts["InstanceName"]}: {Texts["CreateInstanceMissingDataError"]}");
        }

        if (string.IsNullOrWhiteSpace(javaPath))
            return FailInstanceSettingsValidation(Texts["CreateInstanceMissingDataError"]);

        if (javaPath.Any(char.IsControl)
            || (javaPath.StartsWith("(", StringComparison.Ordinal)
                && javaPath.Contains(") ", StringComparison.Ordinal)))
        {
            return FailInstanceSettingsValidation(
                $"{Texts["JavaPath"]}: {Texts["CreateInstanceMissingDataError"]}");
        }

        if (!string.IsNullOrWhiteSpace(replacementPath)
            && (replacementPath.Any(char.IsControl)
                || !Path.GetExtension(replacementPath).Equals(".jar", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(replacementPath)))
        {
            return FailInstanceSettingsValidation(
                $"{Texts["CorePath"]}: {Texts["CreateInstanceMissingDataError"]}");
        }

        return true;
    }

    private bool FailInstanceSettingsValidation(string message)
    {
        InstanceSettingsStateText.Text = message;
        App.Services.Notifications.Push(Texts["Error"], message, NotificationSeverity.Error);
        return false;
    }

    private async Task ScanJavaAsync()
    {
        if (_daemon is null || InstanceSettingsPageControl.XamlRoot is null) return;
        App.Services.Notifications.Push(
            Texts["PleaseWait"],
            Texts["SearchingJvmTip"],
            NotificationSeverity.Informational,
            isClosable: false);
        try
        {
            var jvms = await _daemon.GetJavaListAsync();
            if (jvms.Length == 0)
            {
                InstanceSettingsStateText.Text = Texts["NoJavaFound"];
                App.Services.Notifications.Push(
                    Texts["Info"],
                    Texts["NoJavaFound"],
                    NotificationSeverity.Warning);
                return;
            }

            var list = new ListView { ItemsSource = jvms.Select(java => $"({java.Version}, {java.Architecture}) {java.Path}").ToArray(), SelectedIndex = 0 };
            var dialog = new ContentDialog
            {
                XamlRoot = InstanceSettingsPageControl.XamlRoot,
                Title = Texts["PleaseSelectJvm"],
                Content = list,
                PrimaryButtonText = Texts["Continue"],
                CloseButtonText = Texts["Cancel"],
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && list.SelectedIndex >= 0)
                JavaPathSettingsTextBox.Text = jvms[list.SelectedIndex].Path;
        }
        catch (Exception ex)
        {
            InstanceSettingsStateText.Text = ex.Message;
            App.Services.Notifications.Push(
                Texts["Error"],
                $"{Texts["SearchJavaError"]}: {ex.Message}",
                NotificationSeverity.Error);
        }
    }

    private async Task SelectReplacementCoreAsync()
    {
        var file = await App.Services.Files.PickFileAsync(WinUIIslands.Windowing.WindowNative.GetWindowHandle(_hostWindow));
        if (file is not null) InstanceSettingsPageControl.SetReplacementCore(file.Path);
    }

    private async Task ShowJvmArgumentHelperAsync()
    {
        var arguments = await JvmArgumentHelperDialog.ShowAsync(InstanceSettingsPageControl.XamlRoot, Texts);
        if (arguments is null) return;
        foreach (var argument in arguments)
            InstanceSettingsPageControl.AddArgument(argument);
    }

    private async void SaveEventRules_Click(object sender, RoutedEventArgs e)
    {
        if (_daemon is null)
        {
            App.Services.Notifications.Push(
                Texts["Error"],
                Texts["FuncDisabledReason_NoDaemon"],
                NotificationSeverity.Error);
            return;
        }
        try
        {
            await _daemon.SaveEventRulesAsync(_instanceId, EventTriggerPageControl.GetRules());
            App.Services.Notifications.Push(
                Texts["Success"],
                Texts["EventTrigger_SaveRulesSuccess"],
                NotificationSeverity.Success);
        }
        catch (Exception ex)
        {
            App.Services.Notifications.Push(
                Texts["Error"],
                string.Format(Texts["EventTrigger_SaveRulesFailed"], ex.Message),
                NotificationSeverity.Error);
        }
    }

    private void UpdateEditorState() => EditorStateText.Text = _editor.IsModified ? Texts["Modified"] : Texts["Saved"];

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        UpdateWindowTitle();
        UpdateEditorState();
    }

    private void UpdateWindowTitle()
    {
        if (_isDebugMode)
        {
            SetWindowTitle(Texts["ConsoleTitle"]);
            return;
        }

        var instancePart = string.Format(Texts["InstanceConsole_InstanceTitlePart"], _instanceName);
        var daemonPart = string.Format(
            Texts["InstanceConsole_NodeTitlePart"],
            _daemonConfig.FriendlyName ?? _daemonConfig.EndPoint);
        SetWindowTitle($"{Texts["ConsoleTitle"]} - {instancePart} - {daemonPart}");
    }

    private void SetWindowTitle(string title)
    {
        WindowTitle = title;
        WindowTitleText.Text = title;
        _hostWindow.Title = title;
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        try
        {
            if (_closeHookHandle.IsAllocated)
            {
                unsafe
                {
                    RemoveWindowSubclass(
                        WinUIIslands.Windowing.WindowNative.GetWindowHandle(_hostWindow),
                        &WindowSubclassProc,
                        _closeHookId);
                }
                _closeHookHandle.Free();
            }
            if (_latencyTimer is not null)
            {
                _latencyTimer.Stop();
                _latencyTimer.Tick -= LatencyTimer_Tick;
                _latencyTimer = null;
            }
            _dataManager.LogReceived -= OnDataLogReceived;
            _dataManager.ReportUpdated -= OnReportUpdated;
            App.Services.Localization.LanguageChanged -= Localization_LanguageChanged;
            await _dataManager.DisposeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Failed to dispose instance console {InstanceId}", _instanceId);
        }
        finally
        {
            if (_temporaryFile is not null)
            {
                try { File.Delete(_temporaryFile); } catch { }
            }
            App.UnregisterSecondaryWindow(_hostWindow);
        }
    }

    private const int GwlStyle = -16;
    private const int SwRestore = 9;
    private const int SwMaximize = 3;
    private const nint HwndTopmost = -1;
    private const nint HwndNotTopmost = -2;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpFrameChanged = 0x0020;
    private static readonly nint WsPopup = unchecked((nint)0x80000000);
    private const nint WsVisible = 0x10000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint handle, int index, nint value);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint handle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        nint handle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern unsafe int SetWindowSubclass(
        nint handle,
        delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint> callback,
        nuint subclassId,
        nuint referenceData);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern unsafe int RemoveWindowSubclass(
        nint handle,
        delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint> callback,
        nuint subclassId);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern nint DefSubclassProc(nint handle, uint message, nuint wParam, nint lParam);
}
