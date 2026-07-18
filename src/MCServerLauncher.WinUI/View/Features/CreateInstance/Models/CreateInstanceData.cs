namespace MCServerLauncher.WinUI.View.Features.CreateInstance.Models;

public enum CreateInstanceDataType
{
    Filename,
    CommandLine,
    Number,
    String,
    Path,
    List,
    Array,
    Struct
}

public sealed record CreateInstanceData(CreateInstanceDataType Type, object? Data);

public readonly record struct MinecraftLoaderVersion(string MCVersion, string LoaderVersion);
