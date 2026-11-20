namespace PdfSyokunin.Localization;

/// <summary>
/// 選んだ表示言語を覚えておく。最近使ったファイルの settings.json は「履歴を消去」で削除されるため、別ファイルにする。
/// </summary>
public sealed class LanguageStore(string? path = null)
{
    private readonly string path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PDF職人",
        "language.txt");

    public string? Load()
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(string code)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, code);
        }
        catch (IOException)
        {
            // 言語設定を保存できなくても、今回の表示切り替えはそのまま使える。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
