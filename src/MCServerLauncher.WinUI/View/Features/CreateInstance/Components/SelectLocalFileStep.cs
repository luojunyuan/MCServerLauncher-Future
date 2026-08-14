using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public partial class SelectLocalFileStep : CreateStepControl
{
    private readonly TextBox _pathBox;
    private readonly string _placeholderKey;

    public SelectLocalFileStep(string titleKey, string descriptionKey, string placeholderKey)
        : base(titleKey, descriptionKey)
    {
        _placeholderKey = placeholderKey;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _pathBox = new TextBox
        {
            IsReadOnly = true,
            HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Stretch,
            PlaceholderText = Texts[placeholderKey]
        };
        _pathBox.Width = 560;
        _pathBox.TextChanged += (_, _) => IsFinished = !string.IsNullOrWhiteSpace(_pathBox.Text);
        var browse = new Button { Content = Texts["Browse"] };
        browse.Click += BrowseAsync;
        row.Children.Add(_pathBox);
        row.Children.Add(browse);
        Fields.Children.Add(row);
        RegisterLanguageChangedHandler((_, _) =>
        {
            _pathBox.PlaceholderText = Texts[_placeholderKey];
            browse.Content = Texts["Browse"];
        });
    }

    public string Path => _pathBox.Text;

    public override object Data => new CreateInstanceData(CreateInstanceDataType.Path, Path);

    private void BrowseAsync(object sender, Windows.UI.Xaml.RoutedEventArgs e) =>
        BrowseAsyncCore().FireAndForget("SelectLocalFileStep.BrowseAsync");

    private async Task BrowseAsyncCore()
    {
        var file = await App.Services.Files.PickFileAsync(App.WindowHandle);
        if (file is null) return;
        _pathBox.Text = file.Path;
        IsFinished = true;
    }
}

public sealed partial class SelectMinecraftJavaCore : SelectLocalFileStep
{
    public SelectMinecraftJavaCore() : base("SelectMinecraftJavaCore", "CreateInstance_Archive_Description", "CorePath") { }
}

public sealed partial class SelectMinecraftBedrockArchive : SelectLocalFileStep
{
    public SelectMinecraftBedrockArchive() : base("CreateInstance_MinecraftBedrockArchive_Title", "CreateInstance_Archive_Description", "Archive") { }
}

public sealed partial class SelectTerrariaArchive : SelectLocalFileStep
{
    public SelectTerrariaArchive() : base("CreateInstance_TerrariaArchive_Title", "CreateInstance_Archive_Description", "Archive") { }
}

public sealed partial class SelectOtherExecutableInstanceDependency : SelectLocalFileStep
{
    public SelectOtherExecutableInstanceDependency() : base("CreateInstance_OtherExecutableInstanceDependency_Title", "CreateInstance_OtherExecutableInstanceDependency_Description", "FileName") { }
}
