using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PdfSyokunin.Views;

namespace PdfSyokunin.Services;

public sealed class DialogService(Func<Window?> ownerProvider) : IDialogService
{
    public async Task<IReadOnlyList<string>> SelectPdfFilesAsync(bool allowMultiple)
    {
        var owner = RequireOwner();
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "PDFファイルを選択",
            AllowMultiple = allowMultiple,
            FileTypeFilter = [new FilePickerFileType("PDFファイル") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }]
        });
        return files.Select(file => file.TryGetLocalPath()).Where(path => path is not null).Cast<string>().ToArray();
    }

    public Task<string?> SelectSavePdfAsync(string inputPath, string suffix) =>
        SelectSaveFileAsync(
            "保存先を選択",
            Path.GetFileNameWithoutExtension(inputPath) + suffix + ".pdf",
            Path.GetDirectoryName(inputPath),
            new FilePickerFileType("PDFファイル") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] },
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
            Title = "保存先フォルダーを選択",
            AllowMultiple = false,
            SuggestedStartLocation = suggested
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public Task<string?> SelectSaveTextAsync(string inputPath) =>
        SelectSaveFileAsync(
            "抽出テキストを保存",
            Path.GetFileNameWithoutExtension(inputPath) + "_text.txt",
            Path.GetDirectoryName(inputPath),
            new FilePickerFileType("テキストファイル") { Patterns = ["*.txt"], MimeTypes = ["text/plain"] },
            "txt");

    public void ShowError(string message) => ShowMessage(message, "エラー", MessageDialogKind.Error);

    public void ShowWarning(string message) => ShowMessage(message, "確認", MessageDialogKind.Warning);

    public async Task<bool> ConfirmAsync(string message, string title = "確認") =>
        await new MessageDialogWindow(message, title, MessageDialogKind.Confirm).ShowDialog<bool>(RequireOwner());

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
