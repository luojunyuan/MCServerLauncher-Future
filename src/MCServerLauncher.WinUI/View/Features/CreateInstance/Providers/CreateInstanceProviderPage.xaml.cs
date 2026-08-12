using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.DaemonClient;
using MCServerLauncher.WinUI.Core;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Components;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;

public partial class CreateInstanceProviderPage : UserControl
{
    private readonly List<ICreateInstanceStep> _steps = [];

    protected CreateInstanceProviderPage(CreateInstancePage owner, CreateInstanceSession session)
    {
        Owner = owner;
        Session = session;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected CreateInstancePage Owner { get; }
    protected CreateInstanceSession Session { get; }
    public LocalizedStrings Texts => App.Services.Localization.Texts;

    protected void SetSteps(params ICreateInstanceStep[] steps)
    {
        _steps.Clear();
        _steps.AddRange(steps);
        StepsPanel.Children.Clear();
        foreach (var step in _steps)
        {
            if (step is UIElement element) StepsPanel.Children.Add(element);
            step.Changed += Step_Changed;
        }
        UpdateFinishButtonState();
    }

    protected bool HasInput => _steps.Any(step => step.IsFinished);

    protected virtual Task FinishAsync() => Task.CompletedTask;

    protected Task<bool> ConfirmAsync(
        string confirmationMessage,
        InstanceFactorySetting? debugSetting = null) =>
        Owner.ConfirmAsync(Texts["CreateInstanceConfirmationTitle"], confirmationMessage, debugSetting);

    protected async Task<bool> SubmitAsync(InstanceFactorySetting setting, string confirmationMessage)
    {
        if (!await ConfirmAsync(confirmationMessage)) return false;
        return await SubmitConfirmedAsync(setting);
    }

    protected async Task<bool> SubmitConfirmedAsync(InstanceFactorySetting setting)
    {
        Owner.Push(
            Texts["PleaseWait"],
            Texts["CreatingInstance"],
            isClosable: false,
            durationMs: 5000,
            showSystemNotification: false);
        try
        {
            await Session.Daemon.AddInstanceAsync(setting);
            Owner.Push(
                Texts["Success"],
                Texts["InstanceCreatedSuccess"],
                NotificationSeverity.Success,
                durationMs: 3000,
                showSystemNotification: false);
            await Owner.GoBackFromProviderAsync(false);
            return true;
        }
        catch (Exception ex)
        {
            Owner.Push(
                Texts["Error"],
                ex.Message,
                NotificationSeverity.Error,
                durationMs: 5000,
                showSystemNotification: false);
            return false;
        }
    }

    protected async Task<string?> UploadLocalFileAsync(string path)
    {
        if (!File.Exists(path)) return path;
        var fileName = Path.GetFileName(path);
        var daemonPath = $"caches/downloads/{fileName}";
        Owner.Push(
            Texts["PleaseWait"],
            Texts["UploadingFile"],
            isClosable: false,
            showSystemNotification: false);
        try
        {
            var upload = await Session.Daemon.UploadFileAsync(path, daemonPath, 1024 * 1024);
            if (upload.NetworkLoadTask is not null) await upload.NetworkLoadTask;
            if (!upload.Done)
            {
                Owner.Push(
                    Texts["Error"],
                    Texts["FileUploadFailed"],
                    NotificationSeverity.Error,
                    durationMs: 5000,
                    showSystemNotification: false);
                return null;
            }
            return daemonPath;
        }
        catch (Exception ex)
        {
            Owner.Push(
                Texts["Error"],
                ex.Message,
                NotificationSeverity.Error,
                durationMs: 5000,
                showSystemNotification: false);
            return null;
        }
    }

    protected string BuildConfirmation(string instanceType, string instanceName, params (string Key, string Value)[] values)
    {
        var builder = new System.Text.StringBuilder(Texts["CreateInstanceConfirmationMessage"]);
        builder.AppendLine($"{Texts["InstanceName"]}: {instanceName}");
        builder.AppendLine($"{Texts["InstanceType"]}: {instanceType}");
        foreach (var (key, value) in values) builder.AppendLine($"{Texts[key]}: {value}");
        return builder.ToString();
    }

    protected bool ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Owner.Push(Texts["Error"], Texts["CreateInstanceMissingDataError"], NotificationSeverity.Error);
            return false;
        }
        if (value is "." or ".." || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Any(char.IsControl))
        {
            Owner.Push(Texts["Error"], $"{Texts["InstanceName"]}: {Texts["CreateInstanceMissingDataError"]}", NotificationSeverity.Error);
            return false;
        }
        return true;
    }

    protected bool ValidateJava(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)
            || (value.StartsWith("(", StringComparison.Ordinal) && value.Contains(") ", StringComparison.Ordinal)))
        {
            Owner.Push(Texts["Error"], $"{Texts["JavaPath"]}: {Texts["CreateInstanceMissingDataError"]}", NotificationSeverity.Error);
            return false;
        }
        return true;
    }

    protected bool ValidateJar(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)
            || !Path.GetExtension(value).Equals(".jar", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(value))
        {
            Owner.Push(Texts["Error"], $"{Texts["CorePath"]}: {Texts["CreateInstanceMissingDataError"]}", NotificationSeverity.Error);
            return false;
        }
        return true;
    }

    protected static string[] SplitCommandLine(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void Step_Changed(object? sender, EventArgs e) => UpdateFinishButtonState();

    private void UpdateFinishButtonState() => FinishButton.IsEnabled = _steps.Count > 0 && _steps.All(step => step.IsFinished);

    private void Back_Click(object sender, RoutedEventArgs e) =>
        Owner.GoBackFromProviderAsync(HasInput).FireAndForget("CreateInstanceProviderPage.Back_Click");

    private void Finish_Click(object sender, RoutedEventArgs e) =>
        Finish_ClickCore().FireAndForget("CreateInstanceProviderPage.Finish_Click");

    private async Task Finish_ClickCore()
    {
        FinishButton.IsEnabled = false;
        try { await FinishAsync(); }
        finally { UpdateFinishButtonState(); }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        BackButton.Content = Texts["Back"];
        FinishButton.Content = Texts["Continue"];
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Services.Localization.LanguageChanged -= OnLanguageChanged;
        App.Services.Localization.LanguageChanged += OnLanguageChanged;
        OnLanguageChanged(this, EventArgs.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        App.Services.Localization.LanguageChanged -= OnLanguageChanged;
}
