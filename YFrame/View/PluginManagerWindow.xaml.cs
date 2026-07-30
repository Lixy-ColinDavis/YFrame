using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using YF_Manager;

namespace YFrame.View
{
    /// <summary>
    /// 插件管理器窗口，用于连接插件服务器并下载/管理插件
    /// </summary>
    public partial class PluginManagerWindow : Window
    {
        #region 远程插件信息模型

        /// <summary>
        /// 远程插件信息模型，用于绑定UI列表
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

        #endregion

        #region JSON响应模型

        private class PluginListResponse
        {
            public int Count { get; set; }
            public List<PluginItemResponse> Plugins { get; set; } = new();
        }

        private class PluginItemResponse
        {
            public string PluginId { get; set; } = "";
            public string PluginName { get; set; } = "";
            public string FolderName { get; set; } = "";
            public long TotalSize { get; set; }
            public string FileSizeDisplay { get; set; } = "";
            public int FileCount { get; set; }
        }

        #endregion

        /// <summary>
        /// 远程插件列表数据源
        /// </summary>
        public ObservableCollection<RemotePluginInfo> RemotePlugins { get; } = new();

        /// <summary>
        /// HTTP客户端
        /// </summary>
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private bool _isConnected;

        /// <summary>
        /// 本地插件目录路径
        /// </summary>
        private readonly string _pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

        /// <summary>
        /// 从语言资源获取翻译字符串的辅助方法
        /// </summary>
        private string R(string key) => Application.Current?.TryFindResource(key) as string ?? key;

        /// <summary>
        /// 带格式参数的资源字符串
        /// </summary>
        private string RF(string key, params object[] args)
        {
            var fmt = Application.Current?.TryFindResource(key) as string ?? key;
            return string.Format(fmt, args);
        }

        public PluginManagerWindow()
        {
            InitializeComponent();
            itemsControl.ItemsSource = RemotePlugins;

            // 从配置文件加载上次使用的服务器地址，自动拼接端口
            var savedUrl = Config.PluginManagerServerURL;
            var savedPort = Config.PluginServerPort;
            txtServerURL.Text = BuildFullUrl(savedUrl, savedPort);

            // 初始状态显示
            txtStatus.Text = R("key_PluginManager_NotConnected");
            txtEmpty.Text = R("key_PluginManager_Empty");
        }

        /// <summary>
        /// 将地址和端口拼接为完整URL（地址不含端口时自动追加）
        /// </summary>
        private static string BuildFullUrl(string baseUrl, string port)
        {
            if (string.IsNullOrEmpty(baseUrl))
                return $"http://127.0.0.1:{port}";

            // 如果地址已经包含端口，直接返回
            var uri = new UriBuilder(baseUrl);
            if (uri.Port == 80 && baseUrl.Contains(":") == false)
            {
                // 地址没有指定端口（默认80），追加配置的端口
                uri.Port = int.TryParse(port, out var p) ? p : 9000;
                return uri.Uri.AbsoluteUri.TrimEnd('/');
            }
            return baseUrl;
        }

        /// <summary>
        /// 连接按钮：向服务器请求插件列表
        /// </summary>
        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            var url = txtServerURL.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                SetStatus(R("key_PluginManager_EnterAddress"), Brushes.Orange);
                return;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "http://" + url;
                txtServerURL.Text = url;
            }

            // 持久化服务器地址到配置文件
            Config.PluginManagerServerURL = url;

            url = url.TrimEnd('/');
            var apiUrl = $"{url}/api/plugins";

            SetStatus(R("key_PluginManager_Connecting"), Brushes.DodgerBlue);
            SetButtonsEnabled(false);

            try
            {
                var response = await _httpClient.GetAsync(apiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    SetStatus(RF("key_PluginManager_ConnectFailed", $"HTTP {(int)response.StatusCode}"), Brushes.Red);
                    SetButtonsEnabled(true);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var pluginListResponse = JsonSerializer.Deserialize<PluginListResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (pluginListResponse == null)
                {
                    SetStatus(RF("key_PluginManager_ConnectFailed", "JSON Parse Error"), Brushes.Red);
                    SetButtonsEnabled(true);
                    return;
                }

                RemotePlugins.Clear();
                foreach (var item in pluginListResponse.Plugins)
                {
                    var info = new RemotePluginInfo
                    {
                        PluginId = item.PluginId,
                        PluginName = item.PluginName,
                        FolderName = item.FolderName,
                        TotalSize = item.TotalSize,
                        FileCount = item.FileCount,
                        IsInstalled = Directory.Exists(Path.Combine(_pluginsDir, item.FolderName))
                    };
                    RemotePlugins.Add(info);
                }

                _isConnected = true;
                txtEmpty.Visibility = RemotePlugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                SetStatus(RF("key_PluginManager_Connected", RemotePlugins.Count), Brushes.Green);
                txtPluginCount.Text = RF("key_PluginManager_AvailablePlugins", RemotePlugins.Count);
            }
            catch (HttpRequestException ex)
            {
                SetStatus(RF("key_PluginManager_ConnectFailed", ex.Message), Brushes.Red);
            }
            catch (TaskCanceledException)
            {
                SetStatus(R("key_PluginManager_Timeout"), Brushes.Red);
            }
            catch (Exception ex)
            {
                SetStatus(RF("key_PluginManager_Error", ex.Message), Brushes.Red);
            }
            finally
            {
                SetButtonsEnabled(!_isConnected);
            }
        }

        /// <summary>
        /// 断开按钮：取消下载并清空列表
        /// </summary>
        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            foreach (var plugin in RemotePlugins)
            {
                plugin.Cts?.Cancel();
                plugin.Cts?.Dispose();
                plugin.Cts = null;
                plugin.IsDownloading = false;
            }

            RemotePlugins.Clear();
            _isConnected = false;
            txtEmpty.Visibility = Visibility.Visible;
            SetStatus(R("key_PluginManager_NotConnected"), Brushes.Gray);
            txtPluginCount.Text = "";
            txtDownloadStatus.Text = "";
            SetButtonsEnabled(true);
        }

        /// <summary>
        /// 下载按钮：从服务器流式下载插件ZIP并解压到plugins目录
        /// </summary>
        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.Tag is not RemotePluginInfo pluginInfo)
                return;

            var url = txtServerURL.Text.Trim().TrimEnd('/');
            var downloadUrl = $"{url}/api/plugins/{pluginInfo.FolderName}/download";

            pluginInfo.IsDownloading = true;
            pluginInfo.DownloadProgress = 0;
            pluginInfo.Cts = new CancellationTokenSource();
            txtDownloadStatus.Text = RF("key_PluginManager_Downloading", pluginInfo.PluginName);

            try
            {
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, pluginInfo.Cts.Token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var tempZip = Path.Combine(Path.GetTempPath(), $"{pluginInfo.FolderName}_{Guid.NewGuid():N}.zip");
                var pluginDir = Path.Combine(_pluginsDir, pluginInfo.FolderName);

                // 流式写入临时ZIP文件，同时更新下载进度
                using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var stream = await response.Content.ReadAsStreamAsync(pluginInfo.Cts.Token))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, pluginInfo.Cts.Token)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, bytesRead, pluginInfo.Cts.Token);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progress = (int)(totalRead * 100 / totalBytes);
                            await Dispatcher.InvokeAsync(() => pluginInfo.DownloadProgress = progress);
                        }
                    }
                }

                // 下载完成，解压安装
                await Dispatcher.InvokeAsync(() =>
                {
                    txtDownloadStatus.Text = RF("key_PluginManager_Installing", pluginInfo.PluginName);
                });

                if (Directory.Exists(pluginDir))
                    Directory.Delete(pluginDir, true);
                Directory.CreateDirectory(pluginDir);

                ZipFile.ExtractToDirectory(tempZip, pluginDir);

                try { File.Delete(tempZip); } catch { }

                await Dispatcher.InvokeAsync(() =>
                {
                    pluginInfo.IsDownloading = false;
                    pluginInfo.IsInstalled = true;
                    pluginInfo.DownloadProgress = 100;
                    txtDownloadStatus.Text = RF("key_PluginManager_InstallComplete", pluginInfo.PluginName);
                });
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    pluginInfo.IsDownloading = false;
                    pluginInfo.DownloadProgress = 0;
                    txtDownloadStatus.Text = RF("key_PluginManager_DownloadCancelled", pluginInfo.PluginName);
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    pluginInfo.IsDownloading = false;
                    pluginInfo.DownloadProgress = 0;
                    txtDownloadStatus.Text = RF("key_PluginManager_DownloadFailed", ex.Message);
                    MessageBox.Show(
                        RF("key_PluginManager_DownloadFailed", ex.Message),
                        RF("key_PluginManager_Title", ""),
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                pluginInfo.Cts?.Dispose();
                pluginInfo.Cts = null;
            }
        }

        /// <summary>
        /// 更新连接状态显示
        /// </summary>
        private void SetStatus(string text, Brush color)
        {
            txtStatus.Text = text;
            statusDot.Fill = color;
        }

        /// <summary>
        /// 设置连接/断开按钮的启用状态
        /// </summary>
        private void SetButtonsEnabled(bool canConnect)
        {
            btnConnect.IsEnabled = canConnect;
            btnDisconnect.IsEnabled = !canConnect;
        }
    }
}
