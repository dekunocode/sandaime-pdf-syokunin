using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PdfSyokunin.Localization;

namespace PdfSyokunin;

public partial class App : Application
{
    internal IReadOnlyList<string> StartupPdfPaths { get; private set; } = [];

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 画面を作る前に文言をリソースへ入れておく。初回はWindowsの表示言語に合わせる。
        Loc.Instance.SetLanguage(new LanguageStore().Load() ?? Loc.DetectSystemLanguage());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            StartupPdfPaths = desktop.Args?
                .Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
                .Where(File.Exists)
                .Select(Path.GetFullPath)
                .ToArray() ?? [];
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
