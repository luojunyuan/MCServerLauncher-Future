using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Components;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;

public sealed partial class CreateMinecraftJavaInstanceProvider : CreateInstanceProviderPage
{
    private readonly SelectMinecraftJavaCore _core = new();
    private readonly SelectMinecraftJavaJvm _jvm;
    private readonly SetMinecraftJavaJvmArgument _arguments = new();
    private readonly SetInstanceName _name = new();

    public CreateMinecraftJavaInstanceProvider(CreateInstancePage owner, CreateInstanceSession session) : base(owner, session)
    {
        _jvm = new SelectMinecraftJavaJvm(session);
        SetSteps(_core, _jvm, _arguments, _name);
    }

    protected override async Task FinishAsync()
    {
        var core = _core.Path.Trim();
        var java = _jvm.Path.Trim();
        var name = _name.Value.Trim();
        if (!ValidateJar(core) || !ValidateJava(java) || !ValidateName(name)) return;
        var args = _arguments.Arguments;
        var setting = new InstanceFactorySetting
        {
            Name = name,
            Source = core,
            SourceType = SourceType.Core,
            Target = Path.GetFileName(core),
            TargetType = TargetType.Jar,
            InstanceType = InstanceType.MCJava,
            JavaPath = java,
            Arguments = args,
            Version = "1.21.1",
            Mirror = InstanceFactoryMirror.None,
            UsePostProcess = false
        };
        var confirmation = BuildConfirmation(InstanceType.MCJava.ToString(), name,
            ("CorePath", core), ("JavaPath", java), ("JvmArguments", args.Length == 0 ? Texts["None"] : string.Join(" ", args)));
        if (!await ConfirmAsync(confirmation, setting)) return;
        var source = await UploadLocalFileAsync(core);
        if (source is null) return;
        await SubmitConfirmedAsync(setting with { Source = source });
    }
}
