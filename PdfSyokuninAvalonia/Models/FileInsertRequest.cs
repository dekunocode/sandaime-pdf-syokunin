namespace PdfSyokunin.Models;

public sealed record FileInsertRequest(IReadOnlyList<string> Paths, int InsertionIndex);
