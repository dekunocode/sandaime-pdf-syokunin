using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PdfSyokunin.Models;

public partial class EditablePdfPage(
    string sourcePath,
    int originalIndex,
    Bitmap thumbnail,
    string? sourcePassword = null) : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    public string SourcePath { get; } = sourcePath;

    public string SourceFileName => Path.GetFileName(SourcePath);

    public int OriginalIndex { get; } = originalIndex;

    public int OriginalPageNumber => OriginalIndex + 1;

    public Bitmap Thumbnail { get; } = thumbnail;

    public double ThumbnailWidth => 120 * Zoom;

    public double ThumbnailHeight => 160 * Zoom;

    public bool IsPasswordProtected => SourcePassword is not null;

    // 保護PDFを保存し直すために必要だが、ディスクへは永続化せず編集セッション中だけ保持する。
    internal string? SourcePassword { get; } = sourcePassword;

    [ObservableProperty]
    private int displayNumber;

    [ObservableProperty]
    private int rotation;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isDropBefore;

    [ObservableProperty]
    private bool isDropAfter;

    [ObservableProperty]
    private bool isDragging;

    // 結合元ファイルごとの識別色。ワークスペース側で割り当てる。
    [ObservableProperty]
    private IBrush sourceBrush = Brushes.Gray;

    [ObservableProperty]
    private double zoom = 1.0;

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ThumbnailWidth));
        OnPropertyChanged(nameof(ThumbnailHeight));
    }

    public PdfPageReference ToReference() => new(SourcePath, OriginalIndex, Rotation, SourcePassword);
}
