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
            var extra = strings.Keys.Except(japanese.Keys).ToArray();
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
        }
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

    private static object? Resource(string key) =>
        Application.Current!.TryGetResource(key, null, out var value) ? value : null;

    private static string Placeholders(string text) =>
        string.Join(",", PlaceholderPattern().Matches(text).Select(match => match.Value).Order());

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();
}
