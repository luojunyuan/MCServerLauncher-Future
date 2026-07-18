using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.Common.Minecraft;
using MCServerLauncher.WinUI.Core.Storage;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public abstract class LoaderSetStep : CreateStepControl
{
    protected readonly ComboBox MinecraftVersionBox;
    protected readonly ComboBox LoaderVersionBox;
    protected readonly CheckBox StableVersionsBox;
    protected readonly Button RefreshMinecraftButton;
    protected readonly Button RefreshLoaderButton;
    private bool _loaded;

    protected LoaderSetStep(string loaderKey, string descriptionKey, bool showStableMinecraft, bool showStableLoader = false)
        : base("MinecraftVersion", descriptionKey)
    {
        var minecraftPanel = new StackPanel { Spacing = 6 };
        minecraftPanel.Children.Add(new TextBlock { Text = Texts["MinecraftVersion"] });
        var minecraftRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        MinecraftVersionBox = new ComboBox { MinWidth = 390, HorizontalAlignment = HorizontalAlignment.Stretch };
        RefreshMinecraftButton = new Button { Content = Texts["Refresh"] };
        RefreshMinecraftButton.Click += async (_, _) => await RefreshMinecraftAsync();
        minecraftRow.Children.Add(MinecraftVersionBox);
        minecraftRow.Children.Add(RefreshMinecraftButton);
        minecraftPanel.Children.Add(minecraftRow);
        if (showStableMinecraft)
        {
            StableVersionsBox = new CheckBox { Content = Texts["OnlyShowReleaseVersion"], IsChecked = true };
            StableVersionsBox.Checked += (_, _) => FilterMinecraftVersions();
            StableVersionsBox.Unchecked += (_, _) => FilterMinecraftVersions();
            minecraftPanel.Children.Add(StableVersionsBox);
        }
        else
        {
            StableVersionsBox = new CheckBox { Visibility = Visibility.Collapsed };
        }

        var loaderPanel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        loaderPanel.Children.Add(new TextBlock { Text = Texts[loaderKey] });
        var loaderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        LoaderVersionBox = new ComboBox { MinWidth = 390, IsEnabled = false };
        RefreshLoaderButton = new Button { Content = Texts["Refresh"], IsEnabled = false };
        RefreshLoaderButton.Click += async (_, _) => await RefreshLoaderAsync();
        loaderRow.Children.Add(LoaderVersionBox);
        loaderRow.Children.Add(RefreshLoaderButton);
        loaderPanel.Children.Add(loaderRow);
        if (showStableLoader)
        {
            var stableLoader = new CheckBox { Content = Texts["OnlyShowReleaseVersion"], IsChecked = true };
            stableLoader.Checked += (_, _) => FilterLoaderVersions();
            stableLoader.Unchecked += (_, _) => FilterLoaderVersions();
            loaderPanel.Children.Add(stableLoader);
            StableLoaderBox = stableLoader;
        }

        MinecraftVersionBox.SelectionChanged += async (_, _) =>
        {
            UpdateFinished();
            await MinecraftVersionChangedAsync();
        };
        LoaderVersionBox.SelectionChanged += (_, _) => UpdateFinished();
        Fields.Children.Add(minecraftPanel);
        Fields.Children.Add(loaderPanel);
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await RefreshMinecraftAsync();
            await RefreshLoaderAsync();
        };
        App.Services.Localization.LanguageChanged += (_, _) =>
        {
            RefreshLocalizedText();
            RefreshMinecraftButton.Content = Texts["Refresh"];
            RefreshLoaderButton.Content = Texts["Refresh"];
        };
    }

    protected CheckBox? StableLoaderBox { get; private set; }
    protected List<string> MinecraftVersions { get; private set; } = [];
    protected List<string> LoaderVersions { get; private set; } = [];
    public string SelectedMinecraftVersion => MinecraftVersionBox.SelectedItem?.ToString() ?? string.Empty;
    public string SelectedLoaderVersion => LoaderVersionBox.SelectedItem?.ToString() ?? string.Empty;

    public override object Data => new CreateInstanceData(
        CreateInstanceDataType.Struct,
        new MinecraftLoaderVersion(SelectedMinecraftVersion, SelectedLoaderVersion));

    protected bool UseMirror(string loader) => loader switch
    {
        "Forge" => App.Services.Settings.Current.InstanceCreation.UseMirrorForMinecraftForgeInstall,
        "NeoForge" => App.Services.Settings.Current.InstanceCreation.UseMirrorForMinecraftNeoForgeInstall,
        "Fabric" => App.Services.Settings.Current.InstanceCreation.UseMirrorForMinecraftFabricInstall,
        "Quilt" => App.Services.Settings.Current.InstanceCreation.UseMirrorForMinecraftQuiltInstall,
        _ => false
    };

    protected void SetMinecraftVersions(IEnumerable<string?> versions)
    {
        MinecraftVersions = McVersionSequencer.Sequence(versions.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
        FilterMinecraftVersions();
    }

    protected void SetLoaderVersions(IEnumerable<string?> versions)
    {
        LoaderVersions = versions.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList();
        FilterLoaderVersions();
    }

    protected void FilterMinecraftVersions()
    {
        var selected = SelectedMinecraftVersion;
        var values = StableVersionsBox.Visibility == Visibility.Visible && StableVersionsBox.IsChecked == true
            ? MinecraftVersions.Where(IsStableVersion).ToList()
            : MinecraftVersions.ToList();
        MinecraftVersionBox.ItemsSource = values;
        if (!string.IsNullOrWhiteSpace(selected))
            MinecraftVersionBox.SelectedItem = values.Contains(selected) ? selected : null;
        UpdateFinished();
    }

    protected void FilterLoaderVersions()
    {
        var selected = SelectedLoaderVersion;
        var values = StableLoaderBox?.IsChecked == true
            ? LoaderVersions.Where(IsStableVersion).ToList()
            : LoaderVersions.ToList();
        LoaderVersionBox.ItemsSource = values;
        if (!string.IsNullOrWhiteSpace(selected))
            LoaderVersionBox.SelectedItem = values.Contains(selected) ? selected : null;
        UpdateFinished();
    }

    protected async Task RefreshMinecraftAsync()
    {
        try
        {
            RefreshMinecraftButton.IsEnabled = false;
            await FetchMinecraftVersionsAsync();
            ShowError(string.Empty);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            RefreshMinecraftButton.IsEnabled = true;
        }
    }

    protected async Task RefreshLoaderAsync()
    {
        try
        {
            RefreshLoaderButton.IsEnabled = false;
            await FetchLoaderVersionsAsync();
            ShowError(string.Empty);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            RefreshLoaderButton.IsEnabled = true;
        }
    }

    protected virtual Task MinecraftVersionChangedAsync() => Task.CompletedTask;
    protected abstract Task FetchMinecraftVersionsAsync();
    protected abstract Task FetchLoaderVersionsAsync();
    private void UpdateFinished()
    {
        IsFinished = !string.IsNullOrWhiteSpace(SelectedMinecraftVersion)
                     && !string.IsNullOrWhiteSpace(SelectedLoaderVersion);
    }

    private static bool IsStableVersion(string value) =>
        !value.Contains("snapshot", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("pre", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("rc", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("beta", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("alpha", StringComparison.OrdinalIgnoreCase);
}
