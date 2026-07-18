using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Models;

namespace MCServerLauncher.WinUI.Views.Components.DaemonManager;

public sealed partial class NewDaemonConnectionInput : UserControl
{
    public NewDaemonConnectionInput()
    {
        InitializeComponent();
        PortTextBox.Text = "25565";
    }

    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public string UrlLabel => "URL";
    public string Colon => ":";

    public void Load(DaemonConfigModel config)
    {
        EndpointTextBox.Text = config.EndPoint ?? string.Empty;
        PortTextBox.Text = config.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        WebSocketScheme.SelectedIndex = config.IsSecure ? 1 : 0;
        TokenBox.Password = config.Token ?? string.Empty;
        FriendlyNameTextBox.Text = config.FriendlyName ?? string.Empty;
    }

    public bool TryCreateConfig(out DaemonConfigModel config)
    {
        config = new DaemonConfigModel();
        var endpoint = EndpointTextBox.Text.Trim();
        var token = TokenBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(endpoint)
            || string.IsNullOrWhiteSpace(token)
            || !int.TryParse(PortTextBox.Text, out var port)
            || port is < 1 or > 65535)
        {
            ValidationText.Text = Texts["ConnectDaemonFailedTip"];
            return false;
        }

        ValidationText.Text = string.Empty;
        config = new DaemonConfigModel
        {
            EndPoint = endpoint,
            Port = port,
            Token = token,
            FriendlyName = FriendlyNameTextBox.Text.Trim(),
            IsSecure = WebSocketScheme.SelectedIndex == 1
        };
        return true;
    }

    public void ShowConnectionError(string message) =>
        ValidationText.Text = string.IsNullOrWhiteSpace(message)
            ? Texts["ConnectDaemonFailedTip"]
            : message;
}
