using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.Views.Pages;

public sealed partial class StartupErrorPage : Page
{
    private readonly Action? _continueAction;

    public StartupErrorPage(Exception error, Action? continueAction = null)
    {
        _continueAction = continueAction;
        ErrorText = error.ToString();
        InitializeComponent();
    }

    public string ErrorText { get; }
    public string ProductName => "MCServerLauncher Future";
    public LocalizedStrings Texts => App.Services.Localization.Texts;

    private void Exit_Click(object sender, RoutedEventArgs e) => Environment.Exit(1);

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        foreach (var argument in Environment.GetCommandLineArgs().Skip(1))
            startInfo.ArgumentList.Add(argument);

        Process.Start(startInfo);
        Environment.Exit(0);
    }

    private async void Feedback_Click(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/MCSLTeam/MCServerLauncher-Future/issues/new"));
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (_continueAction is not null)
            _continueAction();
        else
            App.Window.RootPage.ContinueAfterStartupError();
    }
}
