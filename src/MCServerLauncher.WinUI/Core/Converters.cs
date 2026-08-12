using MCServerLauncher.Common.ProtoType.Instance;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace MCServerLauncher.WinUI.Core;

/// <summary>
/// Value converters for use in <c>{x:Bind}</c> function bindings.
/// </summary>
public static class Converters
{
    public static Brush InstanceStatusToBrush(object status)
    {
        if (status is InstanceStatus instanceStatus)
        {
            return instanceStatus switch
            {
                InstanceStatus.Running => GetBrush("StatusRunningBrush", "#FF107C10"),
                InstanceStatus.Crashed => GetBrush("StatusCrashedBrush", "#FFE81123"),
                _ => GetBrush("StatusStoppedBrush", "#FF999999")
            };
        }

        if (status is string statusText)
        {
            return statusText switch
            {
                "Running" => GetBrush("StatusRunningBrush", "#FF107C10"),
                "Crashed" => GetBrush("StatusCrashedBrush", "#FFE81123"),
                _ => GetBrush("StatusStoppedBrush", "#FF999999")
            };
        }

        return GetBrush("StatusStoppedBrush", "#FF999999");
    }

    public static Brush BoolToDotBrush(bool enabled)
    {
        return enabled
            ? GetBrush("StatusEnabledDotBrush", "#FF4CAF50")
            : GetBrush("StatusDisabledDotBrush", "#FF9E9E9E");
    }

    public static Visibility ClientSideBadgeVisibility(bool isClientSideOnly)
    {
        return isClientSideOnly ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility InverseVisibility(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }

    private static Brush GetBrush(string resourceKey, string fallbackHex)
    {
        var resources = Application.Current?.Resources;
        if (resources is not null && resources.TryGetValue(resourceKey, out var value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(HexToColor(fallbackHex));
    }

    private static Color HexToColor(string hex)
    {
        var value = hex.TrimStart('#');
        var a = (byte)255;
        byte r;
        byte g;
        byte b;

        if (value.Length == 8)
        {
            a = Convert.ToByte(value.Substring(0, 2), 16);
            r = Convert.ToByte(value.Substring(2, 2), 16);
            g = Convert.ToByte(value.Substring(4, 2), 16);
            b = Convert.ToByte(value.Substring(6, 2), 16);
        }
        else
        {
            r = Convert.ToByte(value.Substring(0, 2), 16);
            g = Convert.ToByte(value.Substring(2, 2), 16);
            b = Convert.ToByte(value.Substring(4, 2), 16);
        }

        return Color.FromArgb(a, r, g, b);
    }
}
