using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace YFrame.Pack.Services;

/// <summary>
/// 将 YFrame Release 输出目录打成一个版本发布包。
/// 排除根目录下的 Config、Log、plugins，输出到指定目录的 YF-{Version}.zip。
/// </summary>
public static class ReleasePackageService
{
    private static readonly Regex VersionRegex = new(
        @"^\d+(?:\.\d+){1,3}$", RegexOptions.Compiled);

    private static readonly HashSet<string> ExcludedRootDirectories = new(
        new[] { "Config", "Log", "plugins" }, StringComparer.OrdinalIgnoreCase);

    /// <summary>读取 Version.props 中的 Version 节点。</summary>
    /// <param name="versionPropsPath">Version.props 路径</param>
    /// <returns>版本号文本</returns>
    public static string ReadVersion(string versionPropsPath)
    {
        if (!File.Exists(versionPropsPath))
            throw new FileNotFoundException("Version.props 不存在。", versionPropsPath);

        XDocument document = XDocument.Load(versionPropsPath);
        string? version = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Version")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(version) || !VersionRegex.IsMatch(version))
            throw new InvalidDataException("Version.props 中的 Version 格式不正确。");

        return version;
    }

    /// <summary>压缩 Release 目录并生成版本发布包。</summary>
    /// <param name="sourceDirectory">Release 输出目录</param>
    /// <param name="outputDirectory">ZIP 输出目录</param>
    /// <param name="versionPropsPath">Version.props 路径</param>
    /// <param name="log">日志回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>打包结果</returns>
    public static async Task<ReleasePackageResult> BuildAsync(
        string sourceDirectory,
        string outputDirectory,
        string versionPropsPath,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Release 目录不存在：{sourceDirectory}");
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("发布包输出目录不能为空。", nameof(outputDirectory));

        string version = ReadVersion(versionPropsPath);
        string outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);
        string outputPath = Path.Combine(outputRoot, $"YF-{version}.zip");
        string tempPath = outputPath + ".tmp";

        log?.Invoke($"版本号：{version}");
        log?.Invoke($"源目录：{sourceDirectory}");
        log?.Invoke($"输出文件：{outputPath}");

        var files = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Where(file => !IsExcluded(sourceDirectory, file, outputRoot))
            .ToList();
        long totalBytes = files.Sum(file => new FileInfo(file).Length);
        long writtenBytes = 0;

        if (File.Exists(tempPath))
            File.Delete(tempPath);

        try
        {
            using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
            {
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relativePath = Path.GetRelativePath(sourceDirectory, file)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    ZipArchiveEntry entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                    entry.LastWriteTime = File.GetLastWriteTime(file);

                    using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using Stream target = entry.Open();
                    byte[] buffer = new byte[1024 * 1024];
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        writtenBytes += read;
                        if (writtenBytes % (16 * 1024 * 1024) < read)
                        {
                            log?.Invoke($"压缩进度：{writtenBytes * 100.0 / Math.Max(1, totalBytes):0.0}%");
                        }
                    }
                }
            }

            File.Move(tempPath, outputPath, overwrite: true);
            long archiveLength = new FileInfo(outputPath).Length;
            log?.Invoke($"打包完成：{files.Count} 个文件，{archiveLength / 1024.0 / 1024.0:0.00} MB");

            return new ReleasePackageResult
            {
                Version = version,
                OutputPath = outputPath,
                FileCount = files.Count,
                SourceBytes = totalBytes,
                ArchiveBytes = archiveLength
            };
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* 保留原始异常 */ }
            }
            throw;
        }
    }

    /// <summary>判断文件是否属于应当排除的根目录或输出目录。</summary>
    /// <param name="sourceDirectory">源根目录</param>
    /// <param name="file">文件路径</param>
    /// <param name="outputRoot">输出根目录</param>
    /// <returns>是否排除</returns>
    private static bool IsExcluded(string sourceDirectory, string file, string outputRoot)
    {
        string relative = Path.GetRelativePath(sourceDirectory, file);
        string firstSegment = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

        if (ExcludedRootDirectories.Contains(firstSegment))
            return true;

        // 防止输出目录位于源目录内时把正在生成的压缩包打进去
        string fullFile = Path.GetFullPath(file);
        return fullFile.StartsWith(outputRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>发布包打包结果。</summary>
public sealed class ReleasePackageResult
{
    /// <summary>Version.props 中的版本号。</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>生成的 ZIP 完整路径。</summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>压缩文件数量。</summary>
    public int FileCount { get; init; }

    /// <summary>压缩前总字节数。</summary>
    public long SourceBytes { get; init; }

    /// <summary>压缩后总字节数。</summary>
    public long ArchiveBytes { get; init; }
}
