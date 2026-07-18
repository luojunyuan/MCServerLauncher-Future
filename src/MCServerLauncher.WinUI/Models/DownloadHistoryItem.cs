using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Input;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.Models;

public sealed class DownloadHistoryItem
{
    public string FileName { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string LocalPath { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public string Status { get; init; } = string.Empty;

    [JsonIgnore]
    public LocalizedStrings Texts => App.Services.Localization.Texts;

    [JsonIgnore]
    public IAsyncRelayCommand? RetryCommand { get; set; }
}
