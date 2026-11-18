using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using YFrame.Update.Models;

namespace YFrame.Update.Services;

/// <summary>
/// 自动更新执行器：读取远端清单、下载增量包、校验 MD5、备份、替换、删除并重启主程序。
/// </summary>
public sealed class UpdateRunner
{
    /// <summary>远端版本清单固定地址。</summary>
    public const string DefaultManifestUrl =
        "https://yfcos-1256976024.cos.ap-beijing.myqcloud.com/YFrame/update/uat/YF_Version.json";

    private readonly string _appRoot;
    private readonly string _manifestUrl;
    private readonly UpdateLogger _logger;

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    /// <summary>
    /// 创建更新执行器。
    /// </summary>
    /// <param name="appRoot">YFrame 主程序目录；默认为 update.exe 所在目录</param>
    /// <param name="manifestUrl">版本清单地址；默认使用固定地址，便于测试时可覆盖</param>
    /// <param name="logger">更新日志器；为空时自动创建</param>
    public UpdateRunner(string? appRoot = null, string? manifestUrl = null, UpdateLogger? logger = null)
    {
        _appRoot = Path.GetFullPath(appRoot ?? AppContext.BaseDirectory);
        _manifestUrl = string.IsNullOrWhiteSpace(manifestUrl) ? DefaultManifestUrl : manifestUrl;
        _logger = logger ?? new UpdateLogger(_appRoot);
    }

    /// <summary>
    /// 执行完整更新流程。
    /// </summary>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新结果</returns>
    public async Task<UpdateResult> RunAsync(
        IProgress<UpdateProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        Report(progress, 0, "读取版本信息", "正在读取本地版本和远端清单...");
        string localVersion = ReadLocalVersion();
        _logger.Info($"更新器启动；根目录={_appRoot}；本地版本={localVersion}；清单={_manifestUrl}");
        var manifest = await ReadManifestAsync(cancellationToken);
        manifest.Removed ??= new List<string>();

        string current = NormalizeVersion(manifest.CurrentVersion);
        string previous = NormalizeVersion(manifest.PreviousVersion);
        _logger.Info($"远端清单；current_version={current}；pervious_version={previous}；removed={manifest.Removed.Count}");
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(previous))
            throw new InvalidDataException("远端版本清单缺少 current_version 或 pervious_version。");

        if (string.Equals(localVersion, current, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info("本地已是最新版本，无需更新。");
            Report(progress, 100, "已是最新版本", "无需更新，即将启动 YFrame。");
            return new UpdateResult { AlreadyCurrent = true, CurrentVersion = current };
        }

        if (!string.Equals(localVersion, previous, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"本地版本 {localVersion} 既不是最新版 {current}，也不是上一版本 {previous}，无法执行增量更新。");
        }

        var package = manifest.Previous;
        if (package == null || string.IsNullOrWhiteSpace(package.Url))
            throw new InvalidDataException("远端清单缺少 pervious.url。");
        if (package.Size <= 0)
            throw new InvalidDataException("远端清单缺少有效的 pervious.size。");
        if (string.IsNullOrWhiteSpace(package.Md5))
            throw new InvalidDataException("远端清单缺少 pervious.md5。");

        _logger.Info($"准备下载增量包；url={package.Url}；size={package.Size}；md5={package.Md5}");

        string tempDirectory = Path.Combine(Path.GetTempPath(),
            "YFrame_update_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string downloadPath = Path.Combine(tempDirectory, "increment.zip");
        string stagingDirectory = Path.Combine(tempDirectory, "staging");

        try
        {
            Report(progress, 5, "下载增量包", package.Url);
            await DownloadAsync(package.Url, package.Size, downloadPath, progress, cancellationToken);
            _logger.Info($"增量包下载完成；path={downloadPath}；size={new FileInfo(downloadPath).Length}");

            Report(progress, 58, "校验增量包", "正在校验文件大小和 MD5...");
            VerifyPackage(downloadPath, package);
            _logger.Info("增量包大小和 MD5 校验通过。");

            Report(progress, 64, "解压增量包", "正在检查文件路径...");
            var changedFiles = ExtractToStaging(downloadPath, stagingDirectory);
            _logger.Info($"增量包解压完成；文件数={changedFiles.Count}；staging={stagingDirectory}");

            Report(progress, 70, "等待主程序退出", "正在等待 YFrame 关闭以释放文件...");
            await WaitForMainAppExitAsync(progress, cancellationToken);
            _logger.Info("YFrame 主进程已退出，可以替换文件。");

            Report(progress, 74, "备份原文件", "正在备份将被替换或删除的文件...");
            string backupDirectory = BackupFiles(changedFiles, manifest.Removed, current);
            _logger.Info($"备份完成；目录={backupDirectory}；待替换={changedFiles.Count}；待删除={manifest.Removed.Count}");

            Report(progress, 82, "替换文件", $"正在更新 {changedFiles.Count} 个文件...");
            ReplaceFiles(stagingDirectory, changedFiles);
            _logger.Info($"文件替换完成；数量={changedFiles.Count}");

            Report(progress, 92, "删除旧文件", $"正在处理 {manifest.Removed.Count} 个已删除文件...");
            DeleteRemovedFiles(manifest.Removed);
            _logger.Info($"已删除文件处理完成；数量={manifest.Removed.Count}");

            // 更新已成功，删除整个 Backup 目录（含历史残留），避免长期占用磁盘
            Report(progress, 96, "清理备份", "更新成功，正在删除备份目录...");
            CleanupBackupDirectory(backupDirectory);

            Report(progress, 98, "清理临时文件", "更新完成，准备重启 YFrame...");
            _logger.Info($"更新流程完成；目标版本={current}");
            return new UpdateResult
            {
                AlreadyCurrent = false,
                CurrentVersion = current,
                BackupDirectory = string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.Error("更新流程失败", ex);
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
                // 临时目录清理失败不影响已经完成的更新
            }
        }
    }

    /// <summary>
    /// 等待根目录下的 YFrame.exe 退出，避免更新时文件仍被占用。
    /// </summary>
    private async Task WaitForMainAppExitAsync(
        IProgress<UpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        string exePath = Path.Combine(_appRoot, "YFrame.exe");
        for (int i = 0; i < 150; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsProcessRunning(exePath))
                return;

            if (i % 10 == 0)
                Report(progress, 70, "等待主程序退出", $"YFrame 仍在运行，等待释放文件（{i / 10 + 1}s）...");
            await Task.Delay(100, cancellationToken);
        }

        throw new InvalidOperationException("YFrame 仍在运行，无法替换文件。请关闭主程序后重试。");
    }

    /// <summary>判断指定路径的 YFrame 进程是否仍在运行。</summary>
    private static bool IsProcessRunning(string exePath)
    {
        foreach (var process in Process.GetProcessesByName("YFrame"))
        {
            try
            {
                string? path = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(path) ||
                    string.Equals(Path.GetFullPath(path), Path.GetFullPath(exePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // 无法读取模块路径时按同名进程处理，避免误删正在使用的文件
                return true;
            }
            finally
            {
                process.Dispose();
            }
        }
        return false;
    }
    /// <summary>
    /// 更新成功后删除整个 Backup 目录，包括之前版本留下的残留。
    /// 删除失败只记录警告，不影响已经完成的更新。
    /// </summary>
    /// <param name="currentBackupDirectory">本次备份目录，用于日志定位</param>
    private void CleanupBackupDirectory(string currentBackupDirectory)
    {
        string backupRoot = Path.Combine(_appRoot, "Backup");
        try
        {
            if (!Directory.Exists(backupRoot))
            {
                _logger.Info($"备份目录不存在，无需清理；本次备份={currentBackupDirectory}");
                return;
            }

            Directory.Delete(backupRoot, recursive: true);
            _logger.Info($"已删除备份目录：{backupRoot}；本次备份={currentBackupDirectory}");
        }
        catch (Exception ex)
        {
            _logger.Warn($"备份目录清理失败：{backupRoot}；{ex.Message}");
        }
    }

    /// <summary>启动 YFrame 主程序；失败时抛出可读异常。</summary>
    public void StartMainApp()
    {
        string exePath = Path.Combine(_appRoot, "YFrame.exe");
        if (!File.Exists(exePath))
        {
            _logger.Error($"未找到 YFrame.exe：{exePath}");
            throw new FileNotFoundException("未找到 YFrame.exe。", exePath);
        }

        Process.Start(new ProcessStartInfo(exePath)
        {
            WorkingDirectory = _appRoot,
            UseShellExecute = true
        });
        _logger.Info($"已启动 YFrame：{exePath}");
    }

    /// <summary>读取 YFrame.dll 的程序集版本；缺失时回退 YFrame.exe 文件版本。</summary>
    private string ReadLocalVersion()
    {
        try
        {
            string dllPath = Path.Combine(_appRoot, "YFrame.dll");
            if (File.Exists(dllPath))
                return NormalizeVersion(AssemblyName.GetAssemblyName(dllPath).Version?.ToString() ?? string.Empty);

            string exePath = Path.Combine(_appRoot, "YFrame.exe");
            if (File.Exists(exePath))
                return NormalizeVersion(FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? string.Empty);
        }
        catch
        {
            // 回退到 exe 文件版本
            string exePath = Path.Combine(_appRoot, "YFrame.exe");
            if (File.Exists(exePath))
                return NormalizeVersion(FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? string.Empty);
        }

        throw new FileNotFoundException("无法读取本地 YFrame 版本，请确认 YFrame.dll 或 YFrame.exe 存在。");
    }

    /// <summary>下载并反序列化远端版本清单。</summary>
    private async Task<RemoteUpdateManifest> ReadManifestAsync(CancellationToken cancellationToken)
    {
        string json = await Client.GetStringAsync(_manifestUrl, cancellationToken);
        var manifest = JsonSerializer.Deserialize<RemoteUpdateManifest>(json, JsonOptions);
        if (manifest == null)
            throw new InvalidDataException("远端版本清单为空或格式错误。");
        return manifest;
    }

    /// <summary>带进度地下载增量包。</summary>
    private static async Task DownloadAsync(
        string url,
        long expectedSize,
        string destination,
        IProgress<UpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long total = expectedSize > 0 ? expectedSize : response.Content.Headers.ContentLength ?? 0;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[81920];
        long received = 0;
        int read;
        var lastReport = DateTime.UtcNow;

        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;

            if (total > 0 && (DateTime.UtcNow - lastReport).TotalMilliseconds >= 120)
            {
                double percent = 5 + received * 50.0 / total;
                Report(progress, percent, "下载增量包", $"{FormatBytes(received)} / {FormatBytes(total)}");
                lastReport = DateTime.UtcNow;
            }
        }

        Report(progress, 56, "下载完成", FormatBytes(received));
    }

    /// <summary>校验下载文件的大小和 MD5。</summary>
    private static void VerifyPackage(string filePath, PackageInfo package)
    {
        long actualSize = new FileInfo(filePath).Length;
        if (actualSize != package.Size)
            throw new InvalidDataException($"增量包大小不匹配：期望 {package.Size}，实际 {actualSize}。");

        string actualMd5 = ComputeFileMd5(filePath);
        if (!string.Equals(actualMd5, package.Md5.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"增量包 MD5 不匹配：期望 {package.Md5}，实际 {actualMd5}。");
    }

    /// <summary>把增量包解压到临时 staging，并返回所有相对文件路径。</summary>
    private static List<string> ExtractToStaging(string zipPath, string stagingDirectory)
    {
        Directory.CreateDirectory(stagingDirectory);
        var paths = new List<string>();

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;

            string relativePath = NormalizeRelativePath(entry.FullName);
            string targetPath = SafeCombine(stagingDirectory, relativePath);
            string? directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            entry.ExtractToFile(targetPath, overwrite: true);
            paths.Add(relativePath);
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>备份将被替换和删除的文件，返回备份目录。</summary>
    private string BackupFiles(List<string> changedFiles, List<string> removedFiles, string currentVersion)
    {
        string backupRoot = Path.Combine(_appRoot, "Backup",
            $"update_{currentVersion}_{DateTime.Now:yyyyMMdd-HHmmss}");
        int suffix = 1;
        while (Directory.Exists(backupRoot))
            backupRoot = Path.Combine(_appRoot, "Backup",
                $"update_{currentVersion}_{DateTime.Now:yyyyMMdd-HHmmss}-{suffix++}");

        Directory.CreateDirectory(backupRoot);
        var backedUp = new List<string>();
        foreach (string relativePath in changedFiles.Concat(removedFiles)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string source = SafeCombine(_appRoot, relativePath);
            if (!File.Exists(source)) continue;

            string target = SafeCombine(backupRoot, relativePath);
            string? directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.Copy(source, target, overwrite: true);
            backedUp.Add(relativePath);
        }

        File.WriteAllText(Path.Combine(backupRoot, "backup-info.txt"),
            $"version={currentVersion}{Environment.NewLine}" +
            $"time={DateTime.Now:O}{Environment.NewLine}" +
            string.Join(Environment.NewLine, backedUp));
        return backupRoot;
    }

    /// <summary>把 staging 中的文件替换到 YFrame 根目录；先写临时文件再原子替换。</summary>
    private void ReplaceFiles(string stagingDirectory, List<string> relativePaths)
    {
        foreach (string relativePath in relativePaths)
        {
            string source = SafeCombine(stagingDirectory, relativePath);
            string target = SafeCombine(_appRoot, relativePath);
            string? directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryTarget = target + ".yfupdate.tmp";
            File.Copy(source, temporaryTarget, overwrite: true);
            File.Move(temporaryTarget, target, overwrite: true);
        }
    }

    /// <summary>按清单删除新版本中已经移除的文件。</summary>
    private void DeleteRemovedFiles(List<string> removedFiles)
    {
        foreach (string relativePath in removedFiles)
        {
            string target = SafeCombine(_appRoot, NormalizeRelativePath(relativePath));
            if (File.Exists(target))
                File.Delete(target);
        }
    }

    /// <summary>把 ZIP 条目路径转换为安全的相对路径。</summary>
    private static string NormalizeRelativePath(string rawPath)
    {
        string path = rawPath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException("增量包包含空文件路径。");

        foreach (string segment in path.Split('/'))
        {
            if (segment == "..")
                throw new InvalidDataException($"增量包包含非法路径：{rawPath}");
        }

        return path;
    }

    /// <summary>组合路径并确保结果仍位于指定根目录内，防止路径穿越。</summary>
    private static string SafeCombine(string root, string relativePath)
    {
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string combined = Path.GetFullPath(Path.Combine(rootFull, relativePath));
        string prefix = rootFull + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"更新路径越界：{relativePath}");
        return combined;
    }

    /// <summary>计算文件 MD5。</summary>
    private static string ComputeFileMd5(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(MD5.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>归一化为“主.次.修订”，兼容四段版本末尾的 0。</summary>
    private static string NormalizeVersion(string value)
    {
        string text = value.Trim().TrimStart('v', 'V');
        if (Version.TryParse(text, out Version? version))
            return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        return text;
    }

    /// <summary>格式化字节数。</summary>
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

    private static void Report(IProgress<UpdateProgress>? progress, double percent, string status, string detail)
        => progress?.Report(new UpdateProgress(Math.Clamp(percent, 0, 100), status, detail));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
