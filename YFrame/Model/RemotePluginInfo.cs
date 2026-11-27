using System.Text.Json.Serialization;
using System.Windows.Media;
using YF_Manager;

namespace YFrame.Model
{
    /// <summary>
    /// 远程插件信息模型，用于插件管理器UI列表绑定
    /// </summary>
    public class RemotePluginInfo : ViewModelBase
    {
        /// <summary>缓存的图标背景画刷，避免每次绑定都重新创建</summary>
        private Brush? _iconBrush;

        /// <summary>图标缩写（与主窗口插件列表保持一致的生成规则）</summary>
        public string IconText => PluginIconHelper.GetText(PluginId, PluginName);

        /// <summary>图标渐变背景（按插件 ID 稳定取色）</summary>
        public Brush IconBrush => _iconBrush ??= PluginIconHelper.GetBrush(PluginId, PluginName);

        private string _pluginId = "";
        public string PluginId
        {
            get => _pluginId;
            set
            {
                if (_pluginId == value) return;

                _pluginId = value;
                // 先刷新图标，最后通知 PluginId，保持属性通知顺序稳定
                _iconBrush = null;
                OnPropertyChanged(nameof(IconText));
                OnPropertyChanged(nameof(IconBrush));
                OnPropertyChanged(nameof(PluginId));
            }
        }

        private string _pluginName = "";
        public string PluginName
        {
            get => _pluginName;
            set
            {
                if (_pluginName == value) return;

                _pluginName = value;
                // ID 为空时图标缩写回退到名称，先刷新图标再通知名称
                OnPropertyChanged(nameof(IconText));
                OnPropertyChanged(nameof(PluginName));
            }
        }

        private string _folderName = "";
        public string FolderName { get => _folderName; set => SetProperty(ref _folderName, value); }

        private string _downloadUrl = "";
        /// <summary>HTTPS 清单中的 ZIP 下载地址；TCP 模式为空。</summary>
        public string DownloadUrl
        {
            get => _downloadUrl;
            set => SetProperty(ref _downloadUrl, value);
        }

        private string _sha256 = "";
        /// <summary>HTTPS 清单声明的 ZIP SHA-256。</summary>
        public string Sha256
        {
            get => _sha256;
            set => SetProperty(ref _sha256, value);
        }

        private string _version = "";
        /// <summary>插件清单声明的版本。</summary>
        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        private string _manifestKeyId = "";
        /// <summary>清单签名 KeyId，用于安装记录。</summary>
        public string ManifestKeyId
        {
            get => _manifestKeyId;
            set => SetProperty(ref _manifestKeyId, value);
        }

        private long _totalSize;
        public long TotalSize
        {
            get => _totalSize;
            set
            {
                if (SetProperty(ref _totalSize, value))
                    OnPropertyChanged(nameof(FileSizeDisplay));
            }
        }

        /// <summary>
        /// 文件大小格式化显示字符串
        /// </summary>
        public string FileSizeDisplay
        {
            get
            {
                if (TotalSize < 1024) return $"{TotalSize} B";
                if (TotalSize < 1024 * 1024) return $"{TotalSize / 1024.0:F1} KB";
                if (TotalSize < 1024 * 1024 * 1024) return $"{TotalSize / (1024.0 * 1024):F1} MB";
                return $"{TotalSize / (1024.0 * 1024 * 1024):F2} GB";
            }
        }

        private int _fileCount;
        public int FileCount
        {
            get => _fileCount;
            set
            {
                if (SetProperty(ref _fileCount, value))
                    OnPropertyChanged(nameof(FileCountDisplay));
            }
        }
        public string FileCountDisplay => FileCount < 0 ? "-" : $"{FileCount} 个文件";

        private int _downloadProgress;
        public int DownloadProgress
        {
            get => _downloadProgress;
            set
            {
                if (SetProperty(ref _downloadProgress, value))
                    OnPropertyChanged(nameof(DownloadProgressText));
            }
        }
        public string DownloadProgressText => $"{DownloadProgress}%";

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                if (SetProperty(ref _isDownloading, value))
                    OnPropertyChanged(nameof(CanDownload));
            }
        }

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (SetProperty(ref _isInstalled, value))
                    OnPropertyChanged(nameof(CanDownload));
            }
        }

        /// <summary>
        /// 下载按钮是否可见（未安装且未下载中时显示）
        /// </summary>
        [JsonIgnore]
        public bool CanDownload => !IsInstalled && !IsDownloading;

        /// <summary>
        /// 用于取消下载的取消令牌
        /// </summary>
        [JsonIgnore]
        public CancellationTokenSource? Cts { get; set; }
    }
}
