using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace PdfSyokunin.Localization;

/// <summary>表示言語の1項目。コンボボックスには母語での名前を出す。</summary>
public sealed record LanguageOption(string Code, string NativeName, string FontFamily)
{
    public override string ToString() => NativeName;
}

/// <summary>
/// 画面の文言を言語ごとのJSON（埋め込みリソース）から引く。
/// 画面側は {DynamicResource キー} で参照し、言語を切り替えるとアプリのリソースを書き換えて即座に反映する。
/// コード側は <see cref="Get"/> / <see cref="Format"/> を使い、<see cref="LanguageChanged"/> で表示を更新する。
/// </summary>
public sealed partial class Loc : ObservableObject
{
    public const string DefaultCode = "ja";

    // 和文のファイル名も崩れないよう、どの言語でも Yu Gothic UI を後ろに残す。
    public static IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("ja", "日本語", "Inter, Yu Gothic UI, Segoe UI"),
        new("en", "English", "Inter, Segoe UI, Yu Gothic UI"),
        new("th", "ไทย", "Inter, Leelawadee UI, Segoe UI, Yu Gothic UI"),
        new("es", "Español", "Inter, Segoe UI, Yu Gothic UI"),
        new("zh", "简体中文", "Inter, Microsoft YaHei UI, Segoe UI, Yu Gothic UI")
    ];

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Cache = [];
    private IReadOnlyDictionary<string, string> current;
    private readonly IReadOnlyDictionary<string, string> fallback;
    private ResourceDictionary? languageDictionary;

    private Loc()
    {
        fallback = LoadStrings(DefaultCode);
        current = fallback;
        CurrentLanguage = Languages[0];
    }

    public static Loc Instance { get; } = new();

    public event EventHandler? LanguageChanged;

    [ObservableProperty]
    private LanguageOption currentLanguage = null!;

    /// <summary>言語を切り替えるたびに増える。書式付きバインディングの再評価に使う。</summary>
    [ObservableProperty]
    private int version;

    public static string Get(string key) =>
        Instance.current.TryGetValue(key, out var value) ? value
        : Instance.fallback.TryGetValue(key, out var japanese) ? japanese
        : key;

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(PluralKey(key, args)), args);

    /// <summary>
    /// 英語やスペイン語の「1 page / 2 pages」に対応する。最初の整数が1で、いまの言語に「キー_One」があればそれを使う。
    /// 単数と複数を区別しない言語（日本語・タイ語・中国語）は「_One」を持たないので、そのままのキーになる。
    /// </summary>
    private static string PluralKey(string key, object?[] args)
    {
        if (args.OfType<int>().Take(1).SingleOrDefault(-1) != 1)
        {
            return key;
        }

        var singular = key + SingularSuffix;
        return Instance.current.ContainsKey(singular) ? singular : key;
    }

    public const string SingularSuffix = "_One";

    public void SetLanguage(string? code)
    {
        var option = Find(code);
        current = option.Code == DefaultCode ? fallback : LoadStrings(option.Code);

        var culture = CultureInfo.GetCultureInfo(option.Code == "zh" ? "zh-CN" : option.Code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        ApplyToApplicationResources(option);
        CurrentLanguage = option;
        Version++;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public static LanguageOption Find(string? code) =>
        Languages.FirstOrDefault(language => string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? Languages[0];

    /// <summary>保存された設定がないとき、Windowsの表示言語に合わせる。対応外なら英語にする。</summary>
    public static string DetectSystemLanguage()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Languages.Any(language => language.Code == code) ? code : "en";
    }

    /// <summary>テスト用：言語ごとのキーと文言をそのまま返す。</summary>
    public static IReadOnlyDictionary<string, string> LoadStrings(string code)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(code, out var cached))
            {
                return cached;
            }

            var assembly = typeof(Loc).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith($".Localization.{code}.json", StringComparison.Ordinal));
            IReadOnlyDictionary<string, string> strings = new Dictionary<string, string>();
            if (resourceName is not null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName)!;
                strings = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
            }

            Cache[code] = strings;
            return strings;
        }
    }

    private void ApplyToApplicationResources(LanguageOption option)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        // 1件ずつ書き換えると画面の再評価が文言の数だけ走るため、言語ごとの辞書を丸ごと差し替える。
        // 訳がないキーは日本語で埋め、画面に空欄を作らない。
        var dictionary = new ResourceDictionary();
        foreach (var (key, japanese) in fallback)
        {
            dictionary[key] = current.TryGetValue(key, out var value) ? value : japanese;
        }

        dictionary["AppFontFamily"] = new FontFamily(option.FontFamily);

        if (languageDictionary is not null)
        {
            resources.MergedDictionaries.Remove(languageDictionary);
        }

        resources.MergedDictionaries.Add(dictionary);
        languageDictionary = dictionary;
    }
}
