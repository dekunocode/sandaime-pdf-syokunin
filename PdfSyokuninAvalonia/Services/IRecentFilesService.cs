using PdfSyokunin.Models;

namespace PdfSyokunin.Services;

public interface IRecentFilesService
{
    IReadOnlyList<RecentPdfItem> Load();

    void Add(IEnumerable<string> paths);

    void Remove(string path);

    void Clear();
}
