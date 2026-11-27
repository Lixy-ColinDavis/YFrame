namespace YFrame.upload.cos.Models;

/// <summary>插件压缩包及上传状态。</summary>
public sealed class PluginPackageItem : ViewModels.ObservableObject
{
    private string _status = "待压缩";
    private long _zipLength;
    private string _zipPath = string.Empty;
    private string _url = string.Empty;
    private string _sha256 = string.Empty;
    private string _version = "1.0.0";
    private bool _isSelected = true;

    /// <summary>插件目录名，同时作为 ZIP 文件名。</summary>
    public string FolderName { get; init; } = string.Empty;

    /// <summary>插件目录完整路径。</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>原始文件总大小。</summary>
    public long SourceLength { get; init; }

    /// <summary>ZIP 文件路径。</summary>
    public string ZipPath
    {
        get => _zipPath;
        set => SetProperty(ref _zipPath, value);
    }

    /// <summary>ZIP 文件大小。</summary>
    public long ZipLength
    {
        get => _zipLength;
        set
        {
            if (SetProperty(ref _zipLength, value))
                OnPropertyChanged(nameof(ZipSizeText));
        }
    }

    /// <summary>ZIP 文件 SHA-256。</summary>
    public string Sha256
    {
        get => _sha256;
        set => SetProperty(ref _sha256, value);
    }

    /// <summary>插件版本。</summary>
    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    /// <summary>ZIP 大小文本。</summary>
    public string ZipSizeText => _zipLength <= 0
        ? "-"
        : _zipLength < 1024 * 1024
            ? $"{_zipLength / 1024.0:0.0} KB"
            : $"{_zipLength / 1024.0 / 1024.0:0.00} MB";

    /// <summary>COS 公开地址。</summary>
    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    /// <summary>处理状态。</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>是否勾选参与本次压缩和上传。</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
