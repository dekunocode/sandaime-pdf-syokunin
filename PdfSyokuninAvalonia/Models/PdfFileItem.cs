using Avalonia.Media;

namespace PdfSyokunin.Models;

public sealed class PdfFileItem(
    string fullPath,
    int pageCount = 0,
    IBrush? brush = null,
    int order = 0,
    bool canMoveUp = false,
    bool canMoveDown = false)
{
    public string FullPath { get; } = fullPath;

    public string FileName => Path.GetFileName(FullPath);

    public string DirectoryName => Path.GetDirectoryName(FullPath) ?? string.Empty;

    public int PageCount { get; } = pageCount;

    public IBrush Brush { get; } = brush ?? Brushes.Gray;

    public int Order { get; } = order;

    public bool CanMoveUp { get; } = canMoveUp;

    public bool CanMoveDown { get; } = canMoveDown;
}
