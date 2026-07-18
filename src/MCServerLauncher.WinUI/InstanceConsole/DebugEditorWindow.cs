using MCServerLauncher.WinUI.InstanceConsole.View.Dialogs;

namespace MCServerLauncher.WinUI.InstanceConsole;

public sealed class DebugEditorWindow : CoreIsland.Window
{
    public DebugEditorWindow(string path, string fileName)
    {
        var view = new DebugEditorView(path, fileName);
        Content = view;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(view.TitleBarElement);
        SystemBackdrop = new CoreIsland.MicaBackdrop();
        Title = fileName;
        Closed += (_, _) => App.UnregisterSecondaryWindow(this);
    }
}
