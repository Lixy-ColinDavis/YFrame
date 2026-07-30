using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace YFrame.Model
{
    /// <summary>
    /// 远程插件信息模型，用于插件管理器UI列表绑定
    /// </summary>
    public class RemotePluginInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private string _pluginId = "";
        public string PluginId { get => _pluginId; set { _pluginId = value; OnPropertyChanged(); } }

        private string _pluginName = "";
        public string PluginName { get => _pluginName; set { _pluginName = value; OnPropertyChanged(); } }

        private string _folderName = "";
        public string FolderName { get => _folderName; set { _folderName = value; OnPropertyChanged(); } }

        private long _totalSize;
        public long TotalSize { get => _totalSize; set { _totalSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeDisplay)); } }

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
        public int FileCount { get => _fileCount; set { _fileCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileCountDisplay)); } }
        public string FileCountDisplay => $"{FileCount} 个文件";

        private int _downloadProgress;
        public int DownloadProgress { get => _downloadProgress; set { _downloadProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadProgressText)); } }
        public string DownloadProgressText => $"{DownloadProgress}%";

        private bool _isDownloading;
        public bool IsDownloading { get => _isDownloading; set { _isDownloading = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDownload)); } }

        private bool _isInstalled;
        public bool IsInstalled { get => _isInstalled; set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDownload)); } }

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
