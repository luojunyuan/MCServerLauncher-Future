using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.Common.ProtoType;
using MCServerLauncher.DaemonClient;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public sealed class SelectMinecraftJavaJvm : CreateStepControl
{
    private readonly CreateInstanceSession _session;
    private readonly TextBox _pathBox;
    private readonly ComboBox _runtimeBox;
    private readonly Button _searchButton;
    private JavaInfo[] _cached = [];

    public SelectMinecraftJavaJvm(CreateInstanceSession session)
        : base("JavaPath", "MinecraftJavaRequirementTip")
    {
        _session = session;
        var row = new StackPanel { Spacing = 8 };
        _pathBox = new TextBox
        {
            PlaceholderText = Texts["JavaPath"],
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _pathBox.TextChanged += (_, _) => IsFinished = !string.IsNullOrWhiteSpace(_pathBox.Text);
        _runtimeBox = new ComboBox
        {
            PlaceholderText = Texts["PleaseSelectJvm"],
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _runtimeBox.SelectionChanged += RuntimeSelected;
        _searchButton = new Button { Content = Texts["Search"] };
        _searchButton.Click += SearchAsync;
        row.Children.Add(_pathBox);
        row.Children.Add(_runtimeBox);
        row.Children.Add(_searchButton);
        Fields.Children.Add(row);
        App.Services.Localization.LanguageChanged += (_, _) =>
        {
            _pathBox.PlaceholderText = Texts["JavaPath"];
            _runtimeBox.PlaceholderText = Texts["PleaseSelectJvm"];
            _searchButton.Content = Texts["Search"];
        };
        _ = LoadJavaListAsync();
    }

    public string Path => _pathBox.Text;

    public override object Data => new CreateInstanceData(CreateInstanceDataType.Path, Path);

    private void RuntimeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_runtimeBox.SelectedIndex >= 0 && _runtimeBox.SelectedIndex < _cached.Length)
        {
            _pathBox.Text = _cached[_runtimeBox.SelectedIndex].Path;
            IsFinished = true;
        }
    }

    private async Task<bool> LoadJavaListAsync(bool notifyFailure = false)
    {
        try
        {
            _cached = await _session.Daemon.GetJavaListAsync();
            PopulateRuntimeList();
            return true;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            if (notifyFailure)
            {
                App.Services.Notifications.Push(
                    Texts["Error"],
                    $"{Texts["SearchJavaError"]}: {ex.Message}",
                    NotificationSeverity.Error,
                    durationMs: 5000,
                    showSystemNotification: false);
            }
            return false;
        }
    }

    private void PopulateRuntimeList()
    {
        _runtimeBox.Items.Clear();
        foreach (var java in _cached)
            _runtimeBox.Items.Add($"({java.Version}, {java.Architecture}) {java.Path}");
    }

    private async void SearchAsync(object sender, RoutedEventArgs e)
    {
        _searchButton.IsEnabled = false;
        try
        {
            if (!await LoadJavaListAsync(notifyFailure: true)) return;
            if (_cached.Length == 0)
            {
                ShowError(Texts["NoJavaFound"]);
                App.Services.Notifications.Push(
                    Texts["Info"],
                    Texts["NoJavaFound"],
                    NotificationSeverity.Warning,
                    durationMs: 3000,
                    showSystemNotification: false);
                return;
            }

            var list = new ListView
            {
                ItemsSource = _cached.Select(java => $"({java.Version}, {java.Architecture}) {java.Path}"),
                SelectedIndex = 0,
                SelectionMode = ListViewSelectionMode.Single,
                MinWidth = 520
            };
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = Texts["PleaseSelectJvm"],
                Content = list,
                PrimaryButtonText = Texts["Continue"],
                CloseButtonText = Texts["Cancel"],
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary
                && list.SelectedIndex >= 0
                && list.SelectedIndex < _cached.Length)
            {
                _pathBox.Text = _cached[list.SelectedIndex].Path;
                IsFinished = true;
            }
        }
        catch (Exception ex)
        {
            ShowError($"{Texts["SearchJavaError"]}: {ex.Message}");
        }
        finally
        {
            _searchButton.IsEnabled = true;
        }
    }
}
