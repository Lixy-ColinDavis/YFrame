using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using YFrame.upload.cos.Models;

namespace YFrame.upload.cos.Services;

/// <summary>
/// COS 简单上传服务：按 current → increment → sig → manifest 的顺序上传四个文件。
/// </summary>
public sealed class CosUploadService
{
    /// <summary>上传四个 Pack 文件。</summary>
    /// <param name="credentials">COS 凭证</param>
    /// <param name="bucket">存储桶</param>
    /// <param name="region">地域</param>
    /// <param name="files">待上传文件，顺序必须固定</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task UploadAsync(
        CosCredentials credentials,
        string bucket,
        string region,
        IReadOnlyList<PackFileItem> files,
        IProgress<UploadProgressInfo> progress,
        CancellationToken cancellationToken = default)
    {
        if (files.Count != 4)
            throw new InvalidOperationException("上传文件数量必须为 4。");

        var config = new CosXmlConfig.Builder()
            .SetRegion(region)
            .Build();
        QCloudCredentialProvider provider = new DefaultQCloudCredentialProvider(
            credentials.SecretId, credentials.SecretKey, 600);
        var cosXml = new CosXmlServer(config, provider);

        long totalBytes = files.Sum(file => file.Length);
        long completedBefore = 0;

        for (int i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PackFileItem file = files[i];
            file.Status = "上传中";

            var request = new PutObjectRequest(bucket, file.ObjectKey, file.FullPath);
            long fileIndex = i;
            request.SetCosProgressCallback((completed, total) =>
            {
                double overall = totalBytes <= 0
                    ? 0
                    : (completedBefore + completed) * 100.0 / totalBytes;
                progress.Report(new UploadProgressInfo
                {
                    FileName = file.Name,
                    FileIndex = (int)fileIndex,
                    FileCount = files.Count,
                    OverallPercent = Math.Clamp(overall, 0, 100),
                    Message = $"正在上传 {file.Name}"
                });
            });

            try
            {
                // SDK 调用本身是同步的，放到后台线程避免阻塞 UI
                PutObjectResult result = await Task.Run(
                    () => cosXml.PutObject(request), cancellationToken);

                completedBefore += file.Length;
                file.Status = "成功";
                progress.Report(new UploadProgressInfo
                {
                    FileName = file.Name,
                    FileIndex = i,
                    FileCount = files.Count,
                    OverallPercent = totalBytes <= 0 ? 100 : completedBefore * 100.0 / totalBytes,
                    Message = $"{file.Name} 上传成功"
                });
                _ = result;
            }
            catch (Exception ex)
            {
                file.Status = "失败";
                throw new InvalidOperationException($"上传 {file.Name} 失败：{ex.Message}", ex);
            }
        }
    }
}

/// <summary>上传进度信息。</summary>
public sealed class UploadProgressInfo
{
    /// <summary>当前文件名。</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>当前文件序号，从 0 开始。</summary>
    public int FileIndex { get; init; }

    /// <summary>文件总数。</summary>
    public int FileCount { get; init; }

    /// <summary>整体百分比。</summary>
    public double OverallPercent { get; init; }

    /// <summary>状态文本。</summary>
    public string Message { get; init; } = string.Empty;
}
