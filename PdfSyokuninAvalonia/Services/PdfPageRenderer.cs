using Avalonia.Media.Imaging;
using PDFtoImage;
using SkiaSharp;

namespace PdfSyokunin.Services;

public sealed class PdfPageRenderer : IPdfPageRenderer
{
    public Task<Bitmap> RenderAsync(
        string inputPath,
        int pageIndex,
        string? password,
        int width,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pdfBytes = File.ReadAllBytes(inputPath);
            var options = new RenderOptions
            {
                Width = width,
                Height = null,
                WithAspectRatio = true,
                Dpi = 144
            };

            using var bitmap = Conversion.ToImage(pdfBytes, new Index(pageIndex), password, options: options);
            cancellationToken.ThrowIfCancellationRequested();
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 90);
            using var stream = new MemoryStream(encoded.ToArray(), writable: false);
            return new Bitmap(stream);
        }, cancellationToken);
}
