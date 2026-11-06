namespace PdfSyokunin.Models;

public sealed record PdfThumbnailLoadResult(
    IReadOnlyList<EditablePdfPage> Pages,
    bool RequiresPassword)
{
    public static PdfThumbnailLoadResult Success(IReadOnlyList<EditablePdfPage> pages) =>
        new(pages, RequiresPassword: false);

    public static PdfThumbnailLoadResult PasswordRequired() =>
        new([], RequiresPassword: true);
}
