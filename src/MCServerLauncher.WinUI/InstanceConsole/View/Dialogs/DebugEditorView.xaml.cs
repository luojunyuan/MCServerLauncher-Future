using System.IO;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.InstanceConsole.Editing;
using WinUIEditor;

namespace MCServerLauncher.WinUI.InstanceConsole.View.Dialogs;

public sealed partial class DebugEditorView : UserControl
{
    private readonly string _path;
    private readonly IEditorAdapter _editor;
    private int _zoom;

    public DebugEditorView(string path, string fileName)
    {
        _path = path;
        FileName = fileName;
        InitializeComponent();
        _editor = new WinUIEditAdapter(Editor);
        _editor.Modified += (_, _) => UpdateState();
        _editor.SetLineNumbers(true);
        _editor.SetLanguage(Path.GetExtension(fileName));
        _editor.LoadText(File.ReadAllText(path));
        UpdateState();
    }

    public string FileName { get; }
    public string SaveText => "Save";
    public string UndoText => "Undo";
    public string RedoText => "Redo";
    public string CopyText => "Copy";
    public string PasteText => "Paste";
    public string SelectAllText => "Select All";
    public string ZoomOutText => "Zoom Out";
    public string ZoomInText => "Zoom In";
    public FrameworkElement TitleBarElement => EditorTitleBar;

    private void UpdateState() => StateText.Text = _editor.IsModified ? "Modified" : "Saved";

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            File.WriteAllText(_path, _editor.ReadText());
            _editor.LoadText(File.ReadAllText(_path));
            UpdateState();
        }
        catch (Exception ex)
        {
            StateText.Text = ex.Message;
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => _editor.Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => _editor.Redo();
    private void Copy_Click(object sender, RoutedEventArgs e) => _editor.Copy();
    private void Paste_Click(object sender, RoutedEventArgs e) => _editor.Paste();
    private void SelectAll_Click(object sender, RoutedEventArgs e) => _editor.SelectAll();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => _editor.SetZoom(--_zoom);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => _editor.SetZoom(++_zoom);
}
