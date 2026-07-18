using System.ComponentModel;
using System.IO;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.Common.DownloadProvider;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.InstanceConsole;
using MCServerLauncher.WinUI.InstanceConsole.View.Dialogs;

namespace MCServerLauncher.WinUI.Views.Pages;

public sealed partial class DebugPage : Page, INotifyPropertyChanged
{
    public DebugPage()
    {
        NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public string DebugTitle => "Test";
    public string WindowTestsTitle => "Window and Notification Tests";
    public string ShowConsoleText => "Show Console Window";
    public string ShowExceptionText => "Show Exception Window";
    public string ShowFirstSetupText => "Show First Setup";
    public string InformationalText => "Informational-Top";
    public string WarningText => "Warning-TopRight";
    public string ErrorText => "Error-Bottom";
    public string SuccessText => "Success-BottomRight";
    public string FileEditorTestsTitle => "File Editor Tests";
    public string DownloadApiTestsTitle => "Download API Tests";
    public string LogText => "Log";
    public string IniText => "Ini";
    public string YamlText => "Yaml";
    public string TomlText => "Toml";
    public string BatText => "Bat";
    public string ShellText => "Shell";
    public string CsvText => "CSV";
    public string FastMirrorText => "FastMirror";
    public string AListText => "AList";
    public string PolarsText => "Polars";
    public string MslText => "MSL";
    public string McslSyncText => "MCSLSync (Waiting for Production)";
    public string TestFastMirrorEndpointText => "Test FastMirror EndPoint";
    public string TestFastMirrorCoreText => "Test FastMirror Core";
    public string TestAListHostText => "Test RianYun AList Host";
    public string TestAListFileText => "Test RianYun AList File";
    public string TestPolarsEndpointText => "Test Polars EndPoint";
    public string TestPolarsCoreText => "Test Polars Core";
    public string TestMslEndpointText => "Test MSL EndPoint";
    public string TestMslCoreText => "Test MSL Core";
    public string TestMslDownloadText => "Test MSL DownloadUrl";
    public string TestMcslSyncEndpointText => "Test MCSLSync EndPoint";
    public string TestMcslSyncCoreText => "Test MCSLSync Core";
    public string TestMcslSyncVersionText => "Test MCSLSync CoreVersion";
    public string TestMcslSyncDetailText => "Test MCSLSync Core Detail";

    public string DiagnosticsText =>
        $"Version: {App.AppVersion}\n" +
        $"OS: {Environment.OSVersion}\n" +
        $"Data: {App.Services.Paths.DataRoot}\n" +
        $"Logs: {App.Services.Paths.LogsRoot}\n" +
        $"Daemon: {string.Join(", ", App.Services.Daemons.Items.Select(config =>
            $"{config.FriendlyName ?? config.EndPoint}:{config.Port} ({(config.IsSecure ? "wss" : "ws")})"))}";

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Texts)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DiagnosticsText)));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Services.Localization.LanguageChanged -= Localization_LanguageChanged;
        App.Services.Localization.LanguageChanged += Localization_LanguageChanged;
        Localization_LanguageChanged(this, EventArgs.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        App.Services.Localization.LanguageChanged -= Localization_LanguageChanged;

    private void ShowConsoleWindow(object sender, RoutedEventArgs e)
    {
        var window = new InstanceConsoleWindow();
        App.RegisterSecondaryWindow(window);
        window.Activate();
    }

    private void ShowFirstSetup(object sender, RoutedEventArgs e) => App.Window.RootPage.ShowFirstSetupForDebug();

    private void ShowExceptionWindow(object sender, RoutedEventArgs e) =>
        throw new Exception("Test Exception");

    private void OpenFileEditor_Log(object sender, RoutedEventArgs e) => OpenFileEditor("test.log");
    private void OpenFileEditor_Ini(object sender, RoutedEventArgs e) => OpenFileEditor("test.ini");
    private void OpenFileEditor_Yaml(object sender, RoutedEventArgs e) => OpenFileEditor("test.yaml");
    private void OpenFileEditor_Toml(object sender, RoutedEventArgs e) => OpenFileEditor("test.toml");
    private void OpenFileEditor_Bat(object sender, RoutedEventArgs e) => OpenFileEditor("test.bat");
    private void OpenFileEditor_Shell(object sender, RoutedEventArgs e) => OpenFileEditor("test.sh");
    private void OpenFileEditor_Csv(object sender, RoutedEventArgs e) => OpenFileEditor("test.csv");

    private void OpenFileEditor(string filename)
    {
        var path = Path.Combine(Path.GetTempPath(), filename);
        if (!File.Exists(path)) File.WriteAllText(path, GetSampleContent(filename));

        var window = new DebugEditorWindow(path, filename);
        App.RegisterSecondaryWindow(window);
        window.Activate();
    }

    private static string GetSampleContent(string filename) => Path.GetExtension(filename).ToLowerInvariant() switch
    {
        ".log" => "[12:34:56] [main/INFO]: This is an info message\n[12:34:57] [main/WARN]: This is a warning\n[12:34:58] [main/ERROR]: This is an error\n\tat com.example.Main.main(Main.java:10)",
        ".ini" => "[Section]\nKey=Value\n# Comment\nNum=123",
        ".yaml" => "name: Test\nversion: 1.0\ndependencies:\n  - lib1\n  - lib2",
        ".toml" => "[package]\nname = \"test\"\nversion = \"0.1.0\"\n\n[dependencies]\nserde = \"1.0\"",
        ".bat" => "@echo off\nREM This is a batch file\nset VAR=Hello\necho %VAR%\nif \"%VAR%\"==\"Hello\" echo World",
        ".sh" => "#!/bin/bash\n# This is a shell script\nVAR=\"Hello\"\necho $VAR\nif [ \"$VAR\" == \"Hello\" ]; then\n  echo \"World\"\nfi",
        ".csv" => "Name,Age,City\nAlice,30,New York\nBob,25,Los Angeles\nCharlie,35,Chicago",
        _ => "Sample text"
    };

    private void PushSimpleNotification(object sender, RoutedEventArgs e)
    {
        var content = (sender as Button)?.Content?.ToString() ?? InformationalText;
        var parts = content.Split('-');
        var severity = parts[0] switch
        {
            "Success" => NotificationSeverity.Success,
            "Warning" => NotificationSeverity.Warning,
            "Error" => NotificationSeverity.Error,
            _ => NotificationSeverity.Informational
        };
        var randomNumber = Random.Shared.Next(100000, 999999);
        var position = parts.Length > 1 ? parts[1] : "Top";
        App.Services.Notifications.Push(
            "Title",
            $"Message{randomNumber} - {position}",
            severity,
            isClosable: false,
            durationMs: 3000);
    }

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        App.Services.Clipboard.SetText(DiagnosticsText);
        App.Services.Notifications.Push(
            Texts["Status_OK"],
            Texts["InstanceConfigCopied"],
            NotificationSeverity.Success);
    }

    private async Task ShowTextResultContentDialogAsync(string result)
    {
        if (XamlRoot is null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Result",
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = result, TextWrapping = TextWrapping.Wrap },
                MaxHeight = 560
            },
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary
        };
        try { await dialog.ShowAsync(); } catch { }
    }

    private async void TestFastMirrorEndPoint(object sender, RoutedEventArgs e)
    {
        var results = await FastMirror.GetCoreInfo();
        var text = (results ?? []).Aggregate("", (current, result) => current +
            $"Name: {result.Name}\nTag: {result.Tag}\nHomePage: {result.HomePage}\nRecommend: {result.Recommend}\nMinecraftVersions: {string.Join(", ", result.MinecraftVersions ?? [])}\n\n");
        await ShowTextResultContentDialogAsync(text);
    }

    private async void TestFastMirrorCore(object sender, RoutedEventArgs e)
    {
        var results = await FastMirror.GetCoreDetail("Paper", "1.20.1");
        var text = (results ?? []).Aggregate("", (current, result) => current +
            $"Name: {result.Name}\nMinecraftVersion: {result.MinecraftVersion}\nCoreVersion: {result.CoreVersion}\nSHA1: {result.Sha1}\n\n");
        await ShowTextResultContentDialogAsync(text);
    }

    private async void TestRianYunAList(object sender, RoutedEventArgs e)
    {
        var results = await AList.GetFileList("https://mirrors.rainyun.com", "服务端合集/Arclight");
        var text = (results ?? []).Aggregate("", (current, result) => current +
            $"FileName: {result.FileName}\nFileSize: {result.FileSize}\nIsDirectory: {result.IsDirectory}\n\n");
        await ShowTextResultContentDialogAsync(text);
    }

    private async void TestRianYunAListFile(object sender, RoutedEventArgs e)
    {
        var result = await AList.GetFileUrl("https://mirrors.rainyun.com", "服务端合集/Arclight/1.21-neoforge.zip");
        await ShowTextResultContentDialogAsync($"RawUrl: {result}\n");
    }

    private async void TestPolars(object sender, RoutedEventArgs e)
    {
        var results = await PolarsMirror.GetCoreInfo();
        var text = (results ?? []).Aggregate("", (current, result) => current +
            $"Name: {result.Name}\nId: {result.Id}\nDescription: {result.Description}\n\n");
        await ShowTextResultContentDialogAsync(text);
    }

    private async void TestPolarsCore(object sender, RoutedEventArgs e)
    {
        var results = await PolarsMirror.GetCoreDetail(1);
        var text = (results ?? []).Aggregate("", (current, result) => current +
            $"Name: {result.FileName}\nDownloadUrl: {result.DownloadUrl}\n\n");
        await ShowTextResultContentDialogAsync(text);
    }

    private async void TestMSL(object sender, RoutedEventArgs e)
    {
        var results = await MSLAPI.GetCoreInfo();
        await ShowTextResultContentDialogAsync((results ?? []).Aggregate("", (current, result) => current + $"Name: {result}\n"));
    }

    private async void TestMSLCore(object sender, RoutedEventArgs e)
    {
        var results = await MSLAPI.GetMinecraftVersions("paper");
        await ShowTextResultContentDialogAsync((results ?? []).Aggregate("Name: paper\n\n", (current, result) => current + $"Version: {result}\n"));
    }

    private async void TestMSLDownloadUrl(object sender, RoutedEventArgs e)
    {
        var result = await MSLAPI.GetDownloadUrl("paper", "1.21");
        await ShowTextResultContentDialogAsync($"Name: paper\nVersion:1.21\n{result}\n");
    }

    private async void TestMCSLSync(object sender, RoutedEventArgs e)
    {
        var results = await MCSLSync.GetCoreInfo();
        await ShowTextResultContentDialogAsync((results ?? []).Aggregate("", (current, result) => current + $"Name: {result}\n"));
    }

    private async void TestMCSLSyncCore(object sender, RoutedEventArgs e)
    {
        var results = await MCSLSync.GetMinecraftVersions("Paper");
        await ShowTextResultContentDialogAsync((results ?? []).Aggregate("Name: Paper\n\n", (current, result) => current + $"Version: {result}\n\n"));
    }

    private async void TestMCSLSyncCoreVersion(object sender, RoutedEventArgs e)
    {
        var results = await MCSLSync.GetCoreVersions("Paper", "1.20.6");
        var text = (results ?? []).Aggregate("Name: Paper\nVersion: 1.20.6\n\n", (current, result) => current + $"Version: {result}\n\n");
        await ShowTextResultContentDialogAsync(text);
    }

    private async void TestMCSLSyncCoreDetail(object sender, RoutedEventArgs e)
    {
        var result = await MCSLSync.GetCoreDetail("Paper", "1.20.6", "build148");
        await ShowTextResultContentDialogAsync($"Core: {result?.Core}\nMinecraftVersion: {result?.MinecraftVersion}\nCoreVersion: {result?.CoreVersion}\nDownloadUrl: {result?.DownloadUrl}\n");
    }
}
