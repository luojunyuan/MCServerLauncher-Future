using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MCServerLauncher.WinUI.Core.Services;

public sealed class FilePickerService : IFilePickerService
{
    public async Task<StorageFile?> PickFileAsync(nint windowHandle, string? suggestedStartLocation = null)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, windowHandle);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");
        return await picker.PickSingleFileAsync();
    }

    public async Task<IReadOnlyList<StorageFile>> PickFilesAsync(nint windowHandle)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, windowHandle);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");
        return await picker.PickMultipleFilesAsync();
    }

    public async Task<StorageFile?> PickSaveFileAsync(nint windowHandle, string suggestedFileName)
    {
        var extension = Path.GetExtension(suggestedFileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".txt";
        var picker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileName(suggestedFileName),
            SuggestedStartLocation = PickerLocationId.Downloads,
            DefaultFileExtension = extension
        };
        InitializeWithWindow.Initialize(picker, windowHandle);
        picker.FileTypeChoices.Add($"{extension} file", new[] { extension });
        return await picker.PickSaveFileAsync();
    }
}
