namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Components;

public interface ICreateInstanceStep
{
    bool IsFinished { get; }
    object? Data { get; }
    event EventHandler? Changed;
}
