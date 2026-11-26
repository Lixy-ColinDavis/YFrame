using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using YF_Manager;
using YFrame.Pack.Models;

namespace YFrame.Pack.Services;

/// <summary>
/// 对比两个 YF-*.*.*.zip，输出增量包和 YF_Version.json。
/// 只比较文件内容，不比较压缩包内时间戳；路径按 Windows 习惯忽略大小写。
/// </summary>
public static class PackService
{
    private static readonly Regex VersionFileRegex = new(
        @"^YF-(?<version>\d+(?:\.\d+){1,3})\.zip$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// 执行一次增量包构建。
    /// </summary>
    /// <param name="previousZipPath">旧版本完整包路径，例如 YF-2.0.0.zip</param>
    /// <param name="currentZipPath">新版本完整包路径，例如 YF-2.0.1.zip</param>
    /// <param name="outputDirectory">current.zip、increment.zip 和 YF_Version.json 的输出目录</param>
    /// <param name="currentUrl">current.zip 的固定下载地址</param>
    /// <param name="incrementUrl">increment.zip 的固定下载地址</param>
    /// <param name="log">进度日志回调，可为空</param>
    /// <returns>构建结果</returns>
    public static PackResult Build(
        string previousZipPath,
        string currentZipPath,
        string outputDirectory,
        string currentUrl,
        string incrementUrl,
        Action<string>? log = null)
    {
        ValidateInputs(previousZipPath, currentZipPath, outputDirectory, currentUrl, incrementUrl);

        string previousVersion = ParseVersionFromFileName(previousZipPath);
        string currentVersion = ParseVersionFromFileName(currentZipPath);
        var previousVersionValue = Version.Parse(previousVersion);
        var currentVersionValue = Version.Parse(currentVersion);
        if (currentVersionValue <= previousVersionValue)
            throw new InvalidOperationException(
                $"新版本 {currentVersion} 必须高于旧版本 {previousVersion}。");

        Directory.CreateDirectory(outputDirectory);
        string incrementPath = Path.Combine(outputDirectory, "increment.zip");
        string currentCopyPath = Path.Combine(outputDirectory, "current.zip");
        string manifestPath = Path.Combine(outputDirectory, "YF_Version.json");

        log?.Invoke($"旧版本：{previousVersion}  {Path.GetFileName(previousZipPath)}");
        log?.Invoke($"新版本：{currentVersion}  {Path.GetFileName(currentZipPath)}");
        log?.Invoke("正在读取两个压缩包并计算文件哈希...");

        using var previousArchive = ZipFile.OpenRead(previousZipPath);
        using var currentArchive = ZipFile.OpenRead(currentZipPath);

        var previousFiles = ReadFileEntries(previousArchive, "旧版本", log);
        var currentFiles = ReadFileEntries(currentArchive, "新版本", log);

        var added = new List<string>();
        var changed = new List<string>();
        var removed = new List<string>();

        foreach (var pair in currentFiles)
        {
            if (!previousFiles.TryGetValue(pair.Key, out var oldFile))
            {
                added.Add(pair.Key);
            }
            else if (oldFile.Length != pair.Value.Length ||
                     !string.Equals(oldFile.Sha256, pair.Value.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                changed.Add(pair.Key);
            }
        }

        foreach (string path in previousFiles.Keys)
        {
            if (!currentFiles.ContainsKey(path))
                removed.Add(path);
        }

        added.Sort(StringComparer.OrdinalIgnoreCase);
        changed.Sort(StringComparer.OrdinalIgnoreCase);
        removed.Sort(StringComparer.OrdinalIgnoreCase);

        log?.Invoke($"差异统计：新增 {added.Count}，修改 {changed.Count}，删除 {removed.Count}。");
        log?.Invoke("正在写入增量包...");

        var changedPaths = added.Concat(changed)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        WriteIncrementArchive(currentFiles, changedPaths, incrementPath);

        // 把最新完整包复制到输出目录，发布端可直接按固定文件名 current.zip 上传
        if (!string.Equals(Path.GetFullPath(currentZipPath), Path.GetFullPath(currentCopyPath),
                StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(currentZipPath, currentCopyPath, overwrite: true);
        }
        else if (!File.Exists(currentCopyPath))
        {
            File.Copy(currentZipPath, currentCopyPath, overwrite: true);
        }

        long incrementSize = new FileInfo(incrementPath).Length;
        string incrementSha256 = YF_Sha256Hasher.ComputeFile(incrementPath);
        long currentSize = new FileInfo(currentCopyPath).Length;
        string currentSha256 = YF_Sha256Hasher.ComputeFile(currentCopyPath);

        var manifest = new VersionManifest
        {
            Product = "YFrame",
            CurrentVersion = currentVersion,
            PreviousVersion = previousVersion,
            UpdateTime = DateTime.Now.ToString("yyyy-MM-dd'T'HH-mm-ss", CultureInfo.InvariantCulture),
            Current = new PackageInfo
            {
                Url = currentUrl.Trim(),
                Size = currentSize,
                Sha256 = currentSha256
            },
            Previous = new PackageInfo
            {
                Url = incrementUrl.Trim(),
                Size = incrementSize,
                Sha256 = incrementSha256
            },
            Removed = removed
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, jsonOptions), new UTF8Encoding(false));

        log?.Invoke($"已复制最新完整包：{Path.GetFileName(currentCopyPath)}（{FormatBytes(currentSize)}）");
        log?.Invoke($"已生成增量包：{Path.GetFileName(incrementPath)}（{FormatBytes(incrementSize)}）");
        log?.Invoke($"已生成清单：{Path.GetFileName(manifestPath)}");
        log?.Invoke($"current SHA-256：{currentSha256}");
        log?.Invoke($"increment SHA-256：{incrementSha256}");

        return new PackResult
        {
            PreviousVersion = previousVersion,
            CurrentVersion = currentVersion,
            AddedFiles = added,
            ChangedFiles = changed,
            RemovedFiles = removed,
            IncrementPath = incrementPath,
            CurrentCopyPath = currentCopyPath,
            ManifestPath = manifestPath,
            IncrementSize = incrementSize,
            IncrementSha256 = incrementSha256,
            CurrentSize = currentSize,
            CurrentSha256 = currentSha256
        };
    }

    /// <summary>把新版本中新增和修改的文件写入增量 ZIP。</summary>
    private static void WriteIncrementArchive(
        Dictionary<string, FileEntry> currentFiles,
        List<string> changedPaths,
        string incrementPath)
    {
        if (File.Exists(incrementPath))
            File.Delete(incrementPath);

        using var outputStream = new FileStream(incrementPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var incrementArchive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: false);

        foreach (string path in changedPaths)
        {
            var sourceInfo = currentFiles[path];
            var outputEntry = incrementArchive.CreateEntry(path, CompressionLevel.Optimal);

            // 尽量保留源文件的 ZIP 时间戳；异常时间戳回退为当前时间
            try
            {
                outputEntry.LastWriteTime = sourceInfo.Entry.LastWriteTime;
            }
            catch
            {
                outputEntry.LastWriteTime = DateTimeOffset.Now;
            }

            using var sourceStream = sourceInfo.Entry.Open();
            using var targetStream = outputEntry.Open();
            sourceStream.CopyTo(targetStream);
        }
    }

    /// <summary>读取 ZIP 内所有文件，建立“规范化路径 → 文件信息”的字典。</summary>
    private static Dictionary<string, FileEntry> ReadFileEntries(
        ZipArchive archive,
        string archiveLabel,
        Action<string>? log)
    {
        var result = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
        int index = 0;

        foreach (var entry in archive.Entries)
        {
            // 目录条目的 Name 为空，不参与文件差异
            if (string.IsNullOrEmpty(entry.Name)) continue;

            string path = NormalizeEntryPath(entry.FullName);
            if (string.IsNullOrEmpty(path)) continue;
            if (result.ContainsKey(path))
                throw new InvalidDataException($"{archiveLabel}压缩包存在重复文件路径：{path}");

            using var stream = entry.Open();
            string sha256 = YF_Sha256Hasher.ComputeStream(stream);
            result[path] = new FileEntry
            {
                Entry = entry,
                Length = entry.Length,
                Sha256 = sha256
            };

            index++;
            if (index % 500 == 0)
                log?.Invoke($"  {archiveLabel}已读取 {index} 个文件...");
        }

        log?.Invoke($"  {archiveLabel}共 {result.Count} 个文件。");
        return result;
    }

    /// <summary>
    /// 将 ZIP 条目路径统一为 / 分隔，并阻止 .. 路径穿越。
    /// </summary>
    private static string NormalizeEntryPath(string rawPath)
    {
        string path = rawPath.Replace('\\', '/').TrimStart('/');
        if (path.Length == 0) return string.Empty;

        foreach (string segment in path.Split('/'))
        {
            if (segment == "..")
                throw new InvalidDataException($"压缩包包含非法路径：{rawPath}");
        }

        return path;
    }

    /// <summary>从 YF-x.y.z.zip 文件名中提取版本号。</summary>
    private static string ParseVersionFromFileName(string zipPath)
    {
        string fileName = Path.GetFileName(zipPath);
        Match match = VersionFileRegex.Match(fileName);
        if (!match.Success)
            throw new InvalidOperationException(
                $"文件名不符合 YF-*.*.*.zip 规则：{fileName}。示例：YF-2.0.0.zip。");

        return match.Groups["version"].Value;
    }

    /// <summary>校验构建参数，尽早给出可读错误。</summary>
    private static void ValidateInputs(
        string previousZipPath,
        string currentZipPath,
        string outputDirectory,
        string currentUrl,
        string incrementUrl)
    {
        if (string.IsNullOrWhiteSpace(previousZipPath) || !File.Exists(previousZipPath))
            throw new FileNotFoundException("未找到旧版本压缩包。", previousZipPath);
        if (string.IsNullOrWhiteSpace(currentZipPath) || !File.Exists(currentZipPath))
            throw new FileNotFoundException("未找到新版本压缩包。", currentZipPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("输出目录不能为空。", nameof(outputDirectory));
        if (string.IsNullOrWhiteSpace(currentUrl))
            throw new ArgumentException("current 下载地址不能为空。", nameof(currentUrl));
        if (string.IsNullOrWhiteSpace(incrementUrl))
            throw new ArgumentException("increment 下载地址不能为空。", nameof(incrementUrl));
    }

    /// <summary>把字节数格式化为易读大小。</summary>
    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

    /// <summary>ZIP 内单个文件的比较信息。</summary>
    private sealed class FileEntry
    {
        public ZipArchiveEntry Entry { get; init; } = null!;
        public long Length { get; init; }
        public string Sha256 { get; init; } = string.Empty;
    }
}

/// <summary>增量构建结果，供界面展示和后续操作使用。</summary>
public sealed class PackResult
{
    public string PreviousVersion { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public List<string> AddedFiles { get; init; } = new();
    public List<string> ChangedFiles { get; init; } = new();
    public List<string> RemovedFiles { get; init; } = new();
    public string IncrementPath { get; init; } = string.Empty;
    public string CurrentCopyPath { get; init; } = string.Empty;
    public string ManifestPath { get; init; } = string.Empty;
    public long IncrementSize { get; init; }
    public string IncrementSha256 { get; init; } = string.Empty;
    public long CurrentSize { get; init; }
    public string CurrentSha256 { get; init; } = string.Empty;

    /// <summary>新增和修改文件总数。</summary>
    public int ChangedTotal => AddedFiles.Count + ChangedFiles.Count;
}
