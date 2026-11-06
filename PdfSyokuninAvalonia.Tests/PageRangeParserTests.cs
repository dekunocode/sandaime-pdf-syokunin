using PdfSyokunin.Utilities;

namespace PdfSyokunin.Tests;

[TestClass]
public sealed class PageRangeParserTests
{
    [TestMethod]
    public void Parse_範囲と個別ページをゼロ始まりへ変換する()
    {
        var actual = PageRangeParser.Parse("1-3, 5", 6);

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 4 }, actual.ToArray());
    }

    [TestMethod]
    public void Parse_重複を除きページ順に並べる()
    {
        var actual = PageRangeParser.Parse("3, 1-3", 4);

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, actual.ToArray());
    }

    [TestMethod]
    public void Parse_範囲外は例外にする()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => PageRangeParser.Parse("1-5", 4));
    }
}
