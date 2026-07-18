using System.Globalization;

namespace MCServerLauncher.WinUI.Core.Localization;

public interface ILocalizationService
{
    LocalizedStrings Texts { get; }
    CultureInfo CurrentCulture { get; }
    IReadOnlyList<string> LanguageCodes { get; }
    IReadOnlyList<string> LanguageNames { get; }
    event EventHandler? LanguageChanged;
    string Get(string key);
    void ChangeLanguage(string cultureName);
}
