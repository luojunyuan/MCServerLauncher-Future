using System.Collections;
using System.Globalization;
using System.Resources;

namespace MCServerLauncher.WinUI.Core.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly string[] KnownCultures =
    [
        "en-US",
        "ja-JP",
        "ru-RU",
        "zh-CN",
        "zh-HK",
        "zh-TW"
    ];

    private static readonly string[] KnownNames =
    [
        "English (US)",
        "日本語",
        "Русский",
        "简体中文 (中国)",
        "繁體中文 (中国香港)",
        "正體中文 (中国台湾)"
    ];

    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private readonly ResourceManager _resourceManager = new(
        "MCServerLauncher.WinUI.Translations.Lang",
        typeof(LocalizationService).Assembly);
    private readonly string[] _keys;

    public LocalizationService()
    {
        LanguageCodes = KnownCultures;
        LanguageNames = KnownNames;
        _keys = LoadKeys();
        CurrentCulture = EnglishCulture;
        Reload(CurrentCulture);
    }

    public LocalizedStrings Texts { get; } = new();
    public CultureInfo CurrentCulture { get; private set; }
    public IReadOnlyList<string> LanguageCodes { get; }
    public IReadOnlyList<string> LanguageNames { get; }
    public event EventHandler? LanguageChanged;

    public string Get(string key)
    {
        return Texts[key];
    }

    public void ChangeLanguage(string cultureName)
    {
        var culture = TryGetCulture(cultureName) ?? EnglishCulture;
        CurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Reload(culture);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Reload(CultureInfo culture)
    {
        Texts.ReplaceWith(_keys.Select(key =>
            new KeyValuePair<string, string>(key, ReadString(key, culture))));
    }

    private string ReadString(string key, CultureInfo culture)
    {
        try
        {
            var value = _resourceManager.GetString(key, culture);
            if (!string.IsNullOrEmpty(value)) return Normalize(value);

            value = _resourceManager.GetString(key, EnglishCulture);
            return string.IsNullOrEmpty(value) ? key : Normalize(value);
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
    }

    private string[] LoadKeys()
    {
        try
        {
            var resourceSet = _resourceManager.GetResourceSet(EnglishCulture, true, true);
            if (resourceSet is not null)
            {
                return resourceSet.Cast<DictionaryEntry>()
                    .Select(entry => entry.Key?.ToString())
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToArray();
            }
        }
        catch (MissingManifestResourceException)
        {
            // A missing satellite resource is handled by the key fallback.
        }

        return [];
    }

    private static CultureInfo? TryGetCulture(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return KnownCultures.Contains(name, StringComparer.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo(name)
            : null;
    }

    private static string Normalize(string value) => value.Replace("\\n", "\n", StringComparison.Ordinal);
}
