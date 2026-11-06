namespace PdfSyokunin.Utilities;

public static class SafeFileWriter
{
    public static void Write(string outputPath, Action<string> writeTemporaryFile)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutputPath)
            ?? throw new ArgumentException("保存先フォルダーを取得できません。", nameof(outputPath));
        Directory.CreateDirectory(directory);

        // 同一ドライブ内の置換を使うため、出力先と同じフォルダーへ一時保存する。
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            writeTemporaryFile(temporaryPath);
            File.Move(temporaryPath, fullOutputPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
