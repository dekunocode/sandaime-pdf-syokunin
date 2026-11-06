namespace PdfSyokunin.Services;

public interface IDialogService
{
    Task<IReadOnlyList<string>> SelectPdfFilesAsync(bool allowMultiple);

    Task<string?> SelectSavePdfAsync(string inputPath, string suffix);

    Task<string?> SelectFolderAsync(string? initialDirectory = null);

    Task<string?> SelectSaveTextAsync(string inputPath);

    void ShowError(string message);

    void ShowWarning(string message);

    Task<bool> ConfirmAsync(string message, string title = "確認");

    Task<string?> RequestPdfPasswordAsync(string fileName, bool isRetry);
}
