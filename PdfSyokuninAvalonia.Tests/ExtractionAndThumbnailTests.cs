using PdfSyokunin.Services;

namespace PdfSyokunin.Tests;

[TestClass]
public sealed class ExtractionAndThumbnailTests
{
    private static string TestPdf => Path.Combine(AppContext.BaseDirectory, "TestData", "test_features.pdf");

    [TestMethod]
    public async Task ExtractTextAsync_埋め込みテキストを抽出する()
    {
        var service = new PdfExtractionService();

        var text = await service.ExtractTextAsync(TestPdf);

        StringAssert.Contains(text, "Hello World");
    }

    [TestMethod]
    public async Task ExtractImagesAsync_埋め込み画像を保存する()
    {
        var output = Path.Combine(Path.GetTempPath(), "PdfSyokuninImageTests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new PdfExtractionService();

            var count = await service.ExtractImagesAsync(TestPdf, output);

            Assert.IsTrue(count > 0);
            Assert.IsTrue(Directory.GetFiles(output).Length > 0);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task CreateAsync_Pdfiumでサムネイルを作成する()
    {
        var service = new PdfThumbnailService();

        var result = await service.TryCreateAsync(TestPdf);

        Assert.IsFalse(result.RequiresPassword);
        Assert.AreEqual(1, result.Pages.Count);
        Assert.IsTrue(result.Pages[0].Thumbnail.PixelSize.Width > 0);
        result.Pages[0].Zoom = 0.6;
        Assert.AreEqual(72, result.Pages[0].ThumbnailWidth);
        Assert.AreEqual(96, result.Pages[0].ThumbnailHeight);
    }

    [TestMethod]
    public async Task CreateAsync_保護PDFを正しいパスワードで読み込む()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PdfSyokuninProtectedThumbnailTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var encrypted = Path.Combine(directory, "protected.pdf");
            await new PdfDocumentService().EncryptAsync(TestPdf, encrypted, "thumbnail-password");
            var service = new PdfThumbnailService();

            var rejected = await service.TryCreateAsync(encrypted, "incorrect-password");
            var result = await service.TryCreateAsync(encrypted, "thumbnail-password");

            Assert.IsTrue(rejected.RequiresPassword);
            Assert.IsFalse(result.RequiresPassword);
            Assert.AreEqual(1, result.Pages.Count);
            Assert.IsTrue(result.Pages[0].Thumbnail.PixelSize.Width > 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
