using PdfSyokunin.Models;
using PdfSyokunin.Utilities;
using System.Text.Json;

namespace PdfSyokunin.Services;

public sealed class RecentFilesService
    : IRecentFilesService
{
    private const int MaximumItems = 5;
    private readonly string settingsPath;

    public RecentFilesService(string? settingsPath = null)
    {
        this.settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PDF職人",
            "settings.json");
    }

    public IReadOnlyList<RecentPdfItem> Load()
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return [];
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath));
            return (settings?.RecentFiles ?? [])
                .Where(item => File.Exists(item.FullPath))
                .OrderByDescending(item => item.LastOpenedUtc)
                .Take(MaximumItems)
                .ToArray();
        }
        catch (JsonException)
        {
            // 設定破損でアプリ本体を起動不能にしない。次回追加時に正常なJSONへ置き換わる。
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public void Add(IEnumerable<string> paths)
    {
        var now = DateTime.UtcNow;
        var additions = paths
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (additions.Length == 0)
        {
            return;
        }

        var existing = Load()
            .Where(item => !additions.Contains(item.FullPath, StringComparer.OrdinalIgnoreCase));
        var recent = additions
            .Select((path, index) => new RecentPdfItem(path, now.AddTicks(-index)))
            .Concat(existing)
            .Take(MaximumItems)
            .ToArray();
        Save(new AppSettings { RecentFiles = [.. recent] });
    }

    public void Remove(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var remaining = Load()
            .Where(item => !string.Equals(item.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (remaining.Length == 0)
        {
            Clear();
            return;
        }

        Save(new AppSettings { RecentFiles = [.. remaining] });
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
        catch (IOException)
        {
            // 履歴消去に失敗してもPDF編集処理には影響させない。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(settingsPath)
                ?? throw new InvalidOperationException("設定フォルダーを取得できません。");
            Directory.CreateDirectory(directory);
            SafeFileWriter.Write(
                settingsPath,
                temporaryPath => File.WriteAllText(
                    temporaryPath,
                    JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })));
        }
        catch (IOException)
        {
            // 最近使ったファイルは補助機能なので、保存不能でも編集を継続する。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class AppSettings
    {
        public List<RecentPdfItem> RecentFiles { get; set; } = [];
    }
}
