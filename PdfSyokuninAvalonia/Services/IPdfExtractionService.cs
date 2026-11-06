namespace PdfSyokunin.Services;

public interface IPdfExtractionService
{
    Task<string> ExtractTextAsync(string inputPath, CancellationToken cancellationToken = default);

    Task<int> ExtractImagesAsync(string inputPath, string outputDirectory, CancellationToken cancellationToken = default);
}
