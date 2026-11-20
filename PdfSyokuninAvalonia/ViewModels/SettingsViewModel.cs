using CommunityToolkit.Mvvm.ComponentModel;
using PdfSyokunin.Localization;
using System.Reflection;
using System.Text;

namespace PdfSyokunin.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly LanguageStore languageStore;
    private readonly string version =
        typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? string.Empty;

    public SettingsViewModel(LanguageStore? languageStore = null)
    {
        this.languageStore = languageStore ?? new LanguageStore();
        Loc.Instance.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(VersionText));
            OnPropertyChanged(nameof(SelectedLanguage));
        };
    }

    public IReadOnlyList<LanguageOption> Languages => Loc.Languages;

    public LanguageOption SelectedLanguage
    {
        get => Loc.Instance.CurrentLanguage;
        set
        {
            if (value is null || value == Loc.Instance.CurrentLanguage)
            {
                return;
            }

            Loc.Instance.SetLanguage(value.Code);
            languageStore.Save(value.Code);
        }
    }

    public string VersionText => Loc.Format("Settings_Version", version);

    public string LibraryDescription => "Avalonia (MIT)・PDFsharp (MIT)・PdfPig (Apache-2.0)・PDFtoImage (MIT)";

    public string ThirdPartyNotices { get; } = LoadThirdPartyNotices();

    private static string LoadThirdPartyNotices()
    {
        var assembly = typeof(SettingsViewModel).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("THIRD-PARTY-NOTICES.txt", StringComparison.Ordinal));
        if (resourceName is null)
        {
            return Loc.Get("Settings_LicenseLoadFailed");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
