using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Components;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;

public sealed partial class CreateMinecraftQuiltInstanceProvider : CreateInstanceProviderPage
{
    private readonly QuiltLoaderSet _loader = new();
    private readonly SelectMinecraftJavaJvm _jvm;
    private readonly SetMinecraftJavaJvmArgument _arguments = new();
    private readonly SetInstanceName _name = new();

    public CreateMinecraftQuiltInstanceProvider(CreateInstancePage owner, CreateInstanceSession session) : base(owner, session)
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
        var mirror = App.Services.Settings.Current.InstanceCreation.UseMirrorForMinecraftQuiltInstall;
        var endpoint = mirror ? "https://bmclapi2.bangbang93.com/quilt-meta" : "https://meta.quiltmc.org";
        var url = $"{endpoint}/v3/versions/loader/{version.MCVersion}/{version.LoaderVersion}/server/jar";
        const string fileName = "quilt-server-launcher.jar";
        var setting = new InstanceFactorySetting
        {
            Name = name, Source = url, SourceType = SourceType.Core, Target = fileName,
            TargetType = TargetType.Jar, InstanceType = InstanceType.MCQuilt, JavaPath = java,
            Arguments = _arguments.Arguments, Version = version.MCVersion,
            Mirror = mirror ? InstanceFactoryMirror.BmclApi : InstanceFactoryMirror.None,
            UsePostProcess = false
        };
        await SubmitAsync(setting, BuildConfirmation(Texts["CreateMinecraftQuiltInstance"], name,
            ("MinecraftVersionLabel", version.MCVersion), ("QuiltVersion", version.LoaderVersion),
            ("JavaPath", java), ("JvmArguments", _arguments.Arguments.Length == 0 ? Texts["None"] : string.Join(" ", _arguments.Arguments))));
    }
}
