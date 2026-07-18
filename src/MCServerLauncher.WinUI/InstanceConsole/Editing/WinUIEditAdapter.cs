using System.Text;
using WinUIEditor;

namespace MCServerLauncher.WinUI.InstanceConsole.Editing;

/// <summary>
/// The only place that knows about WinUIEdit/Scintilla APIs. Pages and view
/// models use IEditorAdapter so a future editor update stays isolated.
/// </summary>
public sealed class WinUIEditAdapter : IEditorAdapter
{
    private readonly CodeEditorControl _control;
    private bool _isModified;

    public WinUIEditAdapter(CodeEditorControl control)
    {
        _control = control;
        _control.Editor.Modified += (_, _) => SetModified(true);
        _control.Editor.SavePointLeft += (_, _) => SetModified(true);
        _control.Editor.SavePointReached += (_, _) => SetModified(false);
    }

    public bool IsModified => _isModified;
    public bool CanUndo => _control.Editor.CanUndo();
    public bool CanRedo => _control.Editor.CanRedo();
    public event EventHandler? Modified;

    public void LoadText(string text)
    {
        _control.Editor.SetText(text ?? string.Empty);
        MarkSaved();
    }

    public void MarkSaved()
    {
        _control.Editor.SetSavePoint();
        SetModified(false);
    }

    public string ReadText() => _control.Editor.GetText(_control.Editor.TextLength + 1);

    public void SetText(string text) => _control.Editor.SetText(text ?? string.Empty);

    public void SetLanguage(string extensionOrLanguage)
    {
        var language = extensionOrLanguage.Trim().TrimStart('.').ToLowerInvariant();
        var highlightingLanguage = language switch
        {
            "cs" or "csharp" => "csharp",
            "json" => "json",
            "xml" => "xml",
            "yaml" or "yml" => "yaml",
            "java" => "java",
            "js" or "javascript" => "javascript",
            "bat" or "cmd" or "batch" => "batch",
            "sh" or "shell" => "shell",
            _ => "text"
        };
        _control.HighlightingLanguage = highlightingLanguage;
    }

    public void SetEncoding(Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        // Scintilla keeps its document buffer in UTF-8. The selected external
        // encoding is applied by the file-transfer layer when reading/writing.
        _control.Editor.CodePage = Encoding.UTF8.CodePage;
    }

    public void Undo() { if (CanUndo) _control.Editor.Undo(); }
    public void Redo() { if (CanRedo) _control.Editor.Redo(); }
    public void Copy() => _control.Editor.Copy();
    public void Paste() => _control.Editor.Paste();
    public void SelectAll() => _control.Editor.SelectAll();
    public void SetZoom(int zoom) => _control.Editor.Zoom = Math.Clamp(zoom, -10, 30);
    public void SetLineNumbers(bool enabled) => _control.Editor.SetMarginWidthN(0, enabled ? 48 : 0);

    public bool Find(string query, bool forward = true)
    {
        if (string.IsNullOrEmpty(query)) return false;

        var text = ReadText();
        if (text.Length == 0) return false;
        var current = (int)Math.Clamp(_control.Editor.CurrentPos, 0, text.Length);
        var index = forward
            ? text.IndexOf(query, current, StringComparison.CurrentCultureIgnoreCase)
            : text.LastIndexOf(query, Math.Max(0, current - 1), StringComparison.CurrentCultureIgnoreCase);
        if (index < 0)
        {
            index = forward
                ? text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase)
                : text.LastIndexOf(query, StringComparison.CurrentCultureIgnoreCase);
        }

        if (index < 0) return false;
        _control.Editor.SetSelection(index, index + query.Length);
        _control.Editor.GotoPos(index + query.Length);
        return true;
    }

    private void SetModified(bool value)
    {
        if (_isModified == value) return;
        _isModified = value;
        Modified?.Invoke(this, EventArgs.Empty);
    }
}
