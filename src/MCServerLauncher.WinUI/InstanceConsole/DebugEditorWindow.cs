using MCServerLauncher.WinUI.InstanceConsole.View.Dialogs;

namespace MCServerLauncher.WinUI.InstanceConsole;

public sealed class DebugEditorWindow : WinUIIslands.Window
{
    public DebugEditorWindow(string path, string fileName)
    {
        var view = new DebugEditorView(path, fileName);
        Content = view;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(view.TitleBarElement);
        SystemBackdrop = new WinUIIslands.MicaBackdrop();
        Title = fileName;
        if (AppWindow is { } appWindow)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(800, 600));
        }
        Closed += (_, _) => App.UnregisterSecondaryWindow(this);
    }
}
