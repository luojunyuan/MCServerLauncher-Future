using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Components;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;

public sealed class CreateMinecraftForgeInstanceProvider : CreateInstanceProviderPage
{
    private readonly ForgeLoaderSet _loader = new();
    private readonly SelectMinecraftJavaJvm _jvm;
    private readonly SetMinecraftJavaJvmArgument _arguments = new();
    private readonly SetInstanceName _name = new();

    public CreateMinecraftForgeInstanceProvider(CreateInstancePage owner, CreateInstanceSession session) : base(owner, session)
    {
        _jvm = new SelectMinecraftJavaJvm(session);
        SetSteps(_loader, _jvm, _arguments, _name);
    }

    protected override async Task FinishAsync()
    {
        var version = (MinecraftLoaderVersion)((CreateInstanceData)_loader.Data!).Data!;
        var java = _jvm.Path.Trim();
        var name = _name.Value.Trim();
        if (!ValidateJava(java) || !ValidateName(name)) return;
        var mirror = App.Services.Settings.Current.InstanceCreation.UseMirrorForMinecraftForgeInstall;
        var fileName = $"forge-{version.MCVersion}-{version.LoaderVersion}-installer.jar";
        var url = mirror
            ? $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{version.MCVersion}-{version.LoaderVersion}/{fileName}"
            : $"https://maven.minecraftforge.net/net/minecraftforge/forge/{version.MCVersion}-{version.LoaderVersion}/{fileName}";
        var setting = new InstanceFactorySetting
        {
            Name = name, Source = url, SourceType = SourceType.Core, Target = fileName,
            TargetType = TargetType.Jar, InstanceType = InstanceType.MCForge, JavaPath = java,
            Arguments = _arguments.Arguments, Version = version.MCVersion,
            Mirror = mirror ? InstanceFactoryMirror.BmclApi : InstanceFactoryMirror.None,
            UsePostProcess = false
        };
        await SubmitAsync(setting, BuildConfirmation(Texts["InstanceType_MCForge"], name,
            ("MinecraftVersionLabel", version.MCVersion), ("ForgeVersionLabel", version.LoaderVersion),
            ("JavaPath", java), ("JvmArguments", _arguments.Arguments.Length == 0 ? Texts["None"] : string.Join(" ", _arguments.Arguments))));
    }
}
