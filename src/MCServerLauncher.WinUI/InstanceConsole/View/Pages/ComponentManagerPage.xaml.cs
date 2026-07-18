using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Models;

namespace MCServerLauncher.WinUI.InstanceConsole.View.Pages;

public sealed partial class ComponentManagerPage : UserControl, INotifyPropertyChanged
{
    public ComponentManagerPage()
    {
        InitializeComponent();
        ComponentTabs.SelectedIndex = 0;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public ObservableCollection<ComponentFileModel> Mods { get; } = [];
    public ObservableCollection<ComponentFileModel> Plugins { get; } = [];
    public ListView ItemsView => ComponentTabs.SelectedIndex == 1 ? PluginsList : ModsList;
    public TextBlock StateText => ComponentsStateText;
    public string SelectedFolder => ComponentTabs.SelectedIndex == 1 ? "plugins" : "mods";
    public string AddComponentText => Texts[ComponentTabs.SelectedIndex == 1
        ? "ComponentManager_AddPlugin"
        : "ComponentManager_AddMod"];
    public event EventHandler? LoadRequested;
    public event EventHandler? AddRequested;
    public event RoutedEventHandler? ToggleRequested;
    public event RoutedEventHandler? LocateRequested;
    public event RoutedEventHandler? DeleteRequested;
    public event EventHandler<IReadOnlyList<StorageFile>>? FilesDropped;

    public void SetItems(
        IEnumerable<ComponentFileModel> mods,
        IEnumerable<ComponentFileModel> plugins,
        bool hasMods,
        bool hasPlugins)
    {
        Mods.Clear();
        Plugins.Clear();
        foreach (var item in mods) Mods.Add(item);
        foreach (var item in plugins) Plugins.Add(item);

        ModsTab.IsEnabled = hasMods;
        PluginsTab.IsEnabled = hasPlugins;
        if ((ComponentTabs.SelectedIndex == 0 && !hasMods)
            || (ComponentTabs.SelectedIndex == 1 && !hasPlugins))
        {
            ComponentTabs.SelectedIndex = hasMods ? 0 : hasPlugins ? 1 : -1;
        }

        ApplySupportState(hasMods || hasPlugins);
    }

    public void ApplySupportState(bool supported)
    {
        ComponentToolbar.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
        ComponentTabs.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadComponents_Click(object sender, RoutedEventArgs e) => LoadRequested?.Invoke(this, EventArgs.Empty);
    private void AddComponent_Click(object sender, RoutedEventArgs e) => AddRequested?.Invoke(this, EventArgs.Empty);
    private void ToggleComponent_Click(object sender, RoutedEventArgs e) => ToggleRequested?.Invoke(sender, e);
    private void LocateComponent_Click(object sender, RoutedEventArgs e) => LocateRequested?.Invoke(sender, e);
    private void DeleteComponent_Click(object sender, RoutedEventArgs e) => DeleteRequested?.Invoke(sender, e);
    private void ComponentKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddComponentText)));
        LoadRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Services.Localization.LanguageChanged -= Localization_LanguageChanged;
        App.Services.Localization.LanguageChanged += Localization_LanguageChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        App.Services.Localization.LanguageChanged -= Localization_LanguageChanged;

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddComponentText)));
        foreach (var item in Mods) item.RefreshLocalizedText();
        foreach (var item in Plugins) item.RefreshLocalizedText();
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.Handled = true;
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        var files = items.OfType<StorageFile>().ToArray();
        if (files.Length > 0) FilesDropped?.Invoke(this, files);
    }
}
