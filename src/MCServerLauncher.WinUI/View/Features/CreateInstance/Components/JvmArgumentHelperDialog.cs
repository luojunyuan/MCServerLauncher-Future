using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Services;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

internal static class JvmArgumentHelperDialog
{
    private static readonly string[] AdvancedTemplateArguments =
    [
        "-XX:+UseG1GC",
        "-XX:+UnlockExperimentalVMOptions",
        "-XX:+ParallelRefProcEnabled",
        "-XX:MaxGCPauseMillis=200",
        "-XX:+UnlockExperimentalVMOptions",
        "-XX:+DisableExplicitGC",
        "-XX:+AlwaysPreTouch",
        "-XX:G1NewSizePercent=30",
        "-XX:G1MaxNewSizePercent=40",
        "-XX:G1HeapRegionSize=8M",
        "-XX:G1ReservePercent=20",
        "-XX:G1HeapWastePercent=5",
        "-XX:G1MixedGCCountTarget=4",
        "-XX:InitiatingHeapOccupancyPercent=15",
        "-XX:G1MixedGCLiveThresholdPercent=90",
        "-XX:G1RSetUpdatingPauseTimePercent=5",
        "-XX:SurvivorRatio=32",
        "-XX:+PerfDisableSharedMem",
        "-XX:MaxTenuringThreshold=1",
        "-Dusing.aikars.flags=https://mcflags.emc.gs",
        "-Daikars.new.flags=true"
    ];

    public static async Task<string[]?> ShowAsync(XamlRoot? root, LocalizedStrings texts)
    {
        if (root is null) return null;

        var minimumMemory = new TextBox { Header = texts["MinimumMemory"], Text = "1024" };
        var maximumMemory = new TextBox { Header = texts["MaximumMemory"], Text = "1024" };
        var memoryUnit = new ComboBox { SelectedIndex = 0, ItemsSource = new[] { "M", "G" } };
        var encoding = new TextBox { Header = texts["Codecs"], PlaceholderText = "utf-8, gbk, ..." };
        var template = new ComboBox
        {
            Header = texts["Optimization"],
            SelectedIndex = 0,
            ItemsSource = new[]
            {
                texts["Unused"],
                texts["JvmArgBasicTemplate"],
                texts["JvmArgAdvancedTemplate"]
            }
        };

        var memoryRow = new Grid { ColumnSpacing = 8 };
        memoryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        memoryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        memoryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        memoryRow.Children.Add(minimumMemory);
        Grid.SetColumn(maximumMemory, 1);
        memoryRow.Children.Add(maximumMemory);
        Grid.SetColumn(memoryUnit, 2);
        memoryRow.Children.Add(memoryUnit);

        var panel = new StackPanel { Spacing = 10, MinWidth = 420 };
        panel.Children.Add(new TextBlock { Text = texts["CreateInstance_MinecraftJvmRam_Title"], FontWeight = Windows.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = texts["CreateInstance_MinecraftJvmRam_Description"], TextWrapping = TextWrapping.Wrap, Opacity = 0.72 });
        panel.Children.Add(memoryRow);
        panel.Children.Add(encoding);
        panel.Children.Add(new TextBlock { Text = texts["PreventGarbageTextTip"], TextWrapping = TextWrapping.Wrap, Opacity = 0.72 });
        panel.Children.Add(template);
        panel.Children.Add(new Microsoft.UI.Xaml.Controls.InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
            Title = texts["Warning"],
            Message = texts["JvmArgTemplateSameMemTip"]
        });

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = texts["JvmArgHelper"],
            Content = new ScrollViewer { Content = panel, MaxHeight = 520 },
            PrimaryButtonText = texts["Continue"],
            SecondaryButtonText = texts["Cancel"],
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

        if (!long.TryParse(minimumMemory.Text.Trim(), out var minimum)
            || !long.TryParse(maximumMemory.Text.Trim(), out var maximum)
            || minimum < 0
            || maximum < 0)
        {
            App.Services.Notifications.Push(
                texts["Error"],
                texts["CreateInstanceMissingDataError"],
                NotificationSeverity.Error);
            return null;
        }

        var suffix = memoryUnit.SelectedIndex == 1 ? "G" : "M";
        var arguments = new List<string>
        {
            $"-Xms{minimum}{suffix}",
            $"-Xmx{maximum}{suffix}"
        };
        if (!string.IsNullOrWhiteSpace(encoding.Text))
            arguments.Add($"-Dfile.encoding={encoding.Text.Trim()}");
        if (template.SelectedIndex == 1)
            arguments.Add("-XX:+AggressiveOpts");
        else if (template.SelectedIndex == 2)
            arguments.AddRange(AdvancedTemplateArguments);

        return arguments.ToArray();
    }
}
