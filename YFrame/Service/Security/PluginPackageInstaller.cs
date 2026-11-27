using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using YF_Manager;

namespace YFrame.Service.Security;

/// <summary>
/// 插件 ZIP 的安全解压与原子安装。
/// 所有验证在 staging 完成，失败时恢复旧插件目录。
/// </summary>
public static class PluginPackageInstaller
{
    /// <summary>单个插件解压后的最大文件数。</summary>
    private const int MaxEntryCount = 10000;

    /// <summary>单个插件解压后的最大总字节数。</summary>
    private const long MaxExtractedBytes = 512L * 1024 * 1024;

    /// <summary>
    /// 安装已校验的插件 ZIP。
    /// </summary>
    /// <param name="pluginRoot">plugins 根目录</param>
    /// <param name="folderName">插件目录名</param>
    /// <param name="zipPath">已下载并校验的 ZIP</param>
    /// <param name="zipSha256">ZIP SHA-256</param>
    /// <param name="version">插件版本</param>
    /// <param name="keyId">清单签名 KeyId</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="InvalidDataException">ZIP 结构或路径不安全</exception>
    public static void Install(
        string pluginRoot,
        string folderName,
        string zipPath,
        string zipSha256,
        string version,
        string keyId,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        ValidateFolderName(folderName);
        string pluginDir = Path.Combine(pluginRoot, folderName);
        string stagingRoot = Path.Combine(pluginRoot, ".staging");
        string backupRoot = Path.Combine(pluginRoot, ".backup");
        string stagingDir = Path.Combine(stagingRoot, $"{folderName}-{Guid.NewGuid():N}");
        string backupDir = Path.Combine(backupRoot, $"{folderName}-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stagingDir);
            ExtractSafe(zipPath, stagingDir, cancellationToken);

            string entryAssembly = Path.Combine(stagingDir, folderName + ".dll");
            if (!File.Exists(entryAssembly))
                throw new InvalidDataException($"插件 ZIP 缺少入口程序集：{folderName}.dll");

            string entrySha256 = YF_Sha256Hasher.ComputeFile(entryAssembly);
            var record = new PluginInstallRecord
            {
                Id = folderName,
                Version = version,
                ZipSha256 = zipSha256,
                EntryAssembly = Path.GetFileName(entryAssembly),
                EntrySha256 = entrySha256,
                KeyId = keyId,
                InstalledAt = DateTimeOffset.UtcNow.ToString("O")
            };

            // 先把旧目录移到 backup，再替换；任何替换失败都恢复旧目录
            if (Directory.Exists(pluginDir))
            {
                Directory.CreateDirectory(backupRoot);
                Directory.Move(pluginDir, backupDir);
            }

            try
            {
                Directory.Move(stagingDir, pluginDir);
                WriteRecord(pluginDir, record);
            }
            catch
            {
                TryDeleteDirectory(pluginDir);
                if (Directory.Exists(backupDir))
                    Directory.Move(backupDir, pluginDir);
                throw;
            }

            TryDeleteDirectory(backupDir);
            progress?.Report(100);
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }
    }

    /// <summary>
    /// 安全解压 ZIP：拒绝绝对路径、..、符号链接和越界路径。
    /// </summary>
    /// <param name="zipPath">ZIP 路径</param>
    /// <param name="destinationDir">staging 目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static void ExtractSafe(string zipPath, string destinationDir, CancellationToken cancellationToken)
    {
        string destinationRoot = Path.GetFullPath(destinationDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > MaxEntryCount)
            throw new InvalidDataException("插件 ZIP 文件数量超过限制。");

        long totalBytes = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 目录项只创建目录，不写文件
            string relative = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(relative))
                continue;
            if (Path.IsPathRooted(relative) ||
                relative.Contains(':') ||
                relative.Split('/').Any(part => part == ".."))
                throw new InvalidDataException($"插件 ZIP 包含非法路径：{entry.FullName}");

            // Unix 符号链接通常以 0xA000 标记；Windows 重解析点也拒绝
            if ((entry.ExternalAttributes & 0xF000) == 0xA000)
                throw new InvalidDataException($"插件 ZIP 包含符号链接：{entry.FullName}");

            string targetPath = Path.GetFullPath(Path.Combine(destinationRoot, relative));
            if (!targetPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"插件 ZIP 路径越界：{entry.FullName}");

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            totalBytes += entry.Length;
            if (totalBytes > MaxExtractedBytes)
                throw new InvalidDataException("插件 ZIP 解压后大小超过限制。");

            string? parent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            using Stream source = entry.Open();
            using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
        }
    }

    /// <summary>写入安装记录；先写临时文件再替换。</summary>
    /// <param name="pluginDir">插件目录</param>
    /// <param name="record">安装记录</param>
    private static void WriteRecord(string pluginDir, PluginInstallRecord record)
    {
        string path = Path.Combine(pluginDir, PluginInstallRecord.FileName);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(record, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>校验插件目录名，禁止路径穿越。</summary>
    /// <param name="folderName">插件目录名</param>
    private static void ValidateFolderName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName) ||
            folderName is "." or ".." ||
            !string.Equals(Path.GetFileName(folderName), folderName, StringComparison.Ordinal))
            throw new InvalidDataException($"非法插件文件夹名：{folderName}");
    }

    /// <summary>删除目录，失败时忽略清理异常。</summary>
    /// <param name="path">目录路径</param>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PluginInstaller] 清理失败 {path}: {ex.Message}");
        }
    }
}
