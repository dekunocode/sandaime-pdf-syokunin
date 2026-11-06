using PdfSyokunin.Models;

namespace PdfSyokunin.Services;

public interface IPdfThumbnailService
{
    Task<PdfThumbnailLoadResult> TryCreateAsync(
        string inputPath,
        string? password = null,
        CancellationToken cancellationToken = default);
}
