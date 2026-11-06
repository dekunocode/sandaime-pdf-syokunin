using Avalonia.Media.Imaging;
using PdfSyokunin.Models;
using PdfSyokunin.Services;
using PdfSyokunin.ViewModels;

namespace PdfSyokunin.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    private static Bitmap? sharedThumbnail;
    private string directory = string.Empty;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        var testPdf = Path.Combine(AppContext.BaseDirectory, "TestData", "test_features.pdf");
        var result = await new PdfThumbnailService().TryCreateAsync(testPdf);
        sharedThumbnail = result.Pages[0].Thumbnail;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        directory = Path.Combine(Path.GetTempPath(), "PdfSyokuninViewModelTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TestCleanup]
    public void TestCleanup() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    public async Task AcceptFiles_複数ファイルで結合の案内と識別色を設定する()
    {
        var viewModel = CreateViewModel();

        await viewModel.AcceptFilesCommand.ExecuteAsync([CreatePdf("a", 2), CreatePdf("b", 1)]);

        CollectionAssert.AreEqual(new[] { "a.pdf", "b.pdf" }, viewModel.SourceFiles.Select(file => file.FileName).ToArray());
        Assert.AreEqual(WorkspaceTask.Merge, viewModel.ActiveTask);
        Assert.AreNotSame(viewModel.SourceFiles[0].Brush, viewModel.SourceFiles[1].Brush);
        Assert.AreSame(viewModel.SourceFiles[1].Brush, viewModel.Pages[2].SourceBrush);
        Assert.IsFalse(viewModel.SourceFiles[0].CanMoveUp);
        Assert.IsTrue(viewModel.SourceFiles[0].CanMoveDown);
    }

    [TestMethod]
    public async Task MoveSourceDown_ファイル単位で結合順を入れ替えて元に戻せる()
    {
        var viewModel = CreateViewModel();
        await viewModel.AcceptFilesCommand.ExecuteAsync([CreatePdf("a", 2), CreatePdf("b", 2)]);

        viewModel.MoveSourceDownCommand.Execute(viewModel.SourceFiles[0]);

        Assert.AreEqual("b0 b1 a0 a1", Describe(viewModel));
        CollectionAssert.AreEqual(new[] { "b.pdf", "a.pdf" }, viewModel.SourceFiles.Select(file => file.FileName).ToArray());

        viewModel.UndoCommand.Execute(null);

        Assert.AreEqual("a0 a1 b0 b1", Describe(viewModel));
    }

    [TestMethod]
    public async Task MovePages_複数ページを順序を保ったまま挿入位置へ移動する()
    {
        var viewModel = CreateViewModel();
        await viewModel.AcceptFilesCommand.ExecuteAsync([CreatePdf("a", 4)]);
        var moving = new[] { viewModel.Pages[0], viewModel.Pages[2] };

        viewModel.MovePagesCommand.Execute(new PageMoveRequest(moving, 4));

        Assert.AreEqual("a1 a3 a0 a2", Describe(viewModel));
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, viewModel.Pages.Select(page => page.DisplayNumber).ToArray());
    }

    [TestMethod]
    public async Task InsertFiles_指定位置へPDFを挿入する()
    {
        var viewModel = CreateViewModel();
        await viewModel.AcceptFilesCommand.ExecuteAsync([CreatePdf("a", 2)]);

        await viewModel.InsertFilesCommand.ExecuteAsync(new FileInsertRequest([CreatePdf("b", 2)], 1));

        Assert.AreEqual("a0 b0 b1 a1", Describe(viewModel));
        Assert.IsTrue(viewModel.IsDirty);
        Assert.AreEqual(WorkspaceTask.Merge, viewModel.ActiveTask);
    }

    [TestMethod]
    public async Task MovePrevious_移動後も選択状態を保つ()
    {
        var viewModel = CreateViewModel();
        await viewModel.AcceptFilesCommand.ExecuteAsync([CreatePdf("a", 3)]);
        viewModel.Pages[2].IsSelected = true;

        viewModel.MovePreviousCommand.Execute(null);

        Assert.AreEqual("a0 a2 a1", Describe(viewModel));
        Assert.IsTrue(viewModel.Pages[1].IsSelected);
        Assert.AreEqual(1, viewModel.SelectedCount);
    }

    [TestMethod]
    public async Task 選択操作は選択がないとき実行できない()
    {
        var viewModel = CreateViewModel();
        await viewModel.AcceptFilesCommand.ExecuteAsync([CreatePdf("a", 2)]);

        Assert.IsFalse(viewModel.DeleteSelectedCommand.CanExecute(null));
        Assert.IsFalse(viewModel.RotateRightCommand.CanExecute(null));

        viewModel.Pages[0].IsSelected = true;

        Assert.IsTrue(viewModel.DeleteSelectedCommand.CanExecute(null));

        // 保存パネル表示中は、Deleteキーなどで背後のページを変更しない。
        viewModel.OpenSavePanelCommand.Execute("selected");

        Assert.IsTrue(viewModel.SaveSelectionOnly);
        Assert.IsFalse(viewModel.DeleteSelectedCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task 選択がなくなると保存範囲は全ページへ戻る()
    {
        var viewModel = CreateViewModel();
        await viewModel.AcceptFilesCommand.ExecuteAsync([CreatePdf("a", 2)]);
        viewModel.Pages[0].IsSelected = true;
        viewModel.OpenSavePanelCommand.Execute("selected");

        viewModel.ClearSelectionCommand.Execute(null);

        Assert.IsFalse(viewModel.SaveSelectionOnly);
        Assert.IsTrue(viewModel.SaveAllPages);
    }

    private MainViewModel CreateViewModel() =>
        new(new FakeDialogService(), new PdfDocumentService(), new PdfExtractionService(), new FakeThumbnailService(), new FakeRecentFilesService());

    private string CreatePdf(string name, int pageCount)
    {
        // 読み込みは偽のサムネイルサービスが担当するため、中身はページ数だけでよい。
        var path = Path.Combine(directory, name + ".pdf");
        File.WriteAllText(path, pageCount.ToString());
        return path;
    }

    private static string Describe(MainViewModel viewModel) =>
        string.Join(' ', viewModel.Pages.Select(page => Path.GetFileNameWithoutExtension(page.SourcePath) + page.OriginalIndex));

    private sealed class FakeThumbnailService : IPdfThumbnailService
    {
        public Task<PdfThumbnailLoadResult> TryCreateAsync(string inputPath, string? password = null, CancellationToken cancellationToken = default)
        {
            var count = int.Parse(File.ReadAllText(inputPath));
            var pages = Enumerable.Range(0, count)
                .Select(index => new EditablePdfPage(inputPath, index, sharedThumbnail!))
                .ToArray();
            return Task.FromResult(PdfThumbnailLoadResult.Success(pages));
        }
    }

    private sealed class FakeRecentFilesService : IRecentFilesService
    {
        public IReadOnlyList<RecentPdfItem> Load() => [];

        public void Add(IEnumerable<string> paths)
        {
        }

        public void Remove(string path)
        {
        }

        public void Clear()
        {
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public Task<IReadOnlyList<string>> SelectPdfFilesAsync(bool allowMultiple) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string?> SelectSavePdfAsync(string inputPath, string suffix) => Task.FromResult<string?>(null);

        public Task<string?> SelectFolderAsync(string? initialDirectory = null) => Task.FromResult<string?>(null);

        public Task<string?> SelectSaveTextAsync(string inputPath) => Task.FromResult<string?>(null);

        public void ShowError(string message) => Assert.Fail(message);

        public void ShowWarning(string message) => Assert.Fail(message);

        public Task<bool> ConfirmAsync(string message, string title = "確認") => Task.FromResult(true);

        public Task<string?> RequestPdfPasswordAsync(string fileName, bool isRetry) => Task.FromResult<string?>(null);
    }
}
