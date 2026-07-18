namespace MCServerLauncher.WinUI.ViewModels.Models;

public sealed class SettingsLinkItem
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string ActionText { get; init; }
    public required string Uri { get; init; }
}
