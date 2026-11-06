using System.Reflection;
using System.Text;

namespace PdfSyokunin.ViewModels;

public sealed class SettingsViewModel
{
    public string VersionText { get; } =
        $"バージョン {typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3)}・Avalonia / .NET 10";

    public string LibraryDescription => "Avalonia (MIT)・PDFsharp (MIT)・PdfPig (Apache-2.0)・PDFtoImage (MIT)";

    public string ThirdPartyNotices { get; } = LoadThirdPartyNotices();

    private static string LoadThirdPartyNotices()
    {
        var assembly = typeof(SettingsViewModel).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("THIRD-PARTY-NOTICES.txt", StringComparison.Ordinal));
        if (resourceName is null)
        {
            return "ライセンス情報を読み込めませんでした。";
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
