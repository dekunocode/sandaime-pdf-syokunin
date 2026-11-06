using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Collections.Specialized;
using PdfSyokunin.Models;
using PdfSyokunin.Services;
using PdfSyokunin.ViewModels;

namespace PdfSyokunin;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly ScrollViewer pageScrollViewer;
    private readonly ListBox pageList;
    private readonly Border dragHint;
    private readonly TextBlock dragHintText;
    private bool closeApproved;
    private EditablePdfPage? pressedPage;
    private bool collapseSelectionOnRelease;
    private HashSet<EditablePdfPage> movingPages = [];
    private EditablePdfPage? dropTarget;
    private bool dropAfter;
    private Point dragStart;
    private bool pageDragActive;
    private EditablePdfPage? focusedPage;
    private int focusedPageIndex;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainViewModel(
            new DialogService(() => this),
            new PdfDocumentService(),
            new PdfExtractionService(),
            new PdfThumbnailService(),
            new RecentFilesService());
        DataContext = viewModel;

        pageScrollViewer = this.FindControl<ScrollViewer>("PageScrollViewer")!;
        pageList = this.FindControl<ListBox>("PageList")!;
        dragHint = this.FindControl<Border>("DragHint")!;
        dragHintText = this.FindControl<TextBlock>("DragHintText")!;

        // ListBoxやScrollViewerがホイールを消費する前にCtrl操作を捕捉する。
        // handledEventsTooを有効にし、スクロール位置に関係なくズームを優先する。
        pageScrollViewer.AddHandler(
            InputElement.PointerWheelChangedEvent,
            PageScrollViewer_OnPointerWheelChanged,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        // カードのどこをつかんでもドラッグできるよう、ListBoxItemが選択処理をする前に
        // トンネルで押下を捕捉する。
        pageList.AddHandler(PointerPressedEvent, PageList_OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        pageList.AddHandler(PointerMovedEvent, PageList_OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        pageList.AddHandler(PointerReleasedEvent, PageList_OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        pageList.AddHandler(PointerCaptureLostEvent, (_, _) => FinishPageDrag());

        // ListBoxは Ctrl+矢印 をフォーカス移動に使うため、先にページ移動として処理する。
        AddHandler(KeyDownEvent, Window_OnPreviewKeyDown, RoutingStrategies.Tunnel);

        // ListBoxは移動・削除したアイテムのコンテナを作り直すため、そのページにあったフォーカスが失われる。
        // 放置すると次のキー操作でウィンドウ先頭のボタンへフォーカスが飛ぶので、ページ一覧へ戻す。
        AddHandler(GotFocusEvent, Window_OnGotFocus, handledEventsToo: true);
        viewModel.Pages.CollectionChanged += Pages_OnCollectionChanged;

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, Window_OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, Window_OnDragLeave);
        AddHandler(DragDrop.DropEvent, Window_OnDrop);
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        if (Application.Current is App app && app.StartupPdfPaths.Count > 0)
        {
            await viewModel.LoadStartupFilesAsync(app.StartupPdfPaths);
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closeApproved)
        {
            return;
        }

        e.Cancel = true;
        if (await viewModel.ConfirmCloseAsync())
        {
            closeApproved = true;
            Close();
        }
    }

    private void Window_OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control || e.Source is TextBox)
        {
            return;
        }

        var command = e.Key switch
        {
            Key.Left => viewModel.MovePreviousCommand,
            Key.Right => viewModel.MoveNextCommand,
            _ => null
        };
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private void Window_OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        focusedPage = FindPage(e.Source, out _);
        if (focusedPage is not null)
        {
            focusedPageIndex = viewModel.Pages.IndexOf(focusedPage);
        }
    }

    private void Pages_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (focusedPage is null)
        {
            return;
        }

        // 削除された場合に備え、同じ位置のページへフォーカスを移せるよう最新の位置を覚えておく。
        var index = viewModel.Pages.IndexOf(focusedPage);
        if (index >= 0)
        {
            focusedPageIndex = index;
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            focusedPageIndex = e.OldStartingIndex;
        }

        // コンテナの再生成はレイアウト時に行われるため、完了後に復元する。
        Dispatcher.UIThread.Post(RestorePageFocus, DispatcherPriority.Loaded);
    }

    private void RestorePageFocus()
    {
        var current = FocusManager?.GetFocusedElement() as Visual;
        if (focusedPage is null || current is not null && current.IsAttachedToVisualTree())
        {
            return;
        }

        var pages = viewModel.Pages;
        var target = pages.Contains(focusedPage)
            ? focusedPage
            : pages.Count > 0 ? pages[Math.Clamp(focusedPageIndex, 0, pages.Count - 1)] : null;
        if (target is null)
        {
            focusedPage = null;
            return;
        }

        if (pageList.ContainerFromItem(target) is Control container)
        {
            container.BringIntoView();
            container.Focus();
        }
        else
        {
            pageList.Focus();
        }
    }

    private void PageScrollViewer_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var delta = e.Delta.Y > 0 ? 120 : -120;
            if (viewModel.AdjustZoomCommand.CanExecute(delta))
            {
                viewModel.AdjustZoomCommand.Execute(delta);
            }

            e.Handled = true;
        }
    }

    private void PageList_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var page = FindPage(e.Source, out var fromCheckBox);
        if (page is null || fromCheckBox || !e.GetCurrentPoint(pageList).Properties.IsLeftButtonPressed)
        {
            pressedPage = null;
            return;
        }

        pressedPage = page;
        dragStart = e.GetPosition(this);
        pageDragActive = false;

        // 複数選択中のページを押した時点では選択を崩さず、まとめてドラッグできるようにする。
        // ドラッグせずに離した場合だけ、エクスプローラーと同じくそのページ1件に絞る。
        collapseSelectionOnRelease = page.IsSelected && e.KeyModifiers == KeyModifiers.None && viewModel.SelectedCount > 1;
        if (collapseSelectionOnRelease)
        {
            pageList.Focus();
            e.Handled = true;
        }
    }

    private void PageList_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (pressedPage is null || !e.GetCurrentPoint(pageList).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (!pageDragActive)
        {
            if (Math.Abs(point.X - dragStart.X) < 8 && Math.Abs(point.Y - dragStart.Y) < 8)
            {
                return;
            }

            // 選択中のページをつかんだ場合は、選択ページをまとめて移動する。
            movingPages = pressedPage.IsSelected
                ? viewModel.Pages.Where(page => page.IsSelected).ToHashSet()
                : [pressedPage];
            foreach (var page in movingPages)
            {
                page.IsDragging = true;
            }

            pageDragActive = true;
            e.Pointer.Capture(pageList);
            ShowDragHint(movingPages.Count == 1
                ? "水色のラインの位置へページを移動"
                : $"{movingPages.Count}ページを水色のラインの位置へ移動");
        }

        UpdateInsertionIndicator(point);
        e.Handled = true;
    }

    private void PageList_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!pageDragActive)
        {
            if (collapseSelectionOnRelease && pressedPage is not null)
            {
                viewModel.SelectOnly(pressedPage);
                e.Handled = true;
            }

            pressedPage = null;
            collapseSelectionOnRelease = false;
            return;
        }

        var moving = viewModel.Pages.Where(movingPages.Contains).ToArray();
        var insertionIndex = GetInsertionIndex();
        if (insertionIndex is not null)
        {
            var request = new PageMoveRequest(moving, insertionIndex.Value);
            if (viewModel.MovePagesCommand.CanExecute(request))
            {
                viewModel.MovePagesCommand.Execute(request);
            }
        }

        e.Pointer.Capture(null);
        FinishPageDrag();
        e.Handled = true;
    }

    private async void PageMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: EditablePdfPage page, Tag: string action })
        {
            return;
        }

        var index = viewModel.Pages.IndexOf(page);
        switch (action)
        {
            case "insert-before":
                await viewModel.InsertFilesAtCommand.ExecuteAsync(index);
                return;
            case "insert-after":
                await viewModel.InsertFilesAtCommand.ExecuteAsync(index + 1);
                return;
        }

        // 未選択のページを右クリックした場合は、そのページだけを操作対象にする。
        if (!page.IsSelected)
        {
            viewModel.SelectOnly(page);
        }

        switch (action)
        {
            case "rotate-right":
                viewModel.RotateRightCommand.Execute(null);
                break;
            case "rotate-left":
                viewModel.RotateLeftCommand.Execute(null);
                break;
            case "export":
                viewModel.OpenSavePanelCommand.Execute("selected");
                break;
            case "delete":
                viewModel.DeleteSelectedCommand.Execute(null);
                break;
        }
    }

    private static EditablePdfPage? FindPage(object? source, out bool fromCheckBox)
    {
        fromCheckBox = false;
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is CheckBox)
            {
                fromCheckBox = true;
            }

            if (visual is ListBoxItem { DataContext: EditablePdfPage page })
            {
                return page;
            }
        }

        return null;
    }

    private int? GetInsertionIndex() =>
        dropTarget is null ? null : viewModel.Pages.IndexOf(dropTarget) + (dropAfter ? 1 : 0);

    private void UpdateInsertionIndicator(Point pointer)
    {
        var targetCard = FindNearestPageCard(pointer);
        var target = targetCard?.DataContext as EditablePdfPage;
        if (targetCard is null || target is null || movingPages.Contains(target))
        {
            ClearInsertionIndicator();
            return;
        }

        var topLeft = targetCard.TranslatePoint(new Point(), this);
        if (topLeft is null)
        {
            ClearInsertionIndicator();
            return;
        }

        var bounds = new Rect(topLeft.Value, targetCard.Bounds.Size);
        var insertAfter = pointer.Y > bounds.Bottom ||
                          (pointer.Y >= bounds.Top && pointer.X >= bounds.Center.X);
        if (ReferenceEquals(dropTarget, target) && dropAfter == insertAfter)
        {
            return;
        }

        ClearInsertionIndicator();
        dropTarget = target;
        dropAfter = insertAfter;
        target.IsDropBefore = !insertAfter;
        target.IsDropAfter = insertAfter;
    }

    private Border? FindNearestPageCard(Point pointer)
    {
        Border? nearest = null;
        var nearestDistanceSquared = double.MaxValue;
        foreach (var card in pageList.GetVisualDescendants()
                     .OfType<Border>()
                     .Where(candidate => candidate.Classes.Contains("page-card") &&
                                         candidate.DataContext is EditablePdfPage))
        {
            var topLeft = card.TranslatePoint(new Point(), this);
            if (topLeft is null)
            {
                continue;
            }

            var bounds = new Rect(topLeft.Value, card.Bounds.Size);
            var horizontalDistance = pointer.X < bounds.Left
                ? bounds.Left - pointer.X
                : pointer.X > bounds.Right ? pointer.X - bounds.Right : 0;
            var verticalDistance = pointer.Y < bounds.Top
                ? bounds.Top - pointer.Y
                : pointer.Y > bounds.Bottom ? pointer.Y - bounds.Bottom : 0;
            var distanceSquared = (horizontalDistance * horizontalDistance) +
                                  (verticalDistance * verticalDistance);
            if (distanceSquared < nearestDistanceSquared)
            {
                nearest = card;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest;
    }

    private void ClearInsertionIndicator()
    {
        if (dropTarget is not null)
        {
            dropTarget.IsDropBefore = false;
            dropTarget.IsDropAfter = false;
        }

        dropTarget = null;
        dropAfter = false;
    }

    private void ShowDragHint(string text)
    {
        dragHintText.Text = text;
        dragHint.IsVisible = true;
    }

    private void FinishPageDrag()
    {
        ClearInsertionIndicator();
        dragHint.IsVisible = false;
        foreach (var page in movingPages)
        {
            page.IsDragging = false;
        }

        movingPages = [];
        pressedPage = null;
        collapseSelectionOnRelease = false;
        pageDragActive = false;
    }

    private bool IsOverPageArea(Point pointer)
    {
        var topLeft = pageScrollViewer.TranslatePoint(new Point(), this);
        return viewModel.HasDocument && topLeft is not null &&
               new Rect(topLeft.Value, pageScrollViewer.Bounds.Size).Contains(pointer);
    }

    private void Window_OnDragOver(object? sender, DragEventArgs e)
    {
        if (!HasPdf(e.DataTransfer))
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        var point = e.GetPosition(this);
        if (IsOverPageArea(point))
        {
            UpdateInsertionIndicator(point);
            ShowDragHint("水色のラインの位置にPDFを挿入");
        }
        else
        {
            ClearInsertionIndicator();
            dragHint.IsVisible = false;
        }

        e.Handled = true;
    }

    private void Window_OnDragLeave(object? sender, DragEventArgs e)
    {
        ClearInsertionIndicator();
        dragHint.IsVisible = false;
    }

    private void Window_OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles()?
            .Select(item => item.TryGetLocalPath())
            .Where(path => path is not null && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .ToArray() ?? [];
        var insertionIndex = IsOverPageArea(e.GetPosition(this)) ? GetInsertionIndex() : null;
        ClearInsertionIndicator();
        dragHint.IsVisible = false;

        if (files.Length > 0)
        {
            if (insertionIndex is not null)
            {
                var request = new FileInsertRequest(files, insertionIndex.Value);
                if (viewModel.InsertFilesCommand.CanExecute(request))
                {
                    viewModel.InsertFilesCommand.Execute(request);
                }
            }
            else if (viewModel.AcceptFilesCommand.CanExecute(files))
            {
                viewModel.AcceptFilesCommand.Execute(files);
            }
        }

        e.Handled = true;
    }

    private static bool HasPdf(IDataTransfer data) =>
        data.TryGetFiles()?.Any(item =>
            string.Equals(Path.GetExtension(item.TryGetLocalPath()), ".pdf", StringComparison.OrdinalIgnoreCase)) == true;
}
