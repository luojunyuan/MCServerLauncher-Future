using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public sealed partial class SetMinecraftJavaJvmArgument : CreateStepControl
{
    private readonly StackPanel _argumentPanel;
    private readonly TextBox _newArgument;
    private readonly List<string> _arguments = [];
    private readonly Button _addButton;
    private readonly Button _helperButton;

    public SetMinecraftJavaJvmArgument()
        : base("CreateInstance_MinecraftJavaJvmArgument_Title", "CreateInstance_MinecraftJavaJvmArgument_Description")
    {
        var warning = new TextBlock { Text = Texts["NoJarFileJvmArgument"], TextWrapping = TextWrapping.Wrap, Opacity = 0.78 };
        _newArgument = new TextBox { PlaceholderText = Texts["CreateInstance_MinecraftJavaJvmArgument_Title"] };
        _addButton = new Button { Content = Texts["CreateInstance_MinecraftJavaJvmArgument_AddArgument"] };
        _addButton.Click += AddArgument;
        _helperButton = new Button { Content = Texts["JvmArgHelper"] };
        _helperButton.Click += ShowHelperAsync;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(_addButton);
        buttons.Children.Add(_helperButton);
        _argumentPanel = new StackPanel { Spacing = 5 };
        Fields.Children.Add(warning);
        Fields.Children.Add(_newArgument);
        Fields.Children.Add(buttons);
        Fields.Children.Add(_argumentPanel);
        RegisterLanguageChangedHandler((_, _) =>
        {
            warning.Text = Texts["NoJarFileJvmArgument"];
            _newArgument.PlaceholderText = Texts["CreateInstance_MinecraftJavaJvmArgument_Title"];
            _addButton.Content = Texts["CreateInstance_MinecraftJavaJvmArgument_AddArgument"];
            _helperButton.Content = Texts["JvmArgHelper"];
            RebuildArguments();
        });
        IsFinished = true;
    }

    public string[] Arguments => _arguments.ToArray();
    public override object Data => new CreateInstanceData(CreateInstanceDataType.List, Arguments);

    private void AddArgument(object sender, RoutedEventArgs e)
    {
        var value = _newArgument.Text.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        _arguments.Add(value);
        _newArgument.Text = string.Empty;
        RebuildArguments();
        RaiseChanged();
    }

    private void RebuildArguments()
    {
        _argumentPanel.Children.Clear();
        foreach (var argument in _arguments.ToArray())
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = argument, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            var delete = new Button { Content = Texts["Delete"] };
            delete.Click += (_, _) =>
            {
                _arguments.Remove(argument);
                RebuildArguments();
                RaiseChanged();
            };
            Grid.SetColumn(delete, 1);
            row.Children.Add(delete);
            _argumentPanel.Children.Add(row);
        }
    }

    private void ShowHelperAsync(object sender, RoutedEventArgs e) =>
        ShowHelperAsyncCore().FireAndForget("SetMinecraftJavaJvmArgument.ShowHelperAsync");

    private async Task ShowHelperAsyncCore()
    {
        var arguments = await JvmArgumentHelperDialog.ShowAsync(XamlRoot, Texts);
        if (arguments is null) return;
        _arguments.AddRange(arguments);
        RebuildArguments();
        RaiseChanged();
    }
}
