using PdfSyokunin.Services;

namespace PdfSyokunin.Tests;

[TestClass]
public sealed class RecentFilesServiceTests
{
    private string testDirectory = null!;
    private string settingsPath = null!;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "PdfSyokuninRecentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        settingsPath = Path.Combine(testDirectory, "settings.json");
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
    public void Add_新しい順で最大5件を永続化する()
    {
        var paths = Enumerable.Range(1, 6)
            .Select(index => CreateFile($"document-{index}.pdf"))
            .ToArray();
        var service = new RecentFilesService(settingsPath);

        service.Add(paths);
        var loaded = new RecentFilesService(settingsPath).Load();

        Assert.AreEqual(5, loaded.Count);
        Assert.AreEqual(paths[0], loaded[0].FullPath);
        Assert.IsFalse(loaded.Any(item => item.FullPath == paths[5]));
    }

    [TestMethod]
    public void Clear_保存した履歴をすべて消去する()
    {
        var service = new RecentFilesService(settingsPath);
        service.Add([CreateFile("document.pdf")]);

        service.Clear();

        Assert.AreEqual(0, service.Load().Count);
    }

    [TestMethod]
    public void Remove_指定した履歴だけを削除する()
    {
        var first = CreateFile("first.pdf");
        var second = CreateFile("second.pdf");
        var service = new RecentFilesService(settingsPath);
        service.Add([first, second]);

        service.Remove(first);

        var loaded = service.Load();
        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual(second, loaded[0].FullPath);
        Assert.IsTrue(File.Exists(first), "履歴削除で元PDFを削除してはいけません。");
    }

    private string CreateFile(string fileName)
    {
        var path = Path.Combine(testDirectory, fileName);
        File.WriteAllBytes(path, []);
        return path;
    }
}
