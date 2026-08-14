using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Components;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;

public sealed partial class CreateMinecraftFabricInstanceProvider : CreateInstanceProviderPage
{
    private readonly FabricLoaderSet _loader = new();
    private readonly SelectMinecraftJavaJvm _jvm;
    private readonly SetMinecraftJavaJvmArgument _arguments = new();
    private readonly SetInstanceName _name = new();

    public CreateMinecraftFabricInstanceProvider(CreateInstancePage owner, CreateInstanceSession session) : base(owner, session)
    {
        _jvm = new SelectMinecraftJavaJvm(session);
        SetSteps(_loader, _jvm, _arguments, _name);
    }

    protected override async Task FinishAsync()
    {
        var data = (CreateInstanceData)_loader.Data!;
        var version = (MinecraftLoaderVersion)data.Data!;
        var java = _jvm.Path.Trim();
        var name = _name.Value.Trim();
        if (!ValidateJava(java) || !ValidateName(name)) return;
        var mirror = App.Services.Settings.Current.InstanceCreation.UseMirrorForMinecraftFabricInstall;
        var url = mirror
            ? $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{version.MCVersion}/{version.LoaderVersion}/1.0.1/server/jar"
            : $"https://meta.fabricmc.net/v2/versions/loader/{version.MCVersion}/{version.LoaderVersion}/1.0.1/server/jar";
        var fileName = $"fabric-server-mc.{version.MCVersion}-loader.{version.LoaderVersion}-launcher.{version.MCVersion}.jar";
        var setting = new InstanceFactorySetting
        {
            Name = name, Source = url, SourceType = SourceType.Core, Target = fileName,
            TargetType = TargetType.Jar, InstanceType = InstanceType.MCFabric, JavaPath = java,
            Arguments = _arguments.Arguments, Version = version.MCVersion,
            Mirror = mirror ? InstanceFactoryMirror.BmclApi : InstanceFactoryMirror.None,
            UsePostProcess = false
        };
        await SubmitAsync(setting, BuildConfirmation(Texts["InstanceType_MCFabric"], name,
            ("MinecraftVersionLabel", version.MCVersion), ("FabricLoaderVersionLabel", version.LoaderVersion),
            ("JavaPath", java), ("JvmArguments", _arguments.Arguments.Length == 0 ? Texts["None"] : string.Join(" ", _arguments.Arguments))));
    }
}
