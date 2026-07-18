using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Components;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;

public sealed class CreateMinecraftNeoForgeInstanceProvider : CreateInstanceProviderPage
{
    private readonly NeoForgeLoaderSet _loader = new();
    private readonly SelectMinecraftJavaJvm _jvm;
    private readonly SetMinecraftJavaJvmArgument _arguments = new();
    private readonly SetInstanceName _name = new();

    public CreateMinecraftNeoForgeInstanceProvider(CreateInstancePage owner, CreateInstanceSession session) : base(owner, session)
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
        var mirror = App.Services.Settings.Current.InstanceCreation.UseMirrorForMinecraftNeoForgeInstall;
        var legacy = version.MCVersion == "1.20.1";
        var fileName = legacy
            ? $"forge-1.20.1-{version.LoaderVersion}-installer.jar"
            : $"neoforge-{version.LoaderVersion}-installer.jar";
        var url = legacy
            ? (mirror
                ? $"https://bmclapi2.bangbang93.com/maven/net/neoforged/forge/1.20.1-{version.LoaderVersion}/{fileName}"
                : $"https://maven.neoforged.net/releases/net/neoforged/forge/1.20.1-{version.LoaderVersion}/{fileName}")
            : (mirror
                ? $"https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{version.LoaderVersion}/{fileName}"
                : $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{version.LoaderVersion}/{fileName}");
        var setting = new InstanceFactorySetting
        {
            Name = name, Source = url, SourceType = SourceType.Core, Target = fileName,
            TargetType = TargetType.Jar, InstanceType = InstanceType.MCNeoForge, JavaPath = java,
            Arguments = _arguments.Arguments, Version = version.MCVersion,
            Mirror = mirror ? InstanceFactoryMirror.BmclApi : InstanceFactoryMirror.None,
            UsePostProcess = false
        };
        await SubmitAsync(setting, BuildConfirmation(Texts["InstanceType_MCNeoForge"], name,
            ("MinecraftVersionLabel", version.MCVersion), ("NeoForgeVersionLabel", version.LoaderVersion),
            ("JavaPath", java), ("JvmArguments", _arguments.Arguments.Length == 0 ? Texts["None"] : string.Join(" ", _arguments.Arguments))));
    }
}
