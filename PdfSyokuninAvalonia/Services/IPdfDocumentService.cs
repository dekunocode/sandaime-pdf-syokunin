namespace PdfSyokunin.Services;

using PdfSyokunin.Models;

public interface IPdfDocumentService
{
    Task<int> GetPageCountAsync(string inputPath, CancellationToken cancellationToken = default);

    Task MergeAsync(IReadOnlyList<string> inputPaths, string outputPath, CancellationToken cancellationToken = default);

    Task ExtractPagesAsync(string inputPath, string outputPath, string pageRange, CancellationToken cancellationToken = default);

    Task RotateAsync(string inputPath, string outputPath, int angle, CancellationToken cancellationToken = default);

    Task ReorderAsync(string inputPath, string outputPath, IReadOnlyList<int> pageOrder, CancellationToken cancellationToken = default);

    Task EncryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default);

    Task DecryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default);

    Task SavePagesAsync(
        IReadOnlyList<PdfPageReference> pages,
        string outputPath,
        string? password = null,
        CancellationToken cancellationToken = default);
}
