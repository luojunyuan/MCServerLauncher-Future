using Windows.ApplicationModel.DataTransfer;

namespace MCServerLauncher.WinUI.Core.Services;

public sealed class ClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
