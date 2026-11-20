using Avalonia.Media.Imaging;

namespace PdfSyokunin.Services;

/// <summary>拡大プレビュー用に、1ページを大きな画像として描画する。</summary>
public interface IPdfPageRenderer
{
    Task<Bitmap> RenderAsync(
        string inputPath,
        int pageIndex,
        string? password,
        int width,
        CancellationToken cancellationToken = default);
}
