using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
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

        // The instance report only exposes the process working-set as
        // InstancePerformanceCounter.Memory (used bytes); the protocol does not
        // report a "total" memory figure, so the used-MB value is preserved and
        // the progress bar keeps the WinUI-equivalent 10 GB fallback scale.
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

        DiffPlayerList(report.Players ?? []);
    }

    /// <summary>
    /// Reconciles the player list against the incoming report by UUID so an unchanged
    /// player set does not tear down and rebuild the (unvirtualized) ListView every
    /// report tick.
    /// </summary>
    private void DiffPlayerList(IEnumerable<Player> players)
    {
        var incoming = new HashSet<Guid>(players.Select(player => player.Uuid));

        // Remove players that are no longer online.
        var existing = PlayerListView.Items.Cast<Player>().ToArray();
        for (var i = existing.Length - 1; i >= 0; i--)
        {
            if (!incoming.Contains(existing[i].Uuid))
                PlayerListView.Items.RemoveAt(i);
        }

        // Append newly-appeared players, preserving report order.
        var seen = new HashSet<Guid>(PlayerListView.Items.Cast<Player>().Select(player => player.Uuid));
        foreach (var player in players)
        {
            if (seen.Add(player.Uuid))
                PlayerListView.Items.Add(player);
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

    private void TogglePlayerIp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var item = FindAncestor<ListViewItem>(button);
        if (item is null || item.ContentTemplateRoot is not FrameworkElement root) return;

        var ipText = root.FindName("PlayerIpText") as TextBlock;
        if (ipText is null) return;

        // The player model has no real IP (only Name + Uuid); mirror the WinUI
        // PlayerItem, which reveals the UUID as the "IP" value.
        if (button.DataContext is Player player)
            ipText.Text = player.Uuid.ToString();

        var reveal = ipText.Visibility != Visibility.Visible;
        ipText.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;
        if (root.FindName("TogglePlayerIpIcon") is FontIcon icon)
            icon.Glyph = reveal ? "" : "";
    }

    private static T? FindAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
