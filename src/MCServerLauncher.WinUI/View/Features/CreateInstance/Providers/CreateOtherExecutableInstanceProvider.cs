using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Components;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;

public sealed partial class CreateOtherExecutableInstanceProvider : CreateInstanceProviderPage
{
    private readonly SelectOtherExecutableInstanceDependency _dependency = new();
    private readonly SetCreateOtherExecutableInstanceRunCommand _command = new();
    private readonly SetInstanceName _name = new();

    public CreateOtherExecutableInstanceProvider(CreateInstancePage owner, CreateInstanceSession session) : base(owner, session)
    {
        SetSteps(_dependency, _command, _name);
    }

    protected override async Task FinishAsync()
    {
        var dependency = _dependency.Path.Trim();
        var name = _name.Value.Trim();
        if (!ValidateName(name) || string.IsNullOrWhiteSpace(dependency) || !File.Exists(dependency))
        {
            Owner.Push(Texts["Error"], $"{Texts["FileName"]}: {Texts["CreateInstanceMissingDataError"]}", NotificationSeverity.Error);
            return;
        }
        var setting = new InstanceFactorySetting
        {
            Name = name, Source = dependency, SourceType = SourceType.Core,
            Target = Path.GetFileName(dependency), TargetType = TargetType.Executable,
            InstanceType = InstanceType.Universal, Arguments = SplitCommandLine(_command.Value)
        };
        var confirmation = BuildConfirmation(Texts["CreateOtherExecutableInstance"], name,
            ("FileName", dependency), ("CreateInstance_OtherExecutableInstanceRunCommand_Title", _command.Value));
        if (!await ConfirmAsync(confirmation)) return;
        var source = await UploadLocalFileAsync(dependency);
        if (source is null) return;
        await SubmitConfirmedAsync(setting with { Source = source });
    }
}
