using Avalonia;
using Avalonia.Headless;

namespace PdfSyokunin.Tests;

[TestClass]
public sealed class TestBootstrap
{
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();
        // Headless初期化で設定されるUI同期コンテキストはテスト用ループを持たないため、
        // PDFサービス内のTask.Runから戻る継続を妨げないよう解除する。
        SynchronizationContext.SetSynchronizationContext(null);
    }
}
