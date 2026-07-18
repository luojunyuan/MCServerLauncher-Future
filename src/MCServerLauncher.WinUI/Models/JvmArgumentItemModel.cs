using CommunityToolkit.Mvvm.ComponentModel;
using MCServerLauncher.WinUI.Core.Localization;

namespace MCServerLauncher.WinUI.Models;

public partial class JvmArgumentItemModel : ObservableObject
{
    public JvmArgumentItemModel(string argument) => Argument = argument;

    [ObservableProperty]
    public partial string Argument { get; set; }

    public LocalizedStrings Texts => App.Services.Localization.Texts;
}
