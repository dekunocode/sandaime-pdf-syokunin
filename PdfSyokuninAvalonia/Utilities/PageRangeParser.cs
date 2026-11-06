namespace PdfSyokunin.Utilities;

public static class PageRangeParser
{
    public static IReadOnlyList<int> Parse(string text, int pageCount)
    {
        if (pageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageCount));
        }

        var pages = new SortedSet<int>();
        foreach (var rawPart in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawPart.Contains('-'))
            {
                var bounds = rawPart.Split('-', StringSplitOptions.TrimEntries);
                if (bounds.Length != 2 || !int.TryParse(bounds[0], out var start) || !int.TryParse(bounds[1], out var end))
                {
                    throw new FormatException($"ページ範囲「{rawPart}」を解釈できません。");
                }

                if (start < 1 || end > pageCount || start > end)
                {
                    throw new ArgumentOutOfRangeException(nameof(text), $"ページ範囲は1～{pageCount}で指定してください。");
                }

                for (var page = start; page <= end; page++)
                {
                    pages.Add(page - 1);
                }
            }
            else
            {
                if (!int.TryParse(rawPart, out var page) || page < 1 || page > pageCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(text), $"ページ番号は1～{pageCount}で指定してください。");
                }

                pages.Add(page - 1);
            }
        }

        if (pages.Count == 0)
        {
            throw new FormatException("抽出するページを指定してください。");
        }

        return pages.ToArray();
    }
}
