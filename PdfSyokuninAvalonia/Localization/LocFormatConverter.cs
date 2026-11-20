using Avalonia.Data.Converters;
using System.Globalization;

namespace PdfSyokunin.Localization;

/// <summary>
/// 「{0}ページ目」のような数値入りの文言を、言語切り替えにも追従して表示する。
/// 1つ目の値を書式に埋め込み、2つ目以降（Loc.Version）は再評価のきっかけとしてだけ使う。
/// 使い方: MultiBinding Converter=LocFormatConverter, ConverterParameter=キー
/// </summary>
public sealed class LocFormatConverter : IMultiValueConverter
{
    public static LocFormatConverter Instance { get; } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        parameter is string key ? Loc.Format(key, values.Count > 0 ? values[0] : null) : null;
}
