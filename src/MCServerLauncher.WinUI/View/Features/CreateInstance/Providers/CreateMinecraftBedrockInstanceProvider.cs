using MCServerLauncher.Common.ProtoType.Instance;
using MCServerLauncher.WinUI.Core.Services;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Components;
using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;
using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Providers;

public sealed partial class CreateMinecraftBedrockInstanceProvider : CreateInstanceProviderPage
{
    private readonly SelectMinecraftBedrockArchive _archive = new();
    private readonly SetInstanceName _name = new();

    public CreateMinecraftBedrockInstanceProvider(CreateInstancePage owner, CreateInstanceSession session) : base(owner, session)
    {
        SetSteps(_archive, _name);
    }

    protected override async Task FinishAsync()
    {
        var archive = _archive.Path.Trim();
        var name = _name.Value.Trim();
        if (!ValidateName(name) || string.IsNullOrWhiteSpace(archive) || !File.Exists(archive))
        {
            Owner.Push(Texts["Error"], $"{Texts["Archive"]}: {Texts["CreateInstanceMissingDataError"]}", NotificationSeverity.Error);
            return;
        }
        var setting = new InstanceFactorySetting
        {
            Name = name, Source = archive, SourceType = SourceType.Core,
            Target = Path.GetFileName(archive), TargetType = TargetType.Executable,
            InstanceType = InstanceType.Universal, Version = string.Empty
        };
        if (!await ConfirmAsync(BuildConfirmation(Texts["CreateMinecraftBedrockInstance"], name, ("Archive", archive)))) return;
        var source = await UploadLocalFileAsync(archive);
        if (source is null) return;
        await SubmitConfirmedAsync(setting with { Source = source });
    }
}
