using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Models;

namespace MCServerLauncher.WinUI.InstanceConsole.View.Pages;

public sealed partial class ComponentManagerPage : UserControl, INotifyPropertyChanged
{
    private readonly DispatcherTimer _loadingTimer;
    private bool _isLoaded;

    public ComponentManagerPage()
    {
        InitializeComponent();
        AddComponentCommand = new RelayCommand(() => AddRequested?.Invoke(this, EventArgs.Empty));
        RefreshCommand = new RelayCommand(() => LoadRequested?.Invoke(this, EventArgs.Empty));
        _loadingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _loadingTimer.Tick += (_, _) =>
        {
            ComponentLoadingLayer.Visibility = Visibility.Collapsed;
            _loadingTimer.Stop();
        };

        ComponentTabs.SelectedIndex = 0;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public ObservableCollection<ComponentFileModel> Mods { get; } = [];
    public ObservableCollection<ComponentFileModel> Plugins { get; } = [];
    public ICommand AddComponentCommand { get; }
    public ICommand RefreshCommand { get; }
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
        HideLoading();
    }

    public void ApplySupportState(bool supported)
    {
        ComponentToolbar.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
        ComponentTabs.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
        UnsupportedTip.Visibility = supported ? Visibility.Collapsed : Visibility.Visible;
        if (supported) UpdateEmptyTips();
    }

    private void UpdateEmptyTips()
    {
        var modsEmpty = ModsTab.IsEnabled && Mods.Count == 0;
        var pluginsEmpty = PluginsTab.IsEnabled && Plugins.Count == 0;
        ModsEmptyTip.Visibility = modsEmpty ? Visibility.Visible : Visibility.Collapsed;
        PluginsEmptyTip.Visibility = pluginsEmpty ? Visibility.Visible : Visibility.Collapsed;
        ModsList.Visibility = modsEmpty ? Visibility.Collapsed : Visibility.Visible;
        PluginsList.Visibility = pluginsEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LoadComponents_Click(object sender, RoutedEventArgs e)
    {
        ShowLoading();
        LoadRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddComponent_Click(object sender, RoutedEventArgs e) => AddRequested?.Invoke(this, EventArgs.Empty);
    private void ToggleComponent_Click(object sender, RoutedEventArgs e) => ToggleRequested?.Invoke(ToButtonSender(sender), e);
    private void LocateComponent_Click(object sender, RoutedEventArgs e) => LocateRequested?.Invoke(ToButtonSender(sender), e);
    private void DeleteComponent_Click(object sender, RoutedEventArgs e) => DeleteRequested?.Invoke(ToButtonSender(sender), e);
    private void ComponentKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddComponentText)));
        if (_isLoaded) ShowLoading();
        LoadRequested?.Invoke(this, EventArgs.Empty);
    }

    private static Button? ToButtonSender(object sender)
    {
        if (sender is Button button) return button;
        if (sender is MenuFlyoutItem { Tag: ComponentFileModel item }) return new Button { Tag = item };
        return null;
    }

    private void ShowLoading()
    {
        ComponentLoadingLayer.Visibility = Visibility.Visible;
        _loadingTimer.Stop();
        _loadingTimer.Start();
    }

    private void HideLoading()
    {
        ComponentLoadingLayer.Visibility = Visibility.Collapsed;
        _loadingTimer.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        App.Services.Localization.LanguageChanged -= Localization_LanguageChanged;
        App.Services.Localization.LanguageChanged += Localization_LanguageChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        HideLoading();
        App.Services.Localization.LanguageChanged -= Localization_LanguageChanged;
    }

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
