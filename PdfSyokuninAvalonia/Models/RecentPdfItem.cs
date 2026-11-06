namespace PdfSyokunin.Models;

public sealed record RecentPdfItem(string FullPath, DateTime LastOpenedUtc)
{
    public string FileName => Path.GetFileName(FullPath);

    public string DirectoryName => Path.GetDirectoryName(FullPath) ?? string.Empty;
}
