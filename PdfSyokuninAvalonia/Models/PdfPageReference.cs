namespace PdfSyokunin.Models;

public sealed record PdfPageReference(
    string SourcePath,
    int OriginalIndex,
    int Rotation,
    string? SourcePassword = null);
