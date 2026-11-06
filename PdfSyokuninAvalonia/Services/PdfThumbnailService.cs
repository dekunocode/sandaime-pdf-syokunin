using PDFtoImage;
using PDFtoImage.Exceptions;
using PdfSyokunin.Models;
using SkiaSharp;
using Avalonia.Media.Imaging;

namespace PdfSyokunin.Services;

public sealed class PdfThumbnailService : IPdfThumbnailService
{
    public Task<PdfThumbnailLoadResult> TryCreateAsync(
        string inputPath,
        string? password = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            try
            {
                var items = new List<EditablePdfPage>();
                // stringオーバーロードはBase64入力のため、ファイルは一度だけバイト列へ読み込む。
                var pdfBytes = File.ReadAllBytes(inputPath);
                var pageCount = Conversion.GetPageCount(pdfBytes, password);
                var options = new RenderOptions
                {
                    Width = 120,
                    Height = null,
                    WithAspectRatio = true,
                    Dpi = 72
                };

                for (var index = 0; index < pageCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var bitmap = Conversion.ToImage(pdfBytes, new Index(index), password, options: options);
                    using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 85);
                    using var stream = new MemoryStream(encoded.ToArray(), writable: false);
                    var image = new Bitmap(stream);
                    items.Add(new EditablePdfPage(inputPath, index, image, password));
                }

                return PdfThumbnailLoadResult.Success(items);
            }
            catch (PdfPasswordProtectedException)
            {
                // パスワード要求は異常終了ではなく入力待ち状態として上位へ返す。
                return PdfThumbnailLoadResult.PasswordRequired();
            }
        }, cancellationToken);
}
