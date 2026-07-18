using System.ComponentModel;

namespace MCServerLauncher.WinUI.Core.Localization;

/// <summary>
/// Observable dictionary used by x:Bind indexer expressions. Reloading the
/// dictionary raises Item[] so all localized controls refresh in place.
/// </summary>
public sealed class LocalizedStrings : Dictionary<string, string>, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public new string this[string key]
    {
        get => TryGetValue(key, out var value) ? value : key;
        set => base[key] = value;
    }

    public void ReplaceWith(IEnumerable<KeyValuePair<string, string>> values)
    {
        base.Clear();
        foreach (var pair in values)
        {
            base[pair.Key] = pair.Value;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
