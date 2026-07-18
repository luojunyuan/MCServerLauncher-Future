using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Models;

namespace MCServerLauncher.WinUI.InstanceConsole.View.Pages;

public sealed partial class InstanceSettingsPage : UserControl
{
    private bool _canEdit;
    private SettingsSnapshot? _snapshot;

    public InstanceSettingsPage()
    {
        InitializeComponent();
        InstanceNameSettingsTextBox.TextChanged += InputChanged;
        JavaPathSettingsTextBox.TextChanged += InputChanged;
        VersionSettingsTextBox.TextChanged += InputChanged;
        ForceRerunInstallerCheckBox.Checked += InputChanged;
        ForceRerunInstallerCheckBox.Unchecked += InputChanged;
        Arguments.CollectionChanged += Arguments_CollectionChanged;
    }

    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public ObservableCollection<JvmArgumentItemModel> Arguments { get; } = [];
    public TextBox NameInput => InstanceNameSettingsTextBox;
    public TextBox JavaInput => JavaPathSettingsTextBox;
    public TextBox VersionInput => VersionSettingsTextBox;
    public TextBlock StateText => InstanceSettingsStateText;
    public TextBlock InstanceIdText => InstanceIdTextBlock;
    public TextBlock TargetInfo => TargetText;
    public TextBox ReplacementCoreInput => ReplacementCoreTextBox;
    public CheckBox ForceRerunInstallerInput => ForceRerunInstallerCheckBox;
    public ComboBox InstanceTypeSelector => InstanceTypeBox;
    public Button SaveButton => SaveInstanceSettingsButton;
    public event EventHandler? SaveRequested;
    public event EventHandler? ReloadRequested;
    public event EventHandler? ScanJavaRequested;
    public event EventHandler? SelectReplacementCoreRequested;
    public event EventHandler? ClearReplacementCoreRequested;
    public event EventHandler? HelperRequested;

    public void BeginLoad()
    {
        _snapshot = null;
        SaveInstanceSettingsButton.Visibility = Visibility.Collapsed;
    }

    public void MarkLoaded()
    {
        _snapshot = CaptureSnapshot();
        UpdateSaveState();
    }

    public void SetInstanceTypeOptions(IEnumerable<InstanceType> types, InstanceType selected)
    {
        InstanceTypeBox.Items.Clear();
        foreach (var type in types.Distinct()) InstanceTypeBox.Items.Add(type);
        InstanceTypeBox.SelectedItem = selected;
    }

    public InstanceType SelectedInstanceType => InstanceTypeBox.SelectedItem is InstanceType type ? type : InstanceType.Universal;

    public void SetArguments(IEnumerable<string> arguments)
    {
        foreach (var item in Arguments)
            item.PropertyChanged -= Argument_PropertyChanged;
        Arguments.Clear();
        foreach (var argument in arguments.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var item = new JvmArgumentItemModel(argument);
            item.PropertyChanged += Argument_PropertyChanged;
            Arguments.Add(item);
        }
    }

    public string[] GetArguments() => Arguments.Select(item => item.Argument).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

    public string ReplacementCorePath => ReplacementCoreTextBox.Text.Trim();
    public bool ForceRerunInstaller => ForceRerunInstallerCheckBox.IsChecked == true;

    public void SetReplacementCore(string? path)
    {
        ReplacementCoreTextBox.Text = path ?? string.Empty;
        ClearReplacementCoreButton.IsEnabled = _canEdit
            && SelectedInstanceType.IsMinecraftJavaRuntimeType()
            && !string.IsNullOrWhiteSpace(ReplacementCoreTextBox.Text);
        UpdateSaveState();
    }

    public void ApplyEditState(bool canEdit, bool advanced, bool installerBased)
    {
        _canEdit = canEdit;
        InstanceNameSettingsTextBox.IsEnabled = canEdit;
        InstanceTypeBox.IsEnabled = canEdit;
        JavaPathSettingsTextBox.IsEnabled = canEdit && advanced;
        ScanJavaButton.IsEnabled = canEdit && advanced;
        VersionSettingsTextBox.IsEnabled = canEdit;
        NewArgumentTextBox.IsEnabled = canEdit && advanced;
        AddArgumentButton.IsEnabled = canEdit && advanced;
        JvmArgumentsList.IsEnabled = canEdit && advanced;
        ReplacementCoreTextBox.IsEnabled = canEdit && advanced;
        SelectReplacementCoreButton.IsEnabled = canEdit && advanced;
        ClearReplacementCoreButton.IsEnabled = canEdit && advanced && !string.IsNullOrWhiteSpace(ReplacementCoreTextBox.Text);
        ForceRerunInstallerCheckBox.IsEnabled = canEdit && installerBased;
        BasicModeNotice.Visibility = advanced ? Visibility.Collapsed : Visibility.Visible;
        JavaRuntimeSection.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        JvmArgumentsSection.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        CoreReplacementSection.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        InstallerSection.Visibility = installerBased ? Visibility.Visible : Visibility.Collapsed;
        UpdateSaveState();
    }

    public void AddArgument(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument)) return;
        var item = new JvmArgumentItemModel(argument);
        item.PropertyChanged += Argument_PropertyChanged;
        Arguments.Add(item);
    }

    private void InstanceType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InstanceTypeBox.SelectedItem is not InstanceType type) return;
        ApplyEditState(
            _canEdit,
            type.IsMinecraftJavaRuntimeType(),
            type is InstanceType.MCForge or InstanceType.MCNeoForge or InstanceType.MCCleanroom);
        UpdateSaveState();
    }

    private void AddArgument_Click(object sender, RoutedEventArgs e)
    {
        var argument = NewArgumentTextBox.Text.Trim();
        if (argument.Length == 0) return;
        AddArgument(argument);
        NewArgumentTextBox.Text = string.Empty;
    }

    private void RemoveArgument_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not JvmArgumentItemModel item) return;
        item.PropertyChanged -= Argument_PropertyChanged;
        Arguments.Remove(item);
    }

    private void ScanJava_Click(object sender, RoutedEventArgs e) => ScanJavaRequested?.Invoke(this, EventArgs.Empty);
    private void Helper_Click(object sender, RoutedEventArgs e) => HelperRequested?.Invoke(this, EventArgs.Empty);
    private void SelectReplacementCore_Click(object sender, RoutedEventArgs e) => SelectReplacementCoreRequested?.Invoke(this, EventArgs.Empty);
    private void ClearReplacementCore_Click(object sender, RoutedEventArgs e)
    {
        SetReplacementCore(string.Empty);
        ClearReplacementCoreRequested?.Invoke(this, EventArgs.Empty);
    }
    private void Save_Click(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);
    private void Reload_Click(object sender, RoutedEventArgs e) => ReloadRequested?.Invoke(this, EventArgs.Empty);

    private void InputChanged(object sender, RoutedEventArgs e) => UpdateSaveState();
    private void InputChanged(object sender, TextChangedEventArgs e) => UpdateSaveState();
    private void Argument_PropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateSaveState();

    private void Arguments_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateSaveState();

    private void UpdateSaveState()
    {
        var hasChanges = _snapshot is not null && !_snapshot.Equals(CaptureSnapshot());
        SaveInstanceSettingsButton.IsEnabled = _canEdit && hasChanges;
        SaveInstanceSettingsButton.Visibility = _canEdit && hasChanges
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private SettingsSnapshot CaptureSnapshot() => new(
        InstanceNameSettingsTextBox.Text,
        SelectedInstanceType,
        JavaPathSettingsTextBox.Text,
        VersionSettingsTextBox.Text,
        string.Join('\u001f', GetArguments()),
        ReplacementCoreTextBox.Text,
        ForceRerunInstallerCheckBox.IsChecked == true);

    private sealed record SettingsSnapshot(
        string Name,
        InstanceType InstanceType,
        string JavaPath,
        string Version,
        string Arguments,
        string ReplacementCorePath,
        bool ForceRerunInstaller);
}
