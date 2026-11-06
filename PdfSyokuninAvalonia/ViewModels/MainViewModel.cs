using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfSyokunin.Models;
using PdfSyokunin.Services;
using PdfSyokunin.Utilities;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;

namespace PdfSyokunin.ViewModels;

public partial class MainViewModel : ObservableObject
{
    // 暗い背景の上でも互いに見分けやすい色を、読み込んだ順に割り当てる。
    private static readonly IBrush[] SourcePalette =
    [
        new ImmutableSolidColorBrush(Color.Parse("#48B5F5")),
        new ImmutableSolidColorBrush(Color.Parse("#F5A524")),
        new ImmutableSolidColorBrush(Color.Parse("#5CD08B")),
        new ImmutableSolidColorBrush(Color.Parse("#E879B9")),
        new ImmutableSolidColorBrush(Color.Parse("#A78BFA")),
        new ImmutableSolidColorBrush(Color.Parse("#F87171")),
        new ImmutableSolidColorBrush(Color.Parse("#2DD4BF")),
        new ImmutableSolidColorBrush(Color.Parse("#FACC15"))
    ];

    private readonly IDialogService dialogs;
    private readonly IPdfDocumentService pdf;
    private readonly IPdfExtractionService extraction;
    private readonly IPdfThumbnailService thumbnails;
    private readonly IRecentFilesService recentFiles;
    private readonly Stack<WorkspaceSnapshot> undoHistory = [];
    private readonly Stack<WorkspaceSnapshot> redoHistory = [];
    private readonly Dictionary<string, IBrush> sourceBrushes = new(StringComparer.OrdinalIgnoreCase);
    private bool mergeGuideShown;
    private CancellationTokenSource? operationCancellation;
    private CancellationTokenSource? notificationCancellation;

    public MainViewModel(
        IDialogService dialogs,
        IPdfDocumentService pdf,
        IPdfExtractionService extraction,
        IPdfThumbnailService thumbnails,
        IRecentFilesService recentFiles)
    {
        this.dialogs = dialogs;
        this.pdf = pdf;
        this.extraction = extraction;
        this.thumbnails = thumbnails;
        this.recentFiles = recentFiles;

        Pages.CollectionChanged += OnPagesCollectionChanged;
        ReloadRecentFiles();
    }

    public ObservableCollection<EditablePdfPage> Pages { get; } = [];

    public ObservableCollection<PdfFileItem> SourceFiles { get; } = [];

    public ObservableCollection<RecentPdfItem> RecentFiles { get; } = [];

    public SettingsViewModel Settings { get; } = new();

    public bool HasDocument => Pages.Count > 0;

    public bool HasRecentFiles => RecentFiles.Count > 0;

    public bool HasProtectedSources => Pages.Any(page => page.IsPasswordProtected);

    public bool CanUndo => undoHistory.Count > 0 && !IsBusy;

    public bool CanRedo => redoHistory.Count > 0 && !IsBusy;

    public bool CanEdit => HasDocument && !IsBusy;

    public int SelectedCount => Pages.Count(page => page.IsSelected);

    public bool HasSelection => Pages.Any(page => page.IsSelected);

    // パネル表示中はキーボードショートカットで背後のページを変更しない。
    public bool CanEditSelection => HasSelection && !IsBusy && !IsSavePanelOpen && !IsInfoOpen;

    public bool HasMultipleSources => SourceFiles.Count > 1;

    public string WorkspaceTitle => SourceFiles.Count switch
    {
        0 => "PDFを開いてください",
        1 => SourceFiles[0].FileName,
        _ => $"{SourceFiles.Count}個のPDFを結合中"
    };

    public string SelectionText
    {
        get
        {
            var selectedCount = SelectedCount;
            return selectedCount > 0
                ? $"{Pages.Count}ページ｜{selectedCount}ページ選択中"
                : $"{Pages.Count}ページ｜ページを選択して操作";
        }
    }

    public string SelectionHeader => $"{SelectedCount}ページを選択中";

    public bool IsGuideVisible => ActiveTask != WorkspaceTask.None && HasDocument;

    public string GuideTitle => ActiveTask switch
    {
        WorkspaceTask.Merge => "PDFの結合",
        WorkspaceTask.Organize => "ページの整理",
        WorkspaceTask.Extract => "ページ・テキスト・画像の抽出",
        WorkspaceTask.Protect => "パスワードの設定・解除",
        _ => string.Empty
    };

    public string GuideText => ActiveTask switch
    {
        WorkspaceTask.Merge =>
            "左の一覧の ↑↓ で結合する順番を決めて［保存］を押してください。PDFを追加すると末尾に、ページの間へドロップするとその位置に入ります。",
        WorkspaceTask.Organize =>
            "ページをクリックして選択します（Ctrl／Shift＋クリックで複数）。ドラッグで並べ替え、右パネルや右クリックで回転・削除できます。",
        WorkspaceTask.Extract =>
            "取り出したいページを選択して、右パネルの「選択ページを書き出す」から形式を選んでください。選択しなければ「ドキュメント全体」から抽出できます。",
        WorkspaceTask.Protect =>
            "［保存］パネルでパスワードを設定できます。保護されたPDFは、パスワードなしで保存すると保護を解除できます。",
        _ => string.Empty
    };

    public string SaveSummary => SourceFiles.Count > 1
        ? $"{SourceFiles.Count}個のPDFを結合・全{Pages.Count}ページ"
        : $"{WorkspaceTitle}・全{Pages.Count}ページ";

    public string AllPagesOptionText => $"すべてのページ（{Pages.Count}ページ）";

    public string SelectedPagesOptionText => $"選択したページのみ（{SelectedCount}ページ）";

    public bool SaveAllPages
    {
        get => !SaveSelectionOnly;
        set
        {
            if (value)
            {
                SaveSelectionOnly = false;
            }
        }
    }

    public bool ShowDecryptNotice => HasProtectedSources && !ProtectOutput;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string busyText = string.Empty;

    [ObservableProperty]
    private bool isDirty;

    [ObservableProperty]
    private bool isInfoOpen;

    [ObservableProperty]
    private bool isSavePanelOpen;

    [ObservableProperty]
    private bool saveSelectionOnly;

    [ObservableProperty]
    private bool protectOutput;

    [ObservableProperty]
    private string outputPassword = string.Empty;

    [ObservableProperty]
    private string confirmOutputPassword = string.Empty;

    [ObservableProperty]
    private string notificationMessage = string.Empty;

    [ObservableProperty]
    private bool isNotificationVisible;

    [ObservableProperty]
    private double pageZoom = 1.0;

    [ObservableProperty]
    private WorkspaceTask activeTask;

    public string ZoomText => $"{PageZoom:P0}";

    public Task LoadStartupFilesAsync(IEnumerable<string> paths) => LoadFilesAsync(paths, replace: true);

    public Task<bool> ConfirmCloseAsync() =>
        !IsDirty
            ? Task.FromResult(true)
            : dialogs.ConfirmAsync("保存していない編集内容があります。終了しますか？", "三代目PDF職人");

    /// <summary>右クリックしたページが未選択なら、そのページだけを操作対象にする。</summary>
    public void SelectOnly(EditablePdfPage target)
    {
        foreach (var page in Pages)
        {
            page.IsSelected = ReferenceEquals(page, target);
        }
    }

    [RelayCommand]
    private async Task StartTaskAsync(string task)
    {
        var selectedTask = Enum.Parse<WorkspaceTask>(task);
        var paths = await dialogs.SelectPdfFilesAsync(allowMultiple: selectedTask == WorkspaceTask.Merge);
        if (paths.Count == 0)
        {
            return;
        }

        await LoadFilesAsync(paths, replace: true);
        if (!HasDocument)
        {
            return;
        }

        ActiveTask = selectedTask;
        if (selectedTask == WorkspaceTask.Protect)
        {
            // 保護済みPDFなら解除、未保護なら設定が目的なので、初期状態をそれに合わせる。
            OpenSavePanel(null);
            ProtectOutput = !HasProtectedSources;
        }
    }

    [RelayCommand]
    private void DismissGuide() => ActiveTask = WorkspaceTask.None;

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var paths = await dialogs.SelectPdfFilesAsync(allowMultiple: true);
        if (paths.Count > 0)
        {
            // 空のワークスペースでは最初の読み込み、編集中なら既存ページへの追加として扱う。
            await LoadFilesAsync(paths, replace: !HasDocument);
        }
    }

    [RelayCommand]
    private async Task AcceptFilesAsync(string[] paths) =>
        await LoadFilesAsync(paths, replace: !HasDocument);

    [RelayCommand]
    private async Task InsertFilesAsync(FileInsertRequest request) =>
        await LoadFilesAsync(request.Paths, replace: !HasDocument, request.InsertionIndex);

    [RelayCommand]
    private async Task InsertFilesAtAsync(int insertionIndex)
    {
        var paths = await dialogs.SelectPdfFilesAsync(allowMultiple: true);
        if (paths.Count > 0)
        {
            await LoadFilesAsync(paths, replace: !HasDocument, insertionIndex);
        }
    }

    [RelayCommand]
    private async Task OpenRecentAsync(RecentPdfItem item)
    {
        if (File.Exists(item.FullPath) && await CanDiscardCurrentWorkspaceAsync())
        {
            await LoadFilesAsync([item.FullPath], replace: true);
        }
        else if (!File.Exists(item.FullPath))
        {
            ReloadRecentFiles();
            dialogs.ShowWarning("ファイルが見つからないため、履歴から取り除きました。");
        }
    }

    [RelayCommand]
    private async Task NewWorkspaceAsync()
    {
        if (!await CanDiscardCurrentWorkspaceAsync())
        {
            return;
        }

        ClearPages();
        SourceFiles.Clear();
        sourceBrushes.Clear();
        undoHistory.Clear();
        redoHistory.Clear();
        IsDirty = false;
        IsSavePanelOpen = false;
        ActiveTask = WorkspaceTask.None;
        mergeGuideShown = false;
        ResetOutputProtection();
        UpdateDerivedState();
    }

    [RelayCommand]
    private void ClearRecentFiles()
    {
        recentFiles.Clear();
        ReloadRecentFiles();
        ShowNotification("最近使ったファイルを消去しました。");
    }

    [RelayCommand]
    private void RemoveRecentFile(RecentPdfItem item)
    {
        recentFiles.Remove(item.FullPath);
        ReloadRecentFiles();
        ShowNotification($"{item.FileName}を履歴から削除しました。");
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var page in Pages)
        {
            page.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var page in Pages)
        {
            page.IsSelected = false;
        }
    }

    [RelayCommand]
    private void Escape()
    {
        if (IsSavePanelOpen)
        {
            IsSavePanelOpen = false;
        }
        else if (IsInfoOpen)
        {
            IsInfoOpen = false;
        }
        else
        {
            ClearSelection();
        }
    }

    [RelayCommand]
    private void AdjustZoom(int wheelDelta) => ChangeZoom(wheelDelta > 0 ? 0.1 : -0.1);

    [RelayCommand]
    private void ZoomIn() => ChangeZoom(0.1);

    [RelayCommand]
    private void ZoomOut() => ChangeZoom(-0.1);

    [RelayCommand]
    private void ResetZoom() => PageZoom = 1.0;

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private void RotateLeft() => RotateSelection(-90);

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private void RotateRight() => RotateSelection(90);

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private void DeleteSelected()
    {
        var targets = GetSelectedPages();
        if (targets.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var page in targets)
        {
            Pages.Remove(page);
        }

        RebuildSourceFiles();
        CompleteEdit($"{targets.Length}ページを編集対象から削除しました。");
    }

    [RelayCommand]
    private void RemoveSource(PdfFileItem file)
    {
        var targets = Pages
            .Where(page => string.Equals(page.SourcePath, file.FullPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var page in targets)
        {
            Pages.Remove(page);
        }

        RebuildSourceFiles();
        CompleteEdit($"{file.FileName}を編集対象から外しました。");
    }

    [RelayCommand]
    private void MoveSourceUp(PdfFileItem file) => MoveSource(file, -1);

    [RelayCommand]
    private void MoveSourceDown(PdfFileItem file) => MoveSource(file, 1);

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private void MovePrevious() => MoveSelectionOneStep(-1);

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private void MoveNext() => MoveSelectionOneStep(1);

    [RelayCommand]
    private void MovePages(PageMoveRequest request)
    {
        var moving = request.Pages.ToHashSet();
        var insertionIndex = Math.Clamp(request.InsertionIndex, 0, Pages.Count);
        var movingInOrder = Pages.Where(moving.Contains).ToArray();
        if (movingInOrder.Length == 0)
        {
            return;
        }

        // 挿入位置は移動前の一覧基準なので、移動するページを除いた位置へ換算する。
        var remaining = Pages.Where(page => !moving.Contains(page)).ToList();
        var adjustedIndex = Pages.Take(insertionIndex).Count(page => !moving.Contains(page));
        remaining.InsertRange(adjustedIndex, movingInOrder);
        if (remaining.SequenceEqual(Pages))
        {
            return;
        }

        PushUndoSnapshot();
        ApplyPageOrder(remaining);
        RebuildSourceFiles();
        CompleteEdit(movingInOrder.Length == 1 ? "ページを並び替えました。" : $"{movingInOrder.Length}ページを移動しました。");
    }

    [RelayCommand]
    private void Undo()
    {
        if (undoHistory.Count == 0 || IsBusy)
        {
            return;
        }

        redoHistory.Push(CaptureSnapshot());
        RestoreSnapshot(undoHistory.Pop());
        IsDirty = true;
        UpdateDerivedState();
        ShowNotification("編集操作を元に戻しました。");
    }

    [RelayCommand]
    private void Redo()
    {
        if (redoHistory.Count == 0 || IsBusy)
        {
            return;
        }

        undoHistory.Push(CaptureSnapshot());
        RestoreSnapshot(redoHistory.Pop());
        IsDirty = true;
        UpdateDerivedState();
        ShowNotification("編集操作をやり直しました。");
    }

    [RelayCommand]
    private void OpenSavePanel(string? scope)
    {
        if (!HasDocument || IsBusy)
        {
            return;
        }

        SaveSelectionOnly = scope == "selected" && HasSelection;
        IsInfoOpen = false;
        IsSavePanelOpen = true;
    }

    [RelayCommand]
    private void CloseSavePanel() => IsSavePanelOpen = false;

    [RelayCommand]
    private async Task ConfirmSaveAsync()
    {
        var targets = SaveSelectionOnly ? GetSelectedPages() : Pages.ToArray();
        if (targets.Length == 0 || !ValidateOutputPassword())
        {
            return;
        }

        var suffix = SaveSelectionOnly ? "_pages"
            : ShowDecryptNotice ? "_decrypted"
            : SourceFiles.Count > 1 ? "_combined"
            : "_edited";
        var output = await dialogs.SelectSavePdfAsync(targets[0].SourcePath, suffix);
        if (output is null)
        {
            return;
        }

        var password = ProtectOutput ? OutputPassword : null;
        var saved = await RunAsync(
            token => pdf.SavePagesAsync(GetPageReferences(targets), output, password, token),
            "PDFを保存しています…",
            SaveSelectionOnly ? "選択ページをPDFへ書き出しました。" : "編集したPDFを保存しました。");
        if (!saved)
        {
            return;
        }

        // 一部だけの書き出しでは、ワークスペース全体の変更はまだ保存されていない。
        if (!SaveSelectionOnly)
        {
            IsDirty = false;
        }

        recentFiles.Add([output]);
        ReloadRecentFiles();
        ResetOutputProtection();
        IsSavePanelOpen = false;
    }

    [RelayCommand]
    private async Task ExtractTextAsync(string? scope)
    {
        var targets = GetPagesForScope(scope);
        if (targets.Length == 0)
        {
            return;
        }

        var output = await dialogs.SelectSaveTextAsync(targets[0].SourcePath);
        if (output is null)
        {
            return;
        }

        await RunWithTemporaryPdfAsync(
            targets,
            async (temporaryPdf, token) =>
            {
                var text = await extraction.ExtractTextAsync(temporaryPdf, token);
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new InvalidOperationException("テキストが見つかりませんでした。画像PDFにはOCRが必要です。");
                }

                await Task.Run(
                    () => SafeFileWriter.Write(
                        output,
                        temporaryPath => File.WriteAllText(
                            temporaryPath,
                            text,
                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))),
                    token);
            },
            "テキストを抽出しています…",
            "テキストを保存しました。");
    }

    [RelayCommand]
    private async Task ExtractImagesAsync(string? scope)
    {
        var targets = GetPagesForScope(scope);
        if (targets.Length == 0)
        {
            return;
        }

        var outputDirectory = await dialogs.SelectFolderAsync(Path.GetDirectoryName(targets[0].SourcePath));
        if (outputDirectory is null)
        {
            return;
        }

        await RunWithTemporaryPdfAsync(
            targets,
            async (temporaryPdf, token) =>
            {
                var count = await extraction.ExtractImagesAsync(temporaryPdf, outputDirectory, token);
                if (count == 0)
                {
                    throw new InvalidOperationException("埋め込み画像が見つかりませんでした。");
                }
            },
            "画像を抽出しています…",
            "埋め込み画像を保存しました。");
    }

    [RelayCommand]
    private void CancelOperation() => operationCancellation?.Cancel();

    [RelayCommand]
    private void OpenInfo() => IsInfoOpen = true;

    [RelayCommand]
    private void CloseInfo() => IsInfoOpen = false;

    private async Task LoadFilesAsync(IEnumerable<string> paths, bool replace, int? insertionIndex = null)
    {
        var normalizedPaths = paths
            .Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => replace || SourceFiles.All(file => !string.Equals(file.FullPath, path, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (normalizedPaths.Length == 0)
        {
            if (!replace && paths.Any())
            {
                ShowNotification("同じPDFはすでに追加されています。");
            }

            return;
        }

        var loadedPages = new List<EditablePdfPage>();
        var loaded = await RunAsync(async token =>
        {
            foreach (var path in normalizedPaths)
            {
                token.ThrowIfCancellationRequested();
                BusyText = $"{Path.GetFileName(path)}を読み込んでいます…";
                var thumbnailsForFile = await LoadThumbnailsWithPasswordAsync(path, token);
                loadedPages.AddRange(thumbnailsForFile);
            }
        }, "PDFを読み込んでいます…", string.Empty);
        if (!loaded)
        {
            return;
        }

        if (replace)
        {
            ClearPages();
            sourceBrushes.Clear();
            undoHistory.Clear();
            redoHistory.Clear();
            IsDirty = false;
            ActiveTask = WorkspaceTask.None;
            mergeGuideShown = false;
        }
        else
        {
            PushUndoSnapshot();
            IsDirty = true;
        }

        var index = Math.Clamp(insertionIndex ?? Pages.Count, 0, Pages.Count);
        foreach (var page in loadedPages)
        {
            AddPage(page, index++);
        }

        RebuildSourceFiles();
        recentFiles.Add(normalizedPaths);
        ReloadRecentFiles();
        UpdatePageNumbers();
        UpdateDerivedState();

        // 複数ファイルになった瞬間が「結合」の始まりなので、一度だけ操作方法を案内する。
        if (SourceFiles.Count > 1 && !mergeGuideShown && ActiveTask is WorkspaceTask.None or WorkspaceTask.Organize)
        {
            ActiveTask = WorkspaceTask.Merge;
        }

        var position = insertionIndex is null || replace ? string.Empty : $"{index - loadedPages.Count + 1}ページ目の位置へ";
        ShowNotification(replace
            ? $"{normalizedPaths.Length}個のPDFを読み込みました。"
            : $"{normalizedPaths.Length}個のPDFを{position}結合しました。");
    }

    private void RotateSelection(int angle)
    {
        var targets = GetSelectedPages();
        if (targets.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var page in targets)
        {
            page.Rotation = ((page.Rotation + angle) % 360 + 360) % 360;
        }

        CompleteEdit(angle < 0 ? "選択ページを左へ回転しました。" : "選択ページを右へ回転しました。");
    }

    private void MoveSource(PdfFileItem file, int direction)
    {
        var order = SourceFiles.Select(item => item.FullPath).ToList();
        var index = order.FindIndex(path => string.Equals(path, file.FullPath, StringComparison.OrdinalIgnoreCase));
        var swapIndex = index + direction;
        if (index < 0 || swapIndex < 0 || swapIndex >= order.Count)
        {
            return;
        }

        (order[index], order[swapIndex]) = (order[swapIndex], order[index]);
        var rank = order
            .Select((path, position) => (path, position))
            .ToDictionary(item => item.path, item => item.position, StringComparer.OrdinalIgnoreCase);

        // OrderByは安定ソートなので、ファイル内のページ順は保たれる。
        var newOrder = Pages.OrderBy(page => rank[page.SourcePath]).ToArray();
        PushUndoSnapshot();
        ApplyPageOrder(newOrder);
        RebuildSourceFiles();
        CompleteEdit($"{file.FileName}の結合順を変更しました。");
    }

    private void ApplyPageOrder(IReadOnlyList<EditablePdfPage> order)
    {
        // ListBoxは移動したアイテムの選択を外すことがあるため、選択状態を保ったまま並べ替える。
        var selected = GetSelectedPages().ToHashSet();
        for (var index = 0; index < order.Count; index++)
        {
            var current = Pages.IndexOf(order[index]);
            if (current != index)
            {
                Pages.Move(current, index);
            }
        }

        foreach (var page in Pages)
        {
            page.IsSelected = selected.Contains(page);
        }
    }

    private async Task<IReadOnlyList<EditablePdfPage>> LoadThumbnailsWithPasswordAsync(
        string path,
        CancellationToken cancellationToken)
    {
        string? password = null;
        var isRetry = false;
        while (true)
        {
            var result = await thumbnails.TryCreateAsync(path, password, cancellationToken);
            if (!result.RequiresPassword)
            {
                return result.Pages;
            }

            password = await dialogs.RequestPdfPasswordAsync(Path.GetFileName(path), isRetry);
            if (password is null)
            {
                throw new OperationCanceledException();
            }

            isRetry = true;
        }
    }

    private void ChangeZoom(double amount) =>
        PageZoom = Math.Clamp(Math.Round(PageZoom + amount, 1), 0.6, 2.0);

    partial void OnPageZoomChanged(double value)
    {
        foreach (var page in Pages)
        {
            page.Zoom = value;
        }

        OnPropertyChanged(nameof(ZoomText));
    }

    partial void OnActiveTaskChanged(WorkspaceTask value)
    {
        if (value == WorkspaceTask.Merge)
        {
            mergeGuideShown = true;
        }

        OnPropertyChanged(nameof(IsGuideVisible));
        OnPropertyChanged(nameof(GuideTitle));
        OnPropertyChanged(nameof(GuideText));
    }

    partial void OnSaveSelectionOnlyChanged(bool value) => OnPropertyChanged(nameof(SaveAllPages));

    partial void OnProtectOutputChanged(bool value) => OnPropertyChanged(nameof(ShowDecryptNotice));

    partial void OnIsSavePanelOpenChanged(bool value) => NotifySelectionChanged();

    partial void OnIsInfoOpenChanged(bool value) => NotifySelectionChanged();

    partial void OnIsBusyChanged(bool value) => NotifySelectionChanged();

    private void MoveSelectionOneStep(int direction)
    {
        var selected = GetSelectedPages().ToHashSet();
        if (selected.Count == 0)
        {
            return;
        }

        var order = Pages.ToList();
        if (direction < 0)
        {
            for (var index = 1; index < order.Count; index++)
            {
                if (selected.Contains(order[index]) && !selected.Contains(order[index - 1]))
                {
                    (order[index - 1], order[index]) = (order[index], order[index - 1]);
                }
            }
        }
        else
        {
            for (var index = order.Count - 2; index >= 0; index--)
            {
                if (selected.Contains(order[index]) && !selected.Contains(order[index + 1]))
                {
                    (order[index], order[index + 1]) = (order[index + 1], order[index]);
                }
            }
        }

        if (order.SequenceEqual(Pages))
        {
            return;
        }

        PushUndoSnapshot();
        ApplyPageOrder(order);
        RebuildSourceFiles();
        CompleteEdit("選択ページを移動しました。");
    }

    private async Task RunWithTemporaryPdfAsync(
        IReadOnlyList<EditablePdfPage> pages,
        Func<string, CancellationToken, Task> operation,
        string busyMessage,
        string successMessage)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "PdfSyokunin");
        var temporaryPdf = Path.Combine(temporaryDirectory, $"{Guid.NewGuid():N}.pdf");
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            await RunAsync(async token =>
            {
                await pdf.SavePagesAsync(GetPageReferences(pages), temporaryPdf, cancellationToken: token);
                await operation(temporaryPdf, token);
            }, busyMessage, successMessage);
        }
        finally
        {
            if (File.Exists(temporaryPdf))
            {
                File.Delete(temporaryPdf);
            }
        }
    }

    private async Task<bool> RunAsync(
        Func<CancellationToken, Task> operation,
        string busyMessage,
        string successMessage)
    {
        if (IsBusy)
        {
            return false;
        }

        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        BusyText = busyMessage;
        UpdateDerivedState();
        try
        {
            await operation(operationCancellation.Token);
            if (!string.IsNullOrEmpty(successMessage))
            {
                ShowNotification(successMessage);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            ShowNotification("処理をキャンセルしました。");
            return false;
        }
        catch (Exception exception)
        {
            dialogs.ShowError(ToUserMessage(exception));
            return false;
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
            operationCancellation.Dispose();
            operationCancellation = null;
            UpdateDerivedState();
        }
    }

    private void PushUndoSnapshot()
    {
        undoHistory.Push(CaptureSnapshot());
        redoHistory.Clear();
        UpdateDerivedState();
    }

    private WorkspaceSnapshot CaptureSnapshot() =>
        new(Pages.Select(page => new PageState(page, page.Rotation, page.IsSelected)).ToArray());

    private void RestoreSnapshot(WorkspaceSnapshot snapshot)
    {
        ClearPages();
        foreach (var state in snapshot.Pages)
        {
            state.Page.Rotation = state.Rotation;
            state.Page.IsSelected = state.IsSelected;
            AddPage(state.Page, Pages.Count);
        }

        RebuildSourceFiles();
        UpdatePageNumbers();
    }

    private void CompleteEdit(string notification)
    {
        IsDirty = true;
        UpdatePageNumbers();
        UpdateDerivedState();
        ShowNotification(notification);
    }

    private EditablePdfPage[] GetSelectedPages() =>
        Pages.Where(page => page.IsSelected).ToArray();

    private EditablePdfPage[] GetPagesForScope(string? scope) =>
        scope == "selected" ? GetSelectedPages() : Pages.ToArray();

    private static PdfPageReference[] GetPageReferences(IEnumerable<EditablePdfPage> pages) =>
        pages.Select(page => page.ToReference()).ToArray();

    private bool ValidateOutputPassword()
    {
        if (!ProtectOutput)
        {
            return true;
        }

        if (string.IsNullOrEmpty(OutputPassword))
        {
            dialogs.ShowWarning("保存用パスワードを入力してください。");
            return false;
        }

        if (OutputPassword != ConfirmOutputPassword)
        {
            dialogs.ShowWarning("確認用パスワードが一致しません。");
            return false;
        }

        return true;
    }

    private void ResetOutputProtection()
    {
        OutputPassword = string.Empty;
        ConfirmOutputPassword = string.Empty;
        ProtectOutput = false;
    }

    private Task<bool> CanDiscardCurrentWorkspaceAsync() =>
        !IsDirty
            ? Task.FromResult(true)
            : dialogs.ConfirmAsync("保存していない編集内容を破棄しますか？");

    private void AddPage(EditablePdfPage page, int index)
    {
        page.Zoom = PageZoom;
        page.SourceBrush = GetSourceBrush(page.SourcePath);
        page.PropertyChanged += OnPagePropertyChanged;
        Pages.Insert(index, page);
    }

    private IBrush GetSourceBrush(string sourcePath)
    {
        if (!sourceBrushes.TryGetValue(sourcePath, out var brush))
        {
            brush = SourcePalette[sourceBrushes.Count % SourcePalette.Length];
            sourceBrushes[sourcePath] = brush;
        }

        return brush;
    }

    private void ClearPages()
    {
        foreach (var page in Pages)
        {
            page.PropertyChanged -= OnPagePropertyChanged;
        }

        Pages.Clear();
    }

    private void RebuildSourceFiles()
    {
        SourceFiles.Clear();
        var paths = Pages.Select(page => page.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        for (var index = 0; index < paths.Length; index++)
        {
            var path = paths[index];
            var pageCount = Pages.Count(page => string.Equals(page.SourcePath, path, StringComparison.OrdinalIgnoreCase));
            SourceFiles.Add(new PdfFileItem(
                path,
                pageCount,
                GetSourceBrush(path),
                index + 1,
                canMoveUp: index > 0,
                canMoveDown: index < paths.Length - 1));
        }

        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(HasMultipleSources));
    }

    private void ReloadRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var item in recentFiles.Load())
        {
            RecentFiles.Add(item);
        }

        OnPropertyChanged(nameof(HasRecentFiles));
    }

    private void UpdatePageNumbers()
    {
        for (var index = 0; index < Pages.Count; index++)
        {
            Pages[index].DisplayNumber = index + 1;
        }
    }

    private void UpdateDerivedState()
    {
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(HasProtectedSources));
        OnPropertyChanged(nameof(ShowDecryptNotice));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(IsGuideVisible));
        OnPropertyChanged(nameof(SaveSummary));
        OnPropertyChanged(nameof(AllPagesOptionText));
        NotifySelectionChanged();
    }

    private void NotifySelectionChanged()
    {
        if (SaveSelectionOnly && !HasSelection)
        {
            SaveSelectionOnly = false;
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanEditSelection));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(SelectionHeader));
        OnPropertyChanged(nameof(SelectedPagesOptionText));
        RotateLeftCommand.NotifyCanExecuteChanged();
        RotateRightCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        MovePreviousCommand.NotifyCanExecuteChanged();
        MoveNextCommand.NotifyCanExecuteChanged();
    }

    private void OnPagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.OldItems is not null && eventArgs.Action != NotifyCollectionChangedAction.Move)
        {
            foreach (EditablePdfPage page in eventArgs.OldItems)
            {
                page.PropertyChanged -= OnPagePropertyChanged;
            }
        }

        UpdatePageNumbers();
        UpdateDerivedState();
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(EditablePdfPage.IsSelected))
        {
            NotifySelectionChanged();
        }
    }

    private async void ShowNotification(string message)
    {
        notificationCancellation?.Cancel();
        notificationCancellation?.Dispose();
        notificationCancellation = new CancellationTokenSource();
        NotificationMessage = message;
        IsNotificationVisible = true;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), notificationCancellation.Token);
            IsNotificationVisible = false;
        }
        catch (OperationCanceledException)
        {
            // 新しい通知へ置き換えられた場合は、古い表示タイマーだけを終了する。
        }
    }

    private static string ToUserMessage(Exception exception)
    {
        if (exception.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return "PDFを開けませんでした。パスワードが正しいか確認してください。";
        }

        return exception.Message;
    }

    private sealed record PageState(EditablePdfPage Page, int Rotation, bool IsSelected);

    private sealed record WorkspaceSnapshot(IReadOnlyList<PageState> Pages);
}
