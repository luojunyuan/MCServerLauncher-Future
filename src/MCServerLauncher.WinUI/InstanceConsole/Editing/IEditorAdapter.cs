using System.Text;

namespace MCServerLauncher.WinUI.InstanceConsole.Editing;

public interface IEditorAdapter
{
    bool IsModified { get; }
    bool CanUndo { get; }
    bool CanRedo { get; }
    event EventHandler? Modified;

    void LoadText(string text);
    void MarkSaved();
    string ReadText();
    void SetText(string text);
    void SetLanguage(string extensionOrLanguage);
    void SetEncoding(Encoding encoding);
    void Undo();
    void Redo();
    void Copy();
    void Paste();
    void SelectAll();
    void SetZoom(int zoom);
    void SetLineNumbers(bool enabled);
    bool Find(string query, bool forward = true);
}
