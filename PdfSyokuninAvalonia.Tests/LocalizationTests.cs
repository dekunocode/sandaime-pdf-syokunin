using Avalonia;
using PdfSyokunin.Localization;
using System.Text.RegularExpressions;

namespace PdfSyokunin.Tests;

[TestClass]
public sealed partial class LocalizationTests
{
    [TestCleanup]
    public void TestCleanup() => Loc.Instance.SetLanguage(Loc.DefaultCode);

    [TestMethod]
    public void すべての言語が日本語と同じキーと書式を持つ()
    {
        var japanese = Loc.LoadStrings("ja");
        Assert.IsTrue(japanese.Count > 100, "日本語の文言を読み込めていません。");

        foreach (var language in Loc.Languages)
        {
            var strings = Loc.LoadStrings(language.Code);
            var missing = japanese.Keys.Except(strings.Keys).ToArray();
            // 「キー_One」は単数形を区別する言語だけが持つ追加の文言なので、元のキーがあれば許す。
            var extra = strings.Keys.Except(japanese.Keys)
                .Where(key => !IsSingularOf(key, japanese))
                .ToArray();
            Assert.AreEqual(0, missing.Length, $"{language.Code} に足りないキー: {string.Join(", ", missing)}");
            Assert.AreEqual(0, extra.Length, $"{language.Code} に余分なキー: {string.Join(", ", extra)}");

            foreach (var (key, value) in japanese)
            {
                Assert.AreEqual(
                    Placeholders(value),
                    Placeholders(strings[key]),
                    $"{language.Code} の {key} の {{0}} などの数が日本語と違います。");
                Assert.IsFalse(string.IsNullOrWhiteSpace(strings[key]), $"{language.Code} の {key} が空です。");
            }

            foreach (var key in strings.Keys.Where(key => IsSingularOf(key, japanese)))
            {
                var baseKey = key[..^Loc.SingularSuffix.Length];
                Assert.AreEqual(
                    Placeholders(japanese[baseKey]),
                    Placeholders(strings[key]),
                    $"{language.Code} の {key} の {{0}} などの数が {baseKey} と違います。");
            }
        }
    }

    [TestMethod]
    public void Format_数が1なら単数形の文言を使う()
    {
        Loc.Instance.SetLanguage("en");
        Assert.AreEqual("1 page selected", Loc.Format("Selection_Header", 1));
        Assert.AreEqual("2 pages selected", Loc.Format("Selection_Header", 2));
        Assert.AreEqual("sample.pdf · 1 page", Loc.Format("Save_Summary", "sample.pdf", 1));

        Loc.Instance.SetLanguage("es");
        Assert.AreEqual("1 página seleccionada", Loc.Format("Selection_Header", 1));
        Assert.AreEqual("3 páginas seleccionadas", Loc.Format("Selection_Header", 3));

        Loc.Instance.SetLanguage("ja");
        Assert.AreEqual("1ページを選択中", Loc.Format("Selection_Header", 1));
    }

    [TestMethod]
    public void SetLanguage_文言とアプリのリソースが即座に切り替わる()
    {
        var changed = 0;
        void OnChanged(object? sender, EventArgs e) => changed++;
        Loc.Instance.LanguageChanged += OnChanged;
        try
        {
            Loc.Instance.SetLanguage("en");

            Assert.AreEqual("Cancel", Loc.Get("Common_Cancel"));
            Assert.AreEqual("Page 3", Loc.Format("Page_Number", 3));
            Assert.AreEqual("Cancel", Resource("Common_Cancel"));
            Assert.AreEqual("en", Loc.Instance.CurrentLanguage.Code);

            Loc.Instance.SetLanguage("zh");
            Assert.AreEqual("取消", Resource("Common_Cancel"));
            Assert.AreEqual(2, changed);
        }
        finally
        {
            Loc.Instance.LanguageChanged -= OnChanged;
        }
    }

    [TestMethod]
    public void SetLanguage_対応外の言語は日本語になる()
    {
        Loc.Instance.SetLanguage("xx");

        Assert.AreEqual("ja", Loc.Instance.CurrentLanguage.Code);
        Assert.AreEqual("キャンセル", Loc.Get("Common_Cancel"));
    }

    [TestMethod]
    public void LanguageStore_選んだ言語を保存して読み込める()
    {
        var path = Path.Combine(Path.GetTempPath(), "PdfSyokuninLanguageTests", Guid.NewGuid().ToString("N"), "language.txt");
        var store = new LanguageStore(path);

        Assert.IsNull(store.Load());
        store.Save("th");
        Assert.AreEqual("th", new LanguageStore(path).Load());

        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    private static bool IsSingularOf(string key, IReadOnlyDictionary<string, string> japanese) =>
        key.EndsWith(Loc.SingularSuffix, StringComparison.Ordinal)
        && japanese.ContainsKey(key[..^Loc.SingularSuffix.Length]);

    private static object? Resource(string key) =>
        Application.Current!.TryGetResource(key, null, out var value) ? value : null;

    private static string Placeholders(string text) =>
        string.Join(",", PlaceholderPattern().Matches(text).Select(match => match.Value).Order());

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();
}
