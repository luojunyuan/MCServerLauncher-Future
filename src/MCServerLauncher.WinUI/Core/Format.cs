namespace MCServerLauncher.WinUI.Core;

/// <summary>
/// Shared byte-size formatting used by the download, file-manager, instance-card and
/// daemon-card UI. Previously duplicated in six places with subtly different formats.
/// </summary>
public static class Format
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string FormatSize(long bytes, string format = "F1") => FormatSize((double)bytes, format);

    public static string FormatSize(double bytes, string format = "F1")
    {
        var value = Math.Max(0, bytes);
        var index = 0;
        while (value >= 1024 && index < Units.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return $"{value.ToString(format)} {Units[index]}";
    }
}
