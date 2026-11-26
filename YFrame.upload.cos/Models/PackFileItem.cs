namespace YFrame.upload.cos.Models;

/// <summary>
/// 从 SecretKey.csv 读取的 COS 凭证，仅保存在当前进程内存。
/// </summary>
public sealed class CosCredentials
{
    /// <summary>云 API SecretId。</summary>
    public string SecretId { get; init; } = string.Empty;

    /// <summary>云 API SecretKey。</summary>
    public string SecretKey { get; init; } = string.Empty;
}

/// <summary>
/// Pack 目录中的一个待上传文件。
/// </summary>
public sealed class PackFileItem : ViewModels.ObservableObject
{
    private string _status = "待上传";

    /// <summary>文件名，例如 current.zip。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>本地完整路径。</summary>
    public string FullPath { get; init; } = string.Empty;

    /// <summary>COS 对象键。</summary>
    public string ObjectKey { get; init; } = string.Empty;

    /// <summary>文件大小。</summary>
    public long Length { get; init; }

    /// <summary>文件大小文本。</summary>
    public string SizeText => Length < 1024 * 1024
        ? $"{Length / 1024.0:0.0} KB"
        : $"{Length / 1024.0 / 1024.0:0.00} MB";

    /// <summary>上传状态。</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
}
