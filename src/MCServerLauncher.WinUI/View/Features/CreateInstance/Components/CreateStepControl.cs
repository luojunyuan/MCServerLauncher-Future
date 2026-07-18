using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public abstract class CreateStepControl : UserControl, ICreateInstanceStep
{
    private readonly string _titleKey;
    private readonly string _descriptionKey;
    private readonly TextBlock _title;
    private readonly TextBlock _description;
    private readonly TextBlock _status;
    protected readonly StackPanel Fields;
    protected readonly TextBlock ErrorText;
    private bool _isFinished;

    protected CreateStepControl(string titleKey, string descriptionKey)
    {
        _titleKey = titleKey;
        _descriptionKey = descriptionKey;

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Colors.Gray) { Opacity = 0.35 },
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var root = new StackPanel { Spacing = 8 };
        var heading = new Grid { ColumnSpacing = 8 };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _title = new TextBlock { FontSize = 18, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
        _status = new TextBlock
        {
            Text = "✓",
            FontSize = 18,
            Foreground = new SolidColorBrush(Colors.ForestGreen),
            Visibility = Visibility.Collapsed
        };
        heading.Children.Add(_title);
        Grid.SetColumn(_status, 1);
        heading.Children.Add(_status);
        _description = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.78 };
        Fields = new StackPanel { Spacing = 8 };
        ErrorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.IndianRed),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        root.Children.Add(heading);
        root.Children.Add(_description);
        root.Children.Add(Fields);
        root.Children.Add(ErrorText);
        border.Child = root;
        Content = border;
        RefreshLocalizedText();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public bool IsFinished
    {
        get => _isFinished;
        protected set
        {
            if (_isFinished == value) return;
            _isFinished = value;
            _status.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public abstract object? Data { get; }
    public event EventHandler? Changed;

    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    protected void ShowError(Exception exception) => ShowError(exception.Message);

    protected void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    protected TextBlock AddLabel(string key)
    {
        var label = new TextBlock { TextWrapping = TextWrapping.Wrap };
        label.Tag = key;
        label.Text = Texts[key];
        Fields.Children.Add(label);
        App.Services.Localization.LanguageChanged += (_, _) => label.Text = Texts[key];
        return label;
    }

    protected void RefreshLocalizedText()
    {
        _title.Text = Texts[_titleKey];
        _description.Text = Texts[_descriptionKey];
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RefreshLocalizedText();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Services.Localization.LanguageChanged -= OnLanguageChanged;
        App.Services.Localization.LanguageChanged += OnLanguageChanged;
        RefreshLocalizedText();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        App.Services.Localization.LanguageChanged -= OnLanguageChanged;
}
