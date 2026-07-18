using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.InstanceConsole.View.Pages;

public sealed partial class BoardPage : UserControl
{
    private bool _addressVisible;
    private string _address = string.Empty;

    public BoardPage() => InitializeComponent();

    public LocalizedStrings Texts => App.Services.Localization.Texts;

    public void UpdateReport(InstanceReport? report)
    {
        if (report is null)
        {
            AddressCard.Visibility = Visibility.Collapsed;
            PlayerCard.Visibility = Visibility.Collapsed;
            return;
        }

        var showMinecraftWidgets = report.Config.InstanceType.SupportsMinecraftBoardWidgets();
        AddressCard.Visibility = showMinecraftWidgets ? Visibility.Visible : Visibility.Collapsed;
        PlayerCard.Visibility = showMinecraftWidgets ? Visibility.Visible : Visibility.Collapsed;

        var memoryBytes = Math.Max(0L, report.PerformanceCounter.Memory);
        var memoryMb = memoryBytes / 1024d / 1024d;
        MemoryStatusTextBlock.Text = $"{memoryMb:F2} MB";
        MemoryStatusProgressBar.Value = Math.Clamp(memoryMb / 10d, 0d, 100d);

        var cpu = double.IsFinite(report.PerformanceCounter.Cpu)
            ? Math.Clamp(report.PerformanceCounter.Cpu, 0d, 100d)
            : 0d;
        CpuStatusTextBlock.Text = $"{cpu:F2} %";
        CpuStatusProgressBar.Value = cpu;

        var ip = report.Properties.TryGetValue("server-ip", out var serverIp) ? serverIp : string.Empty;
        var port = report.Properties.TryGetValue("server-port", out var serverPort) ? serverPort : string.Empty;
        _address = string.IsNullOrWhiteSpace(ip) ? Texts["Status_LoadFailed"] : ip;
        if (!string.IsNullOrWhiteSpace(port) && !string.Equals(_address, Texts["Status_LoadFailed"], StringComparison.Ordinal))
            _address = $"{_address}:{port}";
        AddressTextBox.Text = _address;

        PlayerListView.Items.Clear();
        foreach (var player in report.Players ?? [])
        {
            PlayerListView.Items.Add(new TextBlock
            {
                Text = $"{player.Name} ({player.Uuid})",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2)
            });
        }
    }

    public void UpdateLatency(long? latency) =>
        WebSocketPingStatusTextBlock.Text = latency.HasValue ? $"{latency.Value} ms" : Texts["Status_LoadFailed"];

    private void ToggleAddress_Click(object sender, RoutedEventArgs e)
    {
        _addressVisible = !_addressVisible;
        AddressTextBox.Visibility = _addressVisible ? Visibility.Visible : Visibility.Collapsed;
        ToggleAddressButton.Content = Texts[_addressVisible ? "ClickToHide" : "ClickToView"];
    }
}
