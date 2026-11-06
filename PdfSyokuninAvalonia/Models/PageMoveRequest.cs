namespace PdfSyokunin.Models;

/// <param name="InsertionIndex">移動前のページ一覧における挿入位置。</param>
public sealed record PageMoveRequest(IReadOnlyList<EditablePdfPage> Pages, int InsertionIndex);
