using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSyokunin;
using PdfSyokunin.ViewModels;

namespace PdfSyokunin.Screenshot;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var outputPath = Path.GetFullPath(args.Length > 0 ? args[0] : "screenshot.png");

        // UseHeadlessDrawing=false で、本物の描画エンジン（Skia）を使う。
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .SetupWithoutStarting();

        // 見本の PDF を 2 つ作る（文字は使わず、図形だけで書類らしく見せる）
        var directory = Path.Combine(Path.GetTempPath(), "PdfSyokuninScreenshot");
        Directory.CreateDirectory(directory);
        var first = CreateSamplePdf(Path.Combine(directory, "会議資料.pdf"), 3, XColor.FromArgb(255, 70, 110, 160));
        var second = CreateSamplePdf(Path.Combine(directory, "図面_改訂B.pdf"), 2, XColor.FromArgb(255, 60, 140, 100));

        var window = new MainWindow();
        window.Show();
        var viewModel = (MainViewModel)window.DataContext!;

        // サムネイルは別スレッドで作られるので、終わるまで UI のキューを回す
        Pump(viewModel.AcceptFilesCommand.ExecuteAsync(new[] { first, second }));
        for (var i = 0; i < 40; i++)
        {
            Tick();
            Thread.Sleep(50);
        }

        var frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            Console.Error.WriteLine("画面を描画できませんでした。");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        frame.Save(outputPath);
        Console.WriteLine($"保存しました: {outputPath} ({frame.PixelSize.Width}x{frame.PixelSize.Height})");
        return 0;
    }

    private static void Pump(Task task)
    {
        var timeout = DateTime.UtcNow.AddMinutes(2);
        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > timeout) throw new TimeoutException("PDF の読み込みが終わりませんでした。");
            Tick();
            Thread.Sleep(20);
        }
        task.GetAwaiter().GetResult();
    }

    private static void Tick()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static string CreateSamplePdf(string path, int pageCount, XColor accent)
    {
        using var document = new PdfDocument();
        var lineBrush = new XSolidBrush(XColor.FromArgb(255, 205, 210, 218));
        for (var p = 0; p < pageCount; p++)
        {
            var page = document.AddPage();
            using var g = XGraphics.FromPdfPage(page);
            var width = page.Width.Point;

            // 見出しの帯と本文の行
            g.DrawRectangle(new XSolidBrush(accent), 0, 0, width, 64);
            g.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, 120, 125, 135)), 56, 100, width * 0.45, 16);
            for (var line = 0; line < 16; line++)
            {
                var length = line % 5 == 4 ? width * 0.4 : width - 112;
                g.DrawRectangle(lineBrush, 56, 140 + line * 30, length, 9);
            }

            // ページごとに少し違う図を入れて、サムネイルで見分けがつくようにする
            var boxTop = 640.0;
            if (p % 2 == 0)
            {
                g.DrawRectangle(new XPen(accent, 2), 56, boxTop, width - 112, 140);
                for (var bar = 0; bar < 6; bar++)
                {
                    var barHeight = 20 + (bar * 37 + p * 23) % 100;
                    g.DrawRectangle(new XSolidBrush(accent), 80 + bar * 70, boxTop + 130 - barHeight, 40, barHeight);
                }
            }
            else
            {
                g.DrawEllipse(new XPen(accent, 2), 56, boxTop, 140, 140);
                g.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, 230, 233, 238)), 220, boxTop, width - 276, 140);
            }
        }
        document.Save(path);
        return path;
    }
}
