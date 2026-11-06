using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using System.Text;

namespace PdfSyokunin.Services;

public sealed class PdfExtractionService : IPdfExtractionService
{
    public Task<string> ExtractTextAsync(string inputPath, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var result = new StringBuilder();
            using var document = PdfDocument.Open(inputPath);
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (result.Length > 0)
                {
                    result.AppendLine().AppendLine();
                }

                result.Append(ContentOrderTextExtractor.GetText(page));
            }

            return result.ToString();
        }, cancellationToken);

    public Task<int> ExtractImagesAsync(string inputPath, string outputDirectory, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            Directory.CreateDirectory(outputDirectory);
            var count = 0;
            using var document = PdfDocument.Open(inputPath);
            foreach (var page in document.GetPages())
            {
                var imageNumber = 0;
                foreach (var image in page.GetImages())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    imageNumber++;
                    count++;
                    if (image.TryGetPng(out var pngBytes))
                    {
                        File.WriteAllBytes(Path.Combine(outputDirectory, $"p{page.Number}-{imageNumber}.png"), pngBytes);
                    }
                    else
                    {
                        // JPEG/JPXなどPNG化できない形式は、劣化させずエンコード済みデータを保存する。
                        File.WriteAllBytes(Path.Combine(outputDirectory, $"p{page.Number}-{imageNumber}.img"), image.RawBytes.ToArray());
                    }
                }
            }

            return count;
        }, cancellationToken);
}
