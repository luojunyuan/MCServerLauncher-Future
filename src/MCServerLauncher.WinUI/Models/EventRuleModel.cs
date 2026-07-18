using System.ComponentModel;
using MCServerLauncher.Common.ProtoType.EventTrigger;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.Models;

public sealed class EventRuleModel : INotifyPropertyChanged
{
    public EventRuleModel(EventRule rule) => Rule = rule;

    public EventRule Rule { get; }
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public string Name => string.IsNullOrWhiteSpace(Rule.Name) ? Rule.Id.ToString() : Rule.Name;
    public string Description => Rule.Description;
    public string Summary => $"{App.Services.Localization.Get("ConsoleCommand_EventTrigger_Trigger")}: {Rule.Triggers.Count}, "
        + $"{App.Services.Localization.Get("ConsoleCommand_EventTrigger_Event")}: {Rule.Actions.Count}";

    public bool IsEnabled
    {
        get => Rule.IsEnabled;
        set
        {
            if (Rule.IsEnabled == value) return;
            Rule.IsEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
    }
}
