using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.Common.ProtoType.EventTrigger;
using MCServerLauncher.DaemonClient.Serialization;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.Models;
using MCServerLauncher.WinUI.InstanceConsole.View.Dialogs;

namespace MCServerLauncher.WinUI.InstanceConsole.View.Pages;

public sealed partial class EventTriggerPage : UserControl
{
    public EventTriggerPage()
    {
        InitializeComponent();
        Loaded += EventTriggerPage_Loaded;
        Unloaded += EventTriggerPage_Unloaded;
        Rules.CollectionChanged += (_, _) => EmptyState.Visibility = Rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public ObservableCollection<EventRuleModel> Rules { get; } = [];
    public event EventHandler? SaveRequested;
    public event EventHandler? ReloadRequested;

    private void EventTriggerPage_Loaded(object sender, RoutedEventArgs e)
    {
        App.Services.Localization.LanguageChanged -= Localization_LanguageChanged;
        App.Services.Localization.LanguageChanged += Localization_LanguageChanged;
        if (App.Services.Settings.Current.App.HideTips.TryGetValue("EventTriggerMultiSelect", out var hidden) && hidden)
            MultiSelectTipBar.IsOpen = false;
    }

    private void EventTriggerPage_Unloaded(object sender, RoutedEventArgs e) =>
        App.Services.Localization.LanguageChanged -= Localization_LanguageChanged;

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        foreach (var rule in Rules) rule.Refresh();
    }

    private void MultiSelectTipBar_Closed(object sender, object e)
    {
        App.Services.Settings.Current.App.HideTips["EventTriggerMultiSelect"] = true;
        _ = App.Services.Settings.SaveAsync();
    }

    public void SetRules(IEnumerable<EventRule> rules)
    {
        Rules.Clear();
        foreach (var rule in rules) Rules.Add(new EventRuleModel(rule));
        EmptyState.Visibility = Rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public List<EventRule> GetRules() => Rules.Select(item => item.Rule).ToList();

    private RelayCommand? _addRuleCommand;
    public ICommand AddRuleCommand => _addRuleCommand ??= new RelayCommand(AddRule);

    private void AddRule()
    {
        Rules.Add(new EventRuleModel(new EventRule
        {
            Name = Texts["EventTrigger_NewRuleName"],
            Description = Texts["EventTrigger_NewRuleDescription"]
        }));
    }

    private void Add_Click(object sender, RoutedEventArgs e) => AddRule();

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not EventRuleModel item || XamlRoot is null) return;
        if (await EventRuleEditorDialog.ShowAsync(XamlRoot, item.Rule, Texts)) item.Refresh();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not EventRuleModel item) return;
        var options = DaemonClientRpcJsonBoundary.CreateStjOptions();
        var copy = JsonSerializer.Deserialize<EventRule>(JsonSerializer.Serialize(item.Rule, options), options);
        if (copy is null) return;
        copy.Id = Guid.NewGuid();
        var baseName = item.Name;
        var copyName = $"{baseName} - Copy";
        var copyCount = 1;
        while (Rules.Any(rule => string.Equals(rule.Name, copyName, StringComparison.Ordinal)))
        {
            copyCount++;
            copyName = $"{baseName} - Copy ({copyCount})";
        }
        copy.Name = copyName;
        foreach (var trigger in copy.Triggers) trigger.Id = Guid.NewGuid();
        foreach (var ruleset in copy.Rulesets) ruleset.Id = Guid.NewGuid();
        foreach (var action in copy.Actions) action.Id = Guid.NewGuid();
        Rules.Add(new EventRuleModel(copy));
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is EventRuleModel item)
            Rules.Remove(item);
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var file = await App.Services.Files.PickFileAsync(App.WindowHandle);
        if (file is null) return;
        try
        {
            var options = DaemonClientRpcJsonBoundary.CreateStjOptions();
            var imported = JsonSerializer.Deserialize<List<EventRule>>(await File.ReadAllTextAsync(file.Path), options) ?? [];
            foreach (var rule in imported)
            {
                rule.Id = Guid.NewGuid();
                foreach (var trigger in rule.Triggers) trigger.Id = Guid.NewGuid();
                foreach (var ruleset in rule.Rulesets) ruleset.Id = Guid.NewGuid();
                foreach (var action in rule.Actions) action.Id = Guid.NewGuid();
                Rules.Add(new EventRuleModel(rule));
            }
            App.Services.Notifications.Push(Texts["Success"], Texts["Success"], NotificationSeverity.Success);
        }
        catch (Exception ex)
        {
            App.Services.Notifications.Push(
                Texts["Error"],
                string.Format(Texts["EventTrigger_LoadRulesFailed"], ex.Message),
                NotificationSeverity.Error);
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var file = await App.Services.Files.PickSaveFileAsync(App.WindowHandle, "EventRules.json");
        if (file is null) return;
        try
        {
            var options = DaemonClientRpcJsonBoundary.CreateStjOptions(writeIndented: true);
            var selected = RulesListView.SelectedItems
                .OfType<EventRuleModel>()
                .Select(item => item.Rule)
                .ToList();
            var rules = selected.Count > 0 ? selected : GetRules();
            await File.WriteAllTextAsync(file.Path, JsonSerializer.Serialize(rules, options));
            App.Services.Notifications.Push(Texts["Success"], Texts["Success"], NotificationSeverity.Success);
        }
        catch (Exception ex)
        {
            App.Services.Notifications.Push(
                Texts["Error"],
                string.Format(Texts["EventTrigger_SaveRulesFailed"], ex.Message),
                NotificationSeverity.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);
    private void Reload_Click(object sender, RoutedEventArgs e) => ReloadRequested?.Invoke(this, EventArgs.Empty);
}
