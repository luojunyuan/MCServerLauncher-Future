namespace MCServerLauncher.WinUI.ViewModels.Models;

[WinRT.GeneratedBindableCustomProperty]
public sealed partial record RefreshIntervalOption(int Seconds, string Display);

public static class RefreshIntervalOptions
{
    public static readonly int[] AllowedSeconds = [5, 20, 30, 45, 60];

    public static int Normalize(int seconds)
    {
        if (AllowedSeconds.Contains(seconds)) return seconds;
        return seconds > AllowedSeconds[^1]
            ? AllowedSeconds[^1]
            : AllowedSeconds.First(value => seconds <= value);
    }
}
