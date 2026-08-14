using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public partial class TextInputStep : CreateStepControl
{
    protected readonly TextBox Input;
    private readonly CreateInstanceDataType _dataType;

    public TextInputStep(string titleKey, string descriptionKey, string placeholderKey,
        CreateInstanceDataType dataType = CreateInstanceDataType.String)
        : base(titleKey, descriptionKey)
    {
        _dataType = dataType;
        Input = new TextBox { PlaceholderText = Texts[placeholderKey], TextWrapping = TextWrapping.Wrap };
        Input.Tag = placeholderKey;
        Input.TextChanged += (_, _) => IsFinished = !string.IsNullOrWhiteSpace(Input.Text);
        Fields.Children.Add(Input);
        RegisterLanguageChangedHandler((_, _) => Input.PlaceholderText = Texts[placeholderKey]);
    }

    public string Value
    {
        get => Input.Text;
        set
        {
            Input.Text = value;
            IsFinished = !string.IsNullOrWhiteSpace(value);
        }
    }

    public override object Data => new CreateInstanceData(_dataType, Input.Text);
}
