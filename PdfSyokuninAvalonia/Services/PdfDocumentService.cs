using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSyokunin.Models;
using PdfSyokunin.Utilities;

namespace PdfSyokunin.Services;

public sealed class PdfDocumentService : IPdfDocumentService
{
    public Task<int> GetPageCountAsync(string inputPath, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = OpenFromMemory(inputPath);
            return source.PageCount;
        }, cancellationToken);

    public Task MergeAsync(IReadOnlyList<string> inputPaths, string outputPath, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            if (inputPaths.Count < 2)
            {
                throw new InvalidOperationException("結合するPDFを2つ以上選択してください。");
            }

            using var output = new PdfDocument();
            foreach (var path in inputPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var source = OpenFromMemory(path);
                foreach (var page in source.Pages)
                {
                    output.AddPage(page);
                }
            }

            Save(output, outputPath);
        }, cancellationToken);

    public Task ExtractPagesAsync(string inputPath, string outputPath, string pageRange, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var source = OpenFromMemory(inputPath);
            var indices = PageRangeParser.Parse(pageRange, source.PageCount);
            using var output = new PdfDocument();
            foreach (var index in indices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.AddPage(source.Pages[index]);
            }

            Save(output, outputPath);
        }, cancellationToken);

    public Task RotateAsync(string inputPath, string outputPath, int angle, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            if (angle is not (90 or 180 or 270))
            {
                throw new ArgumentOutOfRangeException(nameof(angle), "回転角度は90、180、270度から選択してください。");
            }

            using var source = OpenFromMemory(inputPath);
            using var output = new PdfDocument();
            foreach (var sourcePage in source.Pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = output.AddPage(sourcePage);
                page.Rotate = NormalizeRotation(page.Rotate + angle);
            }

            Save(output, outputPath);
        }, cancellationToken);

    public Task ReorderAsync(string inputPath, string outputPath, IReadOnlyList<int> pageOrder, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var source = OpenFromMemory(inputPath);
            if (pageOrder.Count == 0)
            {
                throw new InvalidOperationException("保存するページがありません。");
            }

            using var output = new PdfDocument();
            foreach (var index in pageOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index < 0 || index >= source.PageCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(pageOrder));
                }

                output.AddPage(source.Pages[index]);
            }

            Save(output, outputPath);
        }, cancellationToken);

    public Task EncryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = OpenFromMemory(inputPath);
            using var output = CopyAllPages(source);
            output.SecuritySettings.UserPassword = password;
            output.SecuritySettings.OwnerPassword = password;
            // AES-256を使い、古いRC4暗号で新規文書を作らない。
            output.SecurityHandler.SetEncryptionToV5();
            Save(output, outputPath);
        }, cancellationToken);

    public Task DecryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = OpenFromMemory(inputPath, password);
            using var output = CopyAllPages(source);
            Save(output, outputPath);
        }, cancellationToken);

    public Task SavePagesAsync(
        IReadOnlyList<PdfPageReference> pages,
        string outputPath,
        string? password = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            if (pages.Count == 0)
            {
                throw new InvalidOperationException("保存するページがありません。");
            }

            var sources = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var output = new PdfDocument();
                foreach (var pageReference in pages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!sources.TryGetValue(pageReference.SourcePath, out var source))
                    {
                        source = OpenFromMemory(pageReference.SourcePath, pageReference.SourcePassword);
                        sources.Add(pageReference.SourcePath, source);
                    }

                    if (pageReference.OriginalIndex < 0 || pageReference.OriginalIndex >= source.PageCount)
                    {
                        throw new ArgumentOutOfRangeException(nameof(pages), "元PDFのページ番号が範囲外です。");
                    }

                    var page = output.AddPage(source.Pages[pageReference.OriginalIndex]);
                    page.Rotate = NormalizeRotation(page.Rotate + pageReference.Rotation);
                }

                if (!string.IsNullOrEmpty(password))
                {
                    output.SecuritySettings.UserPassword = password;
                    output.SecuritySettings.OwnerPassword = password;
                    output.SecurityHandler.SetEncryptionToV5();
                }

                Save(output, outputPath);
            }
            finally
            {
                foreach (var source in sources.Values)
                {
                    source.Dispose();
                }
            }
        }, cancellationToken);

    private static PdfDocument OpenFromMemory(string path, string? password = null)
    {
        var stream = new MemoryStream(File.ReadAllBytes(path), writable: false);
        try
        {
            return string.IsNullOrEmpty(password)
                ? PdfReader.Open(stream, PdfDocumentOpenMode.Import)
                : PdfReader.Open(stream, password, PdfDocumentOpenMode.Import);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static PdfDocument CopyAllPages(PdfDocument source)
    {
        var output = new PdfDocument();
        foreach (var page in source.Pages)
        {
            output.AddPage(page);
        }

        return output;
    }

    private static void Save(PdfDocument document, string outputPath) =>
        SafeFileWriter.Write(outputPath, document.Save);

    private static int NormalizeRotation(int angle) => ((angle % 360) + 360) % 360;
}
