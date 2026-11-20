using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PdfSyokunin.Localization;
using PdfSyokunin.Views;

namespace PdfSyokunin.Services;

public sealed class DialogService(Func<Window?> ownerProvider) : IDialogService
{
    public async Task<IReadOnlyList<string>> SelectPdfFilesAsync(bool allowMultiple)
    {
        var owner = RequireOwner();
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.Get("Dialog_SelectPdf"),
            AllowMultiple = allowMultiple,
            FileTypeFilter = [new FilePickerFileType(Loc.Get("Dialog_PdfFiles")) { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }]
        });
        return files.Select(file => file.TryGetLocalPath()).Where(path => path is not null).Cast<string>().ToArray();
    }

    public Task<string?> SelectSavePdfAsync(string inputPath, string suffix) =>
        SelectSaveFileAsync(
            Loc.Get("Dialog_SaveTo"),
            Path.GetFileNameWithoutExtension(inputPath) + suffix + ".pdf",
            Path.GetDirectoryName(inputPath),
            new FilePickerFileType(Loc.Get("Dialog_PdfFiles")) { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] },
            "pdf");

    public async Task<string?> SelectFolderAsync(string? initialDirectory = null)
    {
        var owner = RequireOwner();
        IStorageFolder? suggested = null;
        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            suggested = await owner.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.Get("Dialog_SelectFolder"),
            AllowMultiple = false,
            SuggestedStartLocation = suggested
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public Task<string?> SelectSaveTextAsync(string inputPath) =>
        SelectSaveFileAsync(
            Loc.Get("Dialog_SaveText"),
            Path.GetFileNameWithoutExtension(inputPath) + "_text.txt",
            Path.GetDirectoryName(inputPath),
            new FilePickerFileType(Loc.Get("Dialog_TextFiles")) { Patterns = ["*.txt"], MimeTypes = ["text/plain"] },
            "txt");

    public void ShowError(string message) => ShowMessage(message, Loc.Get("Dialog_Error"), MessageDialogKind.Error);

    public void ShowWarning(string message) => ShowMessage(message, Loc.Get("Dialog_Confirm"), MessageDialogKind.Warning);

    public async Task<bool> ConfirmAsync(string message, string? title = null) =>
        await new MessageDialogWindow(message, title ?? Loc.Get("Dialog_Confirm"), MessageDialogKind.Confirm).ShowDialog<bool>(RequireOwner());

    public async Task<string?> RequestPdfPasswordAsync(string fileName, bool isRetry) =>
        await new PasswordPromptWindow(fileName, isRetry).ShowDialog<string?>(RequireOwner());

    private async Task<string?> SelectSaveFileAsync(
        string title,
        string suggestedName,
        string? initialDirectory,
        FilePickerFileType type,
        string extension)
    {
        var owner = RequireOwner();
        IStorageFolder? suggested = null;
        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            suggested = await owner.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            SuggestedStartLocation = suggested,
            FileTypeChoices = [type],
            DefaultExtension = extension
        });
        return file?.TryGetLocalPath();
    }

    private Window RequireOwner() =>
        ownerProvider() ?? throw new InvalidOperationException("ダイアログの親ウィンドウを取得できません。");

    private async void ShowMessage(string message, string title, MessageDialogKind kind)
    {
        var owner = ownerProvider();
        if (owner is not null)
        {
            await new MessageDialogWindow(message, title, kind).ShowDialog<bool>(owner);
        }
    }
}
