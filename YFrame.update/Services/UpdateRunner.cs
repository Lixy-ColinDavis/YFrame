using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using YF_Manager;
using YFrame.Common;
using YFrame.Update.Models;

namespace YFrame.Update.Services;

/// <summary>
/// 自动更新执行器：读取远端清单、下载增量包或全量包、校验 SHA-256、备份、替换、删除并重启主程序。
/// </summary>
public sealed class UpdateRunner
{
    private readonly string _appRoot;
    private readonly string _manifestUrl;
    private readonly string _environment;
    private readonly UpdateLogger _logger;

    /// <summary>更新器正式文件名。</summary>
    private const string UpdaterFileName = "YFrame.update.exe";

    /// <summary>新更新器的待接替文件名，由 YFrame 下次启动时替换正式文件。</summary>
    private const string PendingUpdaterFileName = "YFrame.update.new.exe";

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    /// <summary>
    /// 创建更新执行器。
    /// </summary>
    /// <param name="appRoot">YFrame 主程序目录；默认为 update.exe 所在目录</param>
    /// <param name="manifestUrl">版本清单地址；为空时使用共享默认 UAT 地址</param>
    /// <param name="logger">更新日志器；为空时自动创建</param>
    /// <param name="environment">签名环境 uat/pdt；为空时使用 uat</param>
    public UpdateRunner(string? appRoot = null, string? manifestUrl = null,
        UpdateLogger? logger = null, string? environment = null)
    {
        _appRoot = Path.GetFullPath(appRoot ?? AppContext.BaseDirectory);
        _manifestUrl = string.IsNullOrWhiteSpace(manifestUrl)
            ? Cos_UpdateDefaults.ManifestUrl(Cos_UpdateDefaults.DefaultEnv)
            : manifestUrl;
        _environment = Cos_UpdateDefaults.NormalizeEnv(environment);
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

        if (!IsVersionOlder(localVersion, current))
        {
            throw new InvalidOperationException(
                $"本地版本 {localVersion} 高于远端版本 {current}，不执行降级更新。");
        }

        bool isPrevious = string.Equals(localVersion, previous, StringComparison.OrdinalIgnoreCase);
        bool isFullUpdate;
        PackageInfo package;

        // 相邻版本优先用增量包；增量包缺失时回退全量包
        if (isPrevious && HasUsablePackage(manifest.Previous))
        {
            isFullUpdate = false;
            package = manifest.Previous;
        }
        else if (HasUsablePackage(manifest.Current))
        {
            isFullUpdate = true;
            package = manifest.Current;
        }
        else
        {
            throw new InvalidDataException(isPrevious
                ? "远端清单缺少可用的增量包和全量包。"
                : $"本地版本 {localVersion} 不是上一版本，且远端清单缺少可用的全量包。");
        }

        string packageKind = isFullUpdate ? "全量包" : "增量包";
        string packageFileName = isFullUpdate ? "current.zip" : "increment.zip";
        _logger.Info($"准备下载{packageKind}；url={package.Url}；size={package.Size}；sha256={package.Sha256}");

        string tempDirectory = Path.Combine(Path.GetTempPath(),
            "YFrame_update_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string downloadPath = Path.Combine(tempDirectory, packageFileName);
        string stagingDirectory = Path.Combine(tempDirectory, "staging");

        try
        {
            Report(progress, 5, $"下载{packageKind}", package.Url);
            await DownloadAsync(package.Url, package.Size, downloadPath, progress, cancellationToken);
            _logger.Info($"{packageKind}下载完成；path={downloadPath}；size={new FileInfo(downloadPath).Length}");

            Report(progress, 58, $"校验{packageKind}", "正在校验文件大小和 SHA-256...");
            VerifyPackage(downloadPath, package);
            _logger.Info($"{packageKind}大小和 SHA-256 校验通过。");

            Report(progress, 64, $"解压{packageKind}", "正在检查文件路径...");
            var changedFiles = ExtractToStaging(downloadPath, stagingDirectory);
            _logger.Info($"{packageKind}解压完成；文件数={changedFiles.Count}；staging={stagingDirectory}");

            // 更新器自身不能在运行时直接覆盖；先落盘为 .new，由下次 YFrame 启动时接替
            bool containsUpdater = changedFiles.RemoveAll(path =>
                string.Equals(path, UpdaterFileName, StringComparison.OrdinalIgnoreCase)) > 0;
            if (containsUpdater)
            {
                Report(progress, 67, "准备新更新器", "检测到新版本更新器，正在写入待替换文件...");
                CopyPendingUpdater(stagingDirectory);
                _logger.Info($"新更新器已写入 {PendingUpdaterFileName}，等待 YFrame 下次启动接替。");
            }

            // 全量包以完整文件树为准；Config/Log/plugins/Backup 等用户数据不覆盖、不清理
            List<string> filesToReplace = changedFiles;
            List<string> filesToRemove = manifest.Removed;
            if (isFullUpdate)
            {
                filesToReplace = changedFiles
                    .Where(path => !IsProtectedFullUpdatePath(path))
                    .ToList();

                filesToRemove = manifest.Removed
                    .Concat(CollectObsoleteFiles(changedFiles))
                    .Where(path => !IsProtectedFullUpdatePath(path) && !IsUpdaterFile(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _logger.Info($"全量更新范围；替换={filesToReplace.Count}；清理旧文件={filesToRemove.Count}；" +
                             $"跳过用户目录/更新器");
            }

            Report(progress, 70, "等待主程序退出", "正在等待 YFrame 关闭以释放文件...");
            await WaitForMainAppExitAsync(progress, cancellationToken);
            _logger.Info("YFrame 主进程已退出，可以替换文件。");

            Report(progress, 74, "备份原文件", "正在备份将被替换或删除的文件...");
            string backupDirectory = BackupFiles(filesToReplace, filesToRemove, current);
            _logger.Info($"备份完成；目录={backupDirectory}；待替换={filesToReplace.Count}；待删除={filesToRemove.Count}");

            Report(progress, 82, "替换文件", $"正在更新 {filesToReplace.Count} 个文件...");
            ReplaceFiles(stagingDirectory, filesToReplace);
            _logger.Info($"文件替换完成；数量={filesToReplace.Count}");

            Report(progress, 92, "删除旧文件", $"正在处理 {filesToRemove.Count} 个已删除文件...");
            DeleteRemovedFiles(filesToRemove);
            _logger.Info($"已删除文件处理完成；数量={filesToRemove.Count}");

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

    /// <summary>下载并验证远端版本清单签名，再反序列化。</summary>
    private async Task<RemoteUpdateManifest> ReadManifestAsync(CancellationToken cancellationToken)
    {
        using var manifestResponse = await Client.GetAsync(_manifestUrl, cancellationToken);
        manifestResponse.EnsureSuccessStatusCode();
        byte[] manifestBytes = await manifestResponse.Content.ReadAsByteArrayAsync(cancellationToken);

        using var signatureResponse = await Client.GetAsync(_manifestUrl + ".sig", cancellationToken);
        signatureResponse.EnsureSuccessStatusCode();
        byte[] signatureBytes = await signatureResponse.Content.ReadAsByteArrayAsync(cancellationToken);

        string publicKey = UpdateTrustKeys.GetPublicKey(_environment);
        if (!YF_ManifestSigner.Verify(manifestBytes, signatureBytes, publicKey))
            throw new InvalidDataException("更新清单签名校验失败，拒绝更新。");

        var manifest = JsonSerializer.Deserialize<RemoteUpdateManifest>(manifestBytes, JsonOptions);
        if (manifest == null)
            throw new InvalidDataException("远端版本清单为空或格式错误。");

        _logger.Info($"更新清单签名校验通过；环境={_environment}；keyId={UpdateTrustKeys.GetKeyId(_environment)}");
        return manifest;
    }

    /// <summary>带进度地下载更新包。</summary>
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
                Report(progress, percent, "下载更新包", $"{FormatBytes(received)} / {FormatBytes(total)}");
                lastReport = DateTime.UtcNow;
            }
        }

        Report(progress, 56, "下载完成", FormatBytes(received));
    }

    /// <summary>校验下载文件的大小和 SHA-256。</summary>
    private static void VerifyPackage(string filePath, PackageInfo package)
    {
        long actualSize = new FileInfo(filePath).Length;
        if (actualSize != package.Size)
            throw new InvalidDataException($"更新包大小不匹配：期望 {package.Size}，实际 {actualSize}。");

        string actualSha256 = YF_Sha256Hasher.ComputeFile(filePath);
        if (!string.Equals(actualSha256, package.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"更新包 SHA-256 不匹配：期望 {package.Sha256}，实际 {actualSha256}。");
    }

    /// <summary>把更新包解压到临时 staging，并返回所有相对文件路径。</summary>
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

    /// <summary>
    /// 将 staging 中的新更新器写到根目录的待接替文件，绝不覆盖正在运行的更新器。
    /// </summary>
    /// <param name="stagingDirectory">更新包解压目录</param>
    private void CopyPendingUpdater(string stagingDirectory)
    {
        string source = SafeCombine(stagingDirectory, UpdaterFileName);
        string pendingPath = SafeCombine(_appRoot, PendingUpdaterFileName);
        File.Copy(source, pendingPath, overwrite: true);
        _logger.Info($"已写入待接替更新器：{pendingPath}");
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
            // 更新器不能在自身运行时被删除；保留给 YFrame 启动时接替
            if (IsUpdaterFile(relativePath))
            {
                _logger.Warn($"忽略对更新器文件的删除：{relativePath}");
                continue;
            }

            string target = SafeCombine(_appRoot, NormalizeRelativePath(relativePath));
            if (File.Exists(target))
                File.Delete(target);
        }
    }

    /// <summary>判断路径是否为正式/待接替更新器文件。</summary>
    private static bool IsUpdaterFile(string relativePath)
    {
        string path = relativePath.Replace('\\', '/').TrimStart('/');
        return string.Equals(path, UpdaterFileName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, PendingUpdaterFileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>把 ZIP 条目路径转换为安全的相对路径。</summary>
    private static string NormalizeRelativePath(string rawPath)
    {
        string path = rawPath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException("更新包包含空文件路径。");

        foreach (string segment in path.Split('/'))
        {
            if (segment == "..")
                throw new InvalidDataException($"更新包包含非法路径：{rawPath}");
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

    /// <summary>判断本地版本是否低于远端版本；无法解析时返回 false，避免误升级。</summary>
    private static bool IsVersionOlder(string local, string current)
    {
        return Version.TryParse(local, out Version? localVersion) &&
               Version.TryParse(current, out Version? currentVersion) &&
               localVersion < currentVersion;
    }

    /// <summary>判断发布包信息是否完整可用。</summary>
    private static bool HasUsablePackage(PackageInfo? package)
    {
        return package != null &&
               !string.IsNullOrWhiteSpace(package.Url) &&
               package.Size > 0 &&
               !string.IsNullOrWhiteSpace(package.Sha256);
    }

    /// <summary>
    /// 判断全量更新中必须保留的路径：用户配置、日志、插件、备份和工具本体。
    /// 这些路径不覆盖、不清理，避免更新破坏用户数据或删除工具。
    /// </summary>
    private static bool IsProtectedFullUpdatePath(string relativePath)
    {
        string path = relativePath.Replace('\\', '/').TrimStart('/');
        if (IsUpdaterFile(path))
            return true;

        string[] protectedDirectories = { "Config", "Log", "plugins", "Backup", "File" };
        foreach (string directory in protectedDirectories)
        {
            if (path.Equals(directory, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return path.Equals("YFrame.Pack.exe", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("YFrame.Pack.config.json", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("YFrame.Installer.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 全量更新时找出当前目录中不属于新完整包、且属于框架管理的旧文件。
    /// 只清理根目录框架文件和 runtimes/Common 目录，避免误删用户文件。
    /// </summary>
    private List<string> CollectObsoleteFiles(IEnumerable<string> packageFiles)
    {
        var packageSet = new HashSet<string>(
            packageFiles.Select(NormalizeRelativePath),
            StringComparer.OrdinalIgnoreCase);

        var obsolete = new List<string>();
        foreach (string file in Directory.EnumerateFiles(_appRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(_appRoot, file).Replace('\\', '/');
            if (packageSet.Contains(relativePath) ||
                IsProtectedFullUpdatePath(relativePath) ||
                IsUpdaterFile(relativePath))
                continue;

            if (IsManagedObsoleteFile(relativePath))
                obsolete.Add(relativePath);
        }

        return obsolete;
    }

    /// <summary>判断旧文件是否属于框架管理的文件类型或目录。</summary>
    private static bool IsManagedObsoleteFile(string relativePath)
    {
        string path = relativePath.Replace('\\', '/');
        string firstSegment = path.Split('/')[0];
        if (firstSegment.Equals("runtimes", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("Common", StringComparison.OrdinalIgnoreCase))
            return true;

        // 嵌套的用户目录一律不动，只清理根目录框架文件
        if (path.Contains('/'))
            return false;

        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".dll" or ".exe" or ".json" or ".config" or ".pdb" or ".xml" or ".dat" or ".pak";
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
