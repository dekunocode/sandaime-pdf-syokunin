using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSyokunin.Models;
using PdfSyokunin.Services;

namespace PdfSyokunin.Tests;

[TestClass]
public sealed class PdfServicesTests
{
    private string testDirectory = null!;
    private readonly PdfDocumentService documents = new();

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "PdfSyokuninTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeAsync_すべてのページを指定順で結合する()
    {
        var first = CreatePdf("first.pdf", 2, 400);
        var second = CreatePdf("second.pdf", 1, 500);
        var output = Path.Combine(testDirectory, "merged.pdf");

        await documents.MergeAsync([first, second], output);

        using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.AreEqual(3, result.PageCount);
        Assert.AreEqual(400, result.Pages[0].Width.Point, 0.1);
        Assert.AreEqual(500, result.Pages[2].Width.Point, 0.1);
    }

    [TestMethod]
    public async Task ExtractPagesAsync_指定範囲だけ保存する()
    {
        var input = CreatePdf("source.pdf", 4, 400);
        var output = Path.Combine(testDirectory, "split.pdf");

        await documents.ExtractPagesAsync(input, output, "2-3");

        using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.AreEqual(2, result.PageCount);
        Assert.AreEqual(410, result.Pages[0].Width.Point, 0.1);
        Assert.AreEqual(420, result.Pages[1].Width.Point, 0.1);
    }

    [TestMethod]
    public async Task RotateAsync_既存角度へ指定角度を加算する()
    {
        var input = CreatePdf("rotate-source.pdf", 1, 400);
        var output = Path.Combine(testDirectory, "rotated.pdf");

        await documents.RotateAsync(input, output, 90);

        using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.AreEqual(90, result.Pages[0].Rotate);
    }

    [TestMethod]
    public async Task ReorderAsync_順序変更と同一パス上書きに対応する()
    {
        var input = CreatePdf("overwrite.pdf", 3, 400);

        await documents.ReorderAsync(input, input, [2, 0]);

        using var result = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        Assert.AreEqual(2, result.PageCount);
        Assert.AreEqual(420, result.Pages[0].Width.Point, 0.1);
        Assert.AreEqual(400, result.Pages[1].Width.Point, 0.1);
    }

    [TestMethod]
    public async Task EncryptAndDecryptAsync_パスワード保護を設定して解除する()
    {
        var input = CreatePdf("plain.pdf", 2, 400);
        var encrypted = Path.Combine(testDirectory, "encrypted.pdf");
        var decrypted = Path.Combine(testDirectory, "decrypted.pdf");

        await documents.EncryptAsync(input, encrypted, "test-password");
        Assert.ThrowsException<PdfReaderException>(() => PdfReader.Open(encrypted, PdfDocumentOpenMode.Import));

        using (var protectedDocument = PdfReader.Open(encrypted, "test-password", PdfDocumentOpenMode.Import))
        {
            Assert.AreEqual(2, protectedDocument.PageCount);
        }

        await documents.DecryptAsync(encrypted, decrypted, "test-password");
        using var result = PdfReader.Open(decrypted, PdfDocumentOpenMode.Import);
        Assert.AreEqual(2, result.PageCount);
    }

    [TestMethod]
    public async Task SavePagesAsync_複数PDFを任意順と回転で保存する()
    {
        var first = CreatePdf("workspace-first.pdf", 2, 400);
        var second = CreatePdf("workspace-second.pdf", 1, 500);
        var output = Path.Combine(testDirectory, "workspace-output.pdf");
        PdfPageReference[] pages =
        [
            new(second, 0, 90),
            new(first, 1, 270)
        ];

        await documents.SavePagesAsync(pages, output);

        using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.AreEqual(2, result.PageCount);
        Assert.AreEqual(500, result.Pages[0].Width.Point, 0.1);
        Assert.AreEqual(90, result.Pages[0].Rotate);
        Assert.AreEqual(410, result.Pages[1].Width.Point, 0.1);
        Assert.AreEqual(270, result.Pages[1].Rotate);
    }

    [TestMethod]
    public async Task SavePagesAsync_保護PDFのパスワードを使って保存する()
    {
        var input = CreatePdf("protected-source.pdf", 1, 400);
        var encrypted = Path.Combine(testDirectory, "protected.pdf");
        var output = Path.Combine(testDirectory, "protected-output.pdf");
        await documents.EncryptAsync(input, encrypted, "source-password");

        await documents.SavePagesAsync(
            [new PdfPageReference(encrypted, 0, 90, "source-password")],
            output);

        using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.AreEqual(1, result.PageCount);
        Assert.AreEqual(90, result.Pages[0].Rotate);
    }

    private string CreatePdf(string fileName, int pageCount, double firstPageWidth)
    {
        var path = Path.Combine(testDirectory, fileName);
        using var document = new PdfDocument();
        for (var index = 0; index < pageCount; index++)
        {
            var page = document.AddPage();
            // ページ順を出力後にも識別できるよう、各ページの幅を変える。
            page.Width = XUnit.FromPoint(firstPageWidth + index * 10);
            page.Height = XUnit.FromPoint(600);
        }

        document.Save(path);
        return path;
    }
}
