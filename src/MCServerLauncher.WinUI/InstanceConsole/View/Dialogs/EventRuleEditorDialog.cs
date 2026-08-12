using System.Text.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.Common.ProtoType.EventTrigger;
using MCServerLauncher.DaemonClient.Serialization;
using MCServerLauncher.WinUI.Core.Localization;
using Serilog;

namespace MCServerLauncher.WinUI.InstanceConsole.View.Dialogs;

/// <summary>
/// WinUI equivalent of the WinUI EventRuleEditorDialog. The editor works on a
/// deep copy and commits only after the user presses Save.
/// </summary>
public static class EventRuleEditorDialog
{
    private const string ActionSeparatorTag = "EventRuleActionSeparator";

    public static async Task<bool> ShowAsync(XamlRoot root, EventRule target, LocalizedStrings texts)
    {
        var working = Clone(target);
        var name = new TextBox { Text = working.Name, Header = texts["Name"] };
        var description = new TextBox
        {
            Text = working.Description,
            Header = texts["Description"],
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true
        };
        var triggerPanel = new StackPanel { Spacing = 6 };
        var rulesetPanel = new StackPanel { Spacing = 6 };
        var actionPanel = new StackPanel { Spacing = 6 };
        var triggerCondition = CreateChoice(
            texts["ConsoleCommand_EventTrigger_Condition"],
            working.TriggerCondition,
            [
                ("Any", texts["ConsoleCommand_EventTrigger_Any"]),
                ("All", texts["ConsoleCommand_EventTrigger_All"])
            ],
            value => working.TriggerCondition = value);
        var actionMode = CreateChoice(
            texts["ConsoleCommand_EventTrigger_ActionExecutionMode"],
            working.ActionExecutionMode,
            [
                ("Sequential", texts["ConsoleCommand_EventTrigger_Sequential"]),
                ("Parallel", texts["ConsoleCommand_EventTrigger_Parallel"])
            ],
            value =>
            {
                working.ActionExecutionMode = value;
                UpdateActionSeparators(actionPanel, texts, value);
            });

        var content = new StackPanel { Spacing = 10, MinWidth = 560 };
        content.Children.Add(name);
        content.Children.Add(description);
        content.Children.Add(triggerCondition);
        content.Children.Add(actionMode);
        content.Children.Add(BuildSection(texts["ConsoleCommand_EventTrigger_Trigger"], triggerPanel,
            new[] { texts["ConsoleCommand_EventTrigger_ConsoleOutput"], texts["ConsoleCommand_EventTrigger_Schedule"], texts["ConsoleCommand_EventTrigger_InstanceStatus"] },
            type => AddTrigger(working, type, triggerPanel, texts)));
        content.Children.Add(BuildSection(texts["ConsoleCommand_EventTrigger_Ruleset"], rulesetPanel,
            new[] { texts["ConsoleCommand_EventTrigger_AlwaysTrueRuleset"], texts["ConsoleCommand_EventTrigger_AlwaysFalseRuleset"], texts["ConsoleCommand_EventTrigger_InstanceStatus"] },
            type => AddRuleset(working, type, rulesetPanel, texts)));
        content.Children.Add(BuildSection(texts["ConsoleCommand_EventTrigger_Action"], actionPanel,
            new[] { texts["ConsoleCommand_EventTrigger_SendCommand"], texts["ConsoleCommand_EventTrigger_ChangeInstanceStatus"], texts["ConsoleCommand_EventTrigger_SendNotification"] },
            type => AddAction(working, type, actionPanel, texts)));

        foreach (var trigger in working.Triggers) AddTriggerEditor(working, trigger, triggerPanel, texts);
        foreach (var ruleset in working.Rulesets) AddRulesetEditor(working, ruleset, rulesetPanel, texts);
        RebuildActionEditors(working, actionPanel, texts);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = texts["Edit"],
            Content = new ScrollViewer { Content = content, MaxHeight = 720 },
            PrimaryButtonText = texts["Save"],
            CloseButtonText = texts["Cancel"],
            DefaultButton = ContentDialogButton.Primary
        };
        try
        {
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;
        }
        catch (Exception ex)
        {
            // ContentDialog.ShowAsync throws when the app is closing or another dialog
            // is already open; never let that crash the async-void caller.
            Log.Warning(ex, "[WinUI] Event-rule editor dialog failed");
            return false;
        }

        working.Name = name.Text.Trim();
        working.Description = description.Text.Trim();
        CopyInto(target, working);
        return true;
    }

    private static StackPanel BuildSection(string title, StackPanel items, IReadOnlyList<string> addItems, Action<int> add)
    {
        var section = new StackPanel { Spacing = 6 };
        section.Children.Add(new TextBlock { Text = title, FontWeight = Windows.UI.Text.FontWeights.SemiBold });
        var menu = new ComboBox { PlaceholderText = title, ItemsSource = addItems.ToArray() };
        var button = new Button { Content = App.Services.Localization.Get("Add") };
        button.Click += (_, _) =>
        {
            if (menu.SelectedIndex >= 0)
            {
                add(menu.SelectedIndex);
                menu.SelectedIndex = -1;
            }
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(menu);
        row.Children.Add(button);
        section.Children.Add(row);
        section.Children.Add(items);
        return section;
    }

    private static void AddTrigger(EventRule rule, int type, StackPanel panel, LocalizedStrings texts)
    {
        TriggerDefinition trigger = type switch
        {
            1 => new ScheduleTrigger(),
            2 => new InstanceStatusTrigger(),
            _ => new ConsoleOutputTrigger()
        };
        rule.Triggers.Add(trigger);
        AddTriggerEditor(rule, trigger, panel, texts);
    }

    private static void AddRuleset(EventRule rule, int type, StackPanel panel, LocalizedStrings texts)
    {
        RulesetDefinition ruleset = type switch
        {
            1 => new AlwaysFalseRuleset(),
            2 => new InstanceStatusRuleset(),
            _ => new AlwaysTrueRuleset()
        };
        rule.Rulesets.Add(ruleset);
        AddRulesetEditor(rule, ruleset, panel, texts);
    }

    private static void AddAction(EventRule rule, int type, StackPanel panel, LocalizedStrings texts)
    {
        ActionDefinition action = type switch
        {
            1 => new ChangeInstanceStatusAction(),
            2 => new SendNotificationAction(),
            _ => new SendCommandAction()
        };
        rule.Actions.Add(action);
        RebuildActionEditors(rule, panel, texts);
    }

    private static void AddTriggerEditor(EventRule rule, TriggerDefinition trigger, StackPanel panel, LocalizedStrings texts)
    {
        var editor = new StackPanel { Spacing = 4, Margin = new Thickness(8, 2, 0, 2) };
        editor.Children.Add(new TextBlock
        {
            Text = trigger switch
            {
                ConsoleOutputTrigger => texts["ConsoleCommand_EventTrigger_ConsoleOutputTrigger"],
                ScheduleTrigger => texts["ConsoleCommand_EventTrigger_ScheduleTrigger"],
                InstanceStatusTrigger => texts["ConsoleCommand_EventTrigger_InstanceStatusTrigger"],
                _ => trigger.Type
            },
            FontWeight = Windows.UI.Text.FontWeights.SemiBold
        });
        switch (trigger)
        {
            case ConsoleOutputTrigger console:
                AddText(editor, texts["ConsoleCommand_EventTrigger_Pattern"], console.Pattern, value => console.Pattern = value);
                var regex = new CheckBox { Content = texts["ConsoleCommand_EventTrigger_IsRegex"], IsChecked = console.IsRegex };
                regex.Checked += (_, _) => console.IsRegex = true;
                regex.Unchecked += (_, _) => console.IsRegex = false;
                editor.Children.Add(regex);
                break;
            case ScheduleTrigger schedule:
                AddText(editor, texts["ConsoleCommand_EventTrigger_CronExpression"], schedule.CronExpression, value => schedule.CronExpression = value);
                break;
            case InstanceStatusTrigger status:
                AddStatusChoice(editor, status.TargetStatus, texts, value => status.TargetStatus = value);
                break;
        }
        AddRemoveButton(editor, texts["Delete"], () => { rule.Triggers.Remove(trigger); panel.Children.Remove(editor); });
        panel.Children.Add(editor);
    }

    private static void AddRulesetEditor(EventRule rule, RulesetDefinition ruleset, StackPanel panel, LocalizedStrings texts)
    {
        var editor = new StackPanel { Spacing = 4, Margin = new Thickness(8, 2, 0, 2) };
        editor.Children.Add(new TextBlock
        {
            Text = ruleset switch
            {
                AlwaysTrueRuleset => texts["ConsoleCommand_EventTrigger_AlwaysTrueRuleset"],
                AlwaysFalseRuleset => texts["ConsoleCommand_EventTrigger_AlwaysFalseRuleset"],
                InstanceStatusRuleset => texts["ConsoleCommand_EventTrigger_InstanceStatus"],
                _ => ruleset.Type
            },
            FontWeight = Windows.UI.Text.FontWeights.SemiBold
        });
        switch (ruleset)
        {
            case AlwaysTrueRuleset:
                AddDescription(editor, texts["ConsoleCommand_EventTrigger_AlwaysTrueRulesetDescription"]);
                break;
            case AlwaysFalseRuleset:
                AddDescription(editor, texts["ConsoleCommand_EventTrigger_AlwaysFalseRulesetDescription"]);
                break;
            case InstanceStatusRuleset status:
                AddStatusChoice(editor, status.TargetStatus, texts, value => status.TargetStatus = value);
                break;
        }
        AddRemoveButton(editor, texts["Delete"], () => { rule.Rulesets.Remove(ruleset); panel.Children.Remove(editor); });
        panel.Children.Add(editor);
    }

    private static void AddActionEditor(EventRule rule, ActionDefinition action, StackPanel panel, LocalizedStrings texts)
    {
        var editor = new StackPanel { Spacing = 4, Margin = new Thickness(8, 2, 0, 2) };
        editor.Children.Add(new TextBlock
        {
            Text = action switch
            {
                SendCommandAction => texts["ConsoleCommand_EventTrigger_SendCommandAction"],
                ChangeInstanceStatusAction => texts["ConsoleCommand_EventTrigger_ChangeInstanceStatusAction"],
                SendNotificationAction => texts["ConsoleCommand_EventTrigger_SendNotificationAction"],
                _ => action.Type
            },
            FontWeight = Windows.UI.Text.FontWeights.SemiBold
        });
        switch (action)
        {
            case SendCommandAction command:
                AddText(editor, texts["ConsoleCommand_EventTrigger_Command"], command.Command, value => command.Command = value);
                break;
            case ChangeInstanceStatusAction status:
                editor.Children.Add(CreateChoice(
                    texts["ConsoleCommand_EventTrigger_Action"],
                    status.Action,
                    [
                        ("Start", texts["Start"]),
                        ("Stop", texts["Stop"]),
                        ("Restart", texts["Restart"]),
                        ("Kill", texts["Kill"])
                    ],
                    value => status.Action = value));
                break;
            case SendNotificationAction notification:
                AddText(editor, texts["ConsoleCommand_EventTrigger_Title"], notification.Title, value => notification.Title = value);
                AddText(editor, texts["ConsoleCommand_EventTrigger_Message"], notification.Message, value => notification.Message = value);
                editor.Children.Add(CreateChoice(
                    texts["ConsoleCommand_EventTrigger_Severity"],
                    notification.Severity,
                    [
                        ("Info", texts["ConsoleCommand_EventTrigger_Info"]),
                        ("Success", texts["ConsoleCommand_EventTrigger_Success"]),
                        ("Warning", texts["Warning"]),
                        ("Error", texts["Status_Error"])
                    ],
                    value => notification.Severity = value));
                break;
        }
        AddRemoveButton(editor, texts["Delete"], () =>
        {
            rule.Actions.Remove(action);
            RebuildActionEditors(rule, panel, texts);
        });
        panel.Children.Add(editor);
    }

    private static void RebuildActionEditors(EventRule rule, StackPanel panel, LocalizedStrings texts)
    {
        panel.Children.Clear();
        for (var index = 0; index < rule.Actions.Count; index++)
        {
            if (index > 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Tag = ActionSeparatorTag,
                    Text = GetActionSeparatorText(rule.ActionExecutionMode, texts),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Opacity = 0.72
                });
            }

            AddActionEditor(rule, rule.Actions[index], panel, texts);
        }
    }

    private static void UpdateActionSeparators(StackPanel panel, LocalizedStrings texts, string mode)
    {
        foreach (var separator in panel.Children.OfType<TextBlock>()
                     .Where(text => Equals(text.Tag, ActionSeparatorTag)))
        {
            separator.Text = GetActionSeparatorText(mode, texts);
        }
    }

    private static string GetActionSeparatorText(string mode, LocalizedStrings texts) =>
        string.Equals(mode, "Parallel", StringComparison.OrdinalIgnoreCase)
            ? texts["ConsoleCommand_EventTrigger_ParallelSeparator"]
            : texts["ConsoleCommand_EventTrigger_SequentialSeparator"];

    private static void AddStatusChoice(StackPanel panel, string currentValue, LocalizedStrings texts, Action<string> setter)
    {
        panel.Children.Add(CreateChoice(
            texts["ConsoleCommand_EventTrigger_TargetStatus"],
            currentValue,
            [
                ("Running", texts["Status_Running"]),
                ("Stopped", texts["Status_Stopped"]),
                ("Crashed", texts["Status_Crashed"])
            ],
            setter));
    }

    private static ComboBox CreateChoice(
        string header,
        string? currentValue,
        IReadOnlyList<(string Value, string Label)> choices,
        Action<string> setter)
    {
        var input = new ComboBox { Header = header, MinWidth = 200 };
        foreach (var choice in choices)
            input.Items.Add(new ComboBoxItem { Content = choice.Label, Tag = choice.Value });

        input.SelectedIndex = choices
            .Select((choice, index) => (choice, index))
            .Where(item => string.Equals(item.choice.Value, currentValue, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();
        input.SelectionChanged += (_, _) =>
        {
            if (input.SelectedItem is ComboBoxItem { Tag: string value }) setter(value);
        };
        return input;
    }

    private static void AddDescription(StackPanel panel, string text)
    {
        panel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72
        });
    }

    private static void AddText(StackPanel panel, string header, string value, Action<string> setter)
    {
        var input = new TextBox { Header = header, Text = value, TextWrapping = TextWrapping.Wrap };
        input.TextChanged += (_, _) => setter(input.Text);
        panel.Children.Add(input);
    }

    private static void AddRemoveButton(StackPanel panel, string text, Action remove)
    {
        var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => remove();
        panel.Children.Add(button);
    }

    private static EventRule Clone(EventRule source) =>
        JsonSerializer.Deserialize<EventRule>(JsonSerializer.Serialize(source, DaemonClientRpcJsonBoundary.CreateStjOptions()), DaemonClientRpcJsonBoundary.CreateStjOptions())
        ?? new EventRule();

    private static void CopyInto(EventRule target, EventRule source)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        target.IsEnabled = source.IsEnabled;
        target.TriggerCondition = source.TriggerCondition;
        target.ActionExecutionMode = source.ActionExecutionMode;
        target.Triggers = source.Triggers;
        target.Rulesets = source.Rulesets;
        target.Actions = source.Actions;
    }
}
