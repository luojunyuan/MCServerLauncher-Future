namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

using MCServerLauncher.WinUI.View.Features.CreateInstance.Models;

public sealed class SetCreateOtherExecutableInstanceRunCommand : TextInputStep
{
    public SetCreateOtherExecutableInstanceRunCommand()
        : base(
            "CreateInstance_OtherExecutableInstanceRunCommand_Title",
            "CreateInstance_OtherExecutableInstanceRunCommand_Description",
            "CreateInstance_OtherExecutableInstanceRunCommand_Title",
            CreateInstanceDataType.CommandLine)
    {
    }
}
