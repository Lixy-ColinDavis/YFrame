using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using YFrame.upload.cos.Models;

namespace YFrame.upload.cos.Services;

/// <summary>
/// 插件压缩、清单生成和 COS 上传服务。
/// </summary>
public sealed class PluginPackService
{
    /// <summary>扫描插件目录，生成待压缩项。</summary>
    /// <param name="sourceRoot">插件根目录</param>
    /// <param name="outputRoot">ZIP 输出目录</param>
    /// <param name="baseUrl">COS 公开根地址</param>
    /// <returns>插件项列表</returns>
    public IReadOnlyList<PluginPackageItem> Scan(
        string sourceRoot,
        string outputRoot,
        string baseUrl)
    {
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException($"插件目录不存在：{sourceRoot}");

        var result = new List<PluginPackageItem>();
        foreach (string directory in Directory.GetDirectories(sourceRoot)
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            string folderName = Path.GetFileName(directory);
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            long sourceLength = files.Sum(file => new FileInfo(file).Length);
            string zipName = folderName + ".zip";
            result.Add(new PluginPackageItem
            {
                FolderName = folderName,
                SourcePath = directory,
                SourceLength = sourceLength,
                ZipPath = Path.Combine(outputRoot, zipName),
                Url = BuildUrl(baseUrl, zipName)
            });
        }

        return result;
    }

    /// <summary>逐个压缩插件目录，并生成清单。</summary>
    /// <param name="packages">插件项</param>
    /// <param name="outputRoot">ZIP 输出目录</param>
    /// <param name="manifestFileName">清单文件名</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本地清单路径</returns>
    public async Task<string> PackageAsync(
        IReadOnlyList<PluginPackageItem> packages,
        string outputRoot,
        string manifestFileName,
        IProgress<PluginPackProgress> progress,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputRoot);
        long totalBytes = packages.Sum(item => item.SourceLength);
        long completedBytes = 0;

        foreach (PluginPackageItem package in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            package.Status = "压缩中";
            string tempZip = package.ZipPath + ".tmp";
            try
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);

                using (var output = new FileStream(tempZip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
                {
                    string[] files = Directory.GetFiles(package.SourcePath, "*", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string entryName = Path.GetRelativePath(package.SourcePath, file)
                            .Replace(Path.DirectorySeparatorChar, '/');
                        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                        entry.LastWriteTime = File.GetLastWriteTime(file);

                        using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using Stream target = entry.Open();
                        byte[] buffer = new byte[1024 * 1024];
                        int read;
                        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                        {
                            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                            completedBytes += read;
                            progress.Report(new PluginPackProgress
                            {
                                PluginName = package.FolderName,
                                Message = $"正在压缩 {package.FolderName}",
                                Percent = totalBytes <= 0 ? 0 : completedBytes * 100.0 / totalBytes
                            });
                        }
                    }
                }

                File.Move(tempZip, package.ZipPath, overwrite: true);
                package.ZipLength = new FileInfo(package.ZipPath).Length;
                package.Status = "已压缩";
            }
            catch
            {
                if (File.Exists(tempZip))
                {
                    try { File.Delete(tempZip); } catch { /* 清理失败不覆盖原始异常 */ }
                }
                throw;
            }
        }

        // 清单最后生成，ZIP 大小已经确定
        string manifestPath = Path.Combine(outputRoot, manifestFileName);
        var manifest = new PluginManifest
        {
            Count = packages.Count,
            Plugins = packages.Select(item => new PluginManifestItem
            {
                Name = item.FolderName,
                FileName = item.FolderName + ".zip",
                Url = item.Url,
                Size = item.ZipLength
            }).ToList()
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, options),
            new UTF8Encoding(false),
            cancellationToken);

        progress.Report(new PluginPackProgress
        {
            PluginName = "清单",
            Message = "插件清单已生成",
            Percent = 100
        });
        return manifestPath;
    }

    /// <summary>上传插件 ZIP 和清单到 COS，清单放最后。</summary>
    /// <param name="credentials">COS 凭证</param>
    /// <param name="bucket">存储桶</param>
    /// <param name="region">地域</param>
    /// <param name="packages">插件项</param>
    /// <param name="manifestPath">本地清单路径</param>
    /// <param name="remotePrefix">COS 对象前缀</param>
    /// <param name="uploadService">COS 服务</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task UploadAsync(
        CosCredentials credentials,
        string bucket,
        string region,
        IReadOnlyList<PluginPackageItem> packages,
        string manifestPath,
        string remotePrefix,
        CosUploadService uploadService,
        IProgress<UploadProgressInfo> progress,
        CancellationToken cancellationToken = default)
    {
        string prefix = remotePrefix.Trim('/');
        var files = packages.Select(item => new CosUploadFile
        {
            Name = item.FolderName + ".zip",
            LocalPath = item.ZipPath,
            ObjectKey = $"{prefix}/{item.FolderName}.zip",
            Length = item.ZipLength
        }).ToList();

        files.Add(new CosUploadFile
        {
            Name = Path.GetFileName(manifestPath),
            LocalPath = manifestPath,
            ObjectKey = $"{prefix}/{Path.GetFileName(manifestPath)}",
            Length = new FileInfo(manifestPath).Length
        });

        return uploadService.UploadObjectsAsync(
            credentials, bucket, region, files, progress, cancellationToken);
    }

    /// <summary>读取本地插件清单。</summary>
    /// <param name="manifestPath">清单路径</param>
    /// <returns>插件清单</returns>
    public PluginManifest LoadManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("找不到插件清单，请先执行压缩。", manifestPath);

        string json = File.ReadAllText(manifestPath, Encoding.UTF8);
        return JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("插件清单格式错误。");
    }

    /// <summary>上传已经压缩好的插件 ZIP 和清单，不再执行压缩。</summary>
    /// <param name="credentials">COS 凭证</param>
    /// <param name="bucket">存储桶</param>
    /// <param name="region">地域</param>
    /// <param name="manifestPath">本地清单路径</param>
    /// <param name="remotePrefix">插件 ZIP 的 COS 对象前缀</param>
    /// <param name="manifestRemotePrefix">plugins.json 的 COS 对象前缀</param>
    /// <param name="uploadService">COS 服务</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task UploadManifestAsync(
        CosCredentials credentials,
        string bucket,
        string region,
        string manifestPath,
        string remotePrefix,
        string manifestRemotePrefix,
        CosUploadService uploadService,
        IProgress<UploadProgressInfo> progress,
        CancellationToken cancellationToken = default)
    {
        PluginManifest manifest = LoadManifest(manifestPath);
        if (manifest.Plugins.Count == 0)
            throw new InvalidDataException("插件清单中没有可上传的插件。");

        string directory = Path.GetDirectoryName(manifestPath)!;
        string prefix = remotePrefix.Trim('/');
        string manifestPrefix = manifestRemotePrefix.Trim('/');
        var files = new List<CosUploadFile>();

        foreach (PluginManifestItem item in manifest.Plugins)
        {
            string fileName = Path.GetFileName(item.FileName);
            string localPath = Path.Combine(directory, fileName);
            if (!File.Exists(localPath))
                throw new FileNotFoundException($"清单对应的 ZIP 不存在：{fileName}", localPath);

            files.Add(new CosUploadFile
            {
                Name = fileName,
                LocalPath = localPath,
                ObjectKey = $"{prefix}/{fileName}",
                Length = new FileInfo(localPath).Length
            });
        }

        // 清单独占最后一个上传位置，且使用独立目录 YFrame/plugins
        files.Add(new CosUploadFile
        {
            Name = Path.GetFileName(manifestPath),
            LocalPath = manifestPath,
            ObjectKey = $"{manifestPrefix}/{Path.GetFileName(manifestPath)}",
            Length = new FileInfo(manifestPath).Length
        });

        return uploadService.UploadObjectsAsync(
            credentials, bucket, region, files, progress, cancellationToken);
    }

    /// <summary>拼接公开访问 URL。</summary>
    /// <param name="baseUrl">根地址</param>
    /// <param name="fileName">文件名</param>
    /// <returns>完整 URL</returns>
    private static string BuildUrl(string baseUrl, string fileName)
        => $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(fileName)}";
}

/// <summary>插件压缩进度。</summary>
public sealed class PluginPackProgress
{
    /// <summary>当前插件名。</summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>状态文本。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>整体百分比。</summary>
    public double Percent { get; init; }
}
