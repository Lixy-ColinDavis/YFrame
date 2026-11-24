using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YF_Manager;
using YF_Model = YFrame.Model;

namespace YFrame.Service
{
    /// <summary>
    /// 插件管理器服务：与插件服务器 HTTP 通信、下载并安装插件
    /// </summary>
    public class PluginManagerService
    {
        #region AOP 单例

        /// <summary>
        /// 单例 + AOP 日志拦截代理
        /// </summary>
        private static readonly Lazy<PluginManagerService> _instance = new Lazy<PluginManagerService>(
            () => new ProxyGenerator().CreateClassProxy<PluginManagerService>(new LogInterceptor())
        );

        public static PluginManagerService Instance => _instance.Value;
        private static readonly YF_Manager_Log _logger = new YF_Manager_Log("插件管理器", "PluginManager");
        #endregion

        #region JSON 响应模型

        private class PluginListResponse
        {
            public int Count { get; set; }
            public List<PluginItemResponse> Plugins { get; set; } = new();
        }

        private class PluginItemResponse
        {
            public string PluginId { get; set; } = "";      // 插件ID
            public string PluginName { get; set; } = "";    // 插件名称
            public string FolderName { get; set; } = "";    // 路径名称
            public long TotalSize { get; set; }             // 整体大小
            public int FileCount { get; set; }              // 文件数量
        }

        private class PluginManifestResponse
        {
            public List<PluginManifestItemResponse> Plugins { get; set; } = new();
        }

        private class PluginManifestItemResponse
        {
            public string Name { get; set; } = "";
            public string FileName { get; set; } = "";
            public string Url { get; set; } = "";
            public long Size { get; set; }
        }

        #endregion

        #region 成员字段

        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// 本地插件目录路径
        /// </summary>
        private readonly string _pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

        /// <summary>
        /// 缓存最近连接的基地址，供下载时复用
        /// </summary>
        private string _lastBaseUrl = "";

        #endregion

        #region 插件列表获取

        /// <summary>
        /// 从指定服务器获取插件列表
        /// </summary>
        /// <param name="serverUrl">服务器完整地址（含端口）</param>
        /// <returns>插件信息列表</returns>
        [Log(Level = LogLevel.Info, Message = "从服务器获取插件列表")]
        public virtual async Task<List<YF_Model.RemotePluginInfo>> FetchPluginListAsync(string serverUrl)
        {
            var url = serverUrl.TrimEnd('/');
            var apiUrl = $"{url}/api/plugins";

            var response = await _httpClient.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var pluginListResponse = JsonSerializer.Deserialize<PluginListResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("服务端返回数据格式错误");

            // 缓存基地址，供下载复用
            _lastBaseUrl = url;

            var result = new List<YF_Model.RemotePluginInfo>();
            foreach (var item in pluginListResponse.Plugins)
            {
                result.Add(new YF_Model.RemotePluginInfo
                {
                    PluginId = item.PluginId,
                    PluginName = item.PluginName,
                    FolderName = item.FolderName,
                    TotalSize = item.TotalSize,
                    FileCount = item.FileCount,
                    IsInstalled = Directory.Exists(Path.Combine(_pluginsDir, item.FolderName))
                });
            }

            return result;
        }

        /// <summary>
        /// 从 HTTPS/COS 配置清单读取插件列表。
        /// 清单中的 url 是每个插件 ZIP 的实际下载地址。
        /// </summary>
        /// <param name="manifestUrl">plugins.json 地址</param>
        /// <returns>插件信息列表</returns>
        [Log(Level = LogLevel.Info, Message = "从 HTTPS 清单获取插件列表")]
        public virtual async Task<List<YF_Model.RemotePluginInfo>> FetchPluginListFromManifestAsync(string manifestUrl)
        {
            string url = NormalizeUrl(manifestUrl);
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("插件清单地址为空。");

            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var manifest = JsonSerializer.Deserialize<PluginManifestResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("插件清单格式错误。");

            var manifestBase = new Uri(url);
            var result = new List<YF_Model.RemotePluginInfo>();
            foreach (PluginManifestItemResponse item in manifest.Plugins)
            {
                string folderName = string.IsNullOrWhiteSpace(item.Name)
                    ? Path.GetFileNameWithoutExtension(item.FileName)
                    : item.Name.Trim();
                ValidateFolderName(folderName);

                string downloadUrl = ResolveDownloadUrl(manifestBase, item.Url, item.FileName);
                result.Add(new YF_Model.RemotePluginInfo
                {
                    PluginId = folderName,
                    PluginName = folderName,
                    FolderName = folderName,
                    TotalSize = item.Size,
                    // 清单没有文件数量字段，用 -1 显示为 “-”
                    FileCount = -1,
                    DownloadUrl = downloadUrl,
                    IsInstalled = Directory.Exists(Path.Combine(_pluginsDir, folderName))
                });
            }

            return result;
        }

        /// <summary>
        /// 从 TCP 插件服务器下载插件 ZIP 并解压。
        /// </summary>
        /// <param name="folderName">插件文件夹名称</param>
        /// <param name="progress">下载进度</param>
        /// <param name="cancellationToken">取消令牌</param>
        [Log(Level = LogLevel.Info, Message = "下载并安装插件")]
        public virtual async Task InstallPluginAsync(string folderName, IProgress<int> progress, CancellationToken cancellationToken)
        {
            ValidateFolderName(folderName);
            string downloadUrl = $"{_lastBaseUrl}/api/plugins/{folderName}/download";
            await InstallPluginFromUrlAsync(folderName, downloadUrl, progress, cancellationToken);
        }

        /// <summary>
        /// 从清单提供的完整 URL 下载插件 ZIP 并解压。
        /// </summary>
        /// <param name="folderName">插件文件夹名称</param>
        /// <param name="downloadUrl">ZIP 下载地址</param>
        /// <param name="progress">下载进度</param>
        /// <param name="cancellationToken">取消令牌</param>
        [Log(Level = LogLevel.Info, Message = "按地址下载并安装插件")]
        public virtual async Task InstallPluginFromUrlAsync(
            string folderName,
            string downloadUrl,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            ValidateFolderName(folderName);
            if (string.IsNullOrWhiteSpace(downloadUrl))
                throw new InvalidOperationException("插件下载地址为空。");

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var tempZip = Path.Combine(Path.GetTempPath(), $"{folderName}_{Guid.NewGuid():N}.zip");
            var pluginDir = Path.Combine(_pluginsDir, folderName);

            // 流式写入临时文件并报进度
            using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        progress.Report((int)(totalRead * 100 / totalBytes));
                    }
                }
            }

            // 解压安装到目标目录
            if (Directory.Exists(pluginDir))
                Directory.Delete(pluginDir, true);
            Directory.CreateDirectory(pluginDir);

            YF_ZipHelper.Instance.ExtractToDirectory(tempZip, pluginDir);

            // 清理临时文件
            try { File.Delete(tempZip); }
            catch (Exception ex) { _logger.ErrorInfo("InstallPluginFromUrlAsync", "清理临时文件失败" + ex.Message); }
        }

        #endregion

        /// <summary>
        /// 从清单中的 url/fileName 解析完整下载地址。
        /// </summary>
        /// <param name="manifestBase">清单地址</param>
        /// <param name="url">清单 url 字段</param>
        /// <param name="fileName">清单 fileName 字段</param>
        /// <returns>绝对下载地址</returns>
        private static string ResolveDownloadUrl(Uri manifestBase, string url, string fileName)
        {
            string value = string.IsNullOrWhiteSpace(url) ? fileName : url;
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException("插件清单缺少下载地址和文件名。");

            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? absolute))
                return absolute.ToString();

            return new Uri(manifestBase, value).ToString();
        }

        /// <summary>
        /// 校验插件文件夹名，禁止路径穿越。
        /// </summary>
        /// <param name="folderName">插件文件夹名</param>
        private static void ValidateFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName) ||
                folderName is "." or ".." ||
                !string.Equals(Path.GetFileName(folderName), folderName, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"非法插件文件夹名：{folderName}");
            }
        }

        #region URL 构建

        /// <summary>
        /// 将配置中的地址和端口拼接为完整 URL
        /// </summary>
        [Log(Level = LogLevel.Debug, Message = "构建完整 URL")]
        public virtual string BuildFullUrl(string baseUrl, string port)
        {
            if (string.IsNullOrEmpty(baseUrl))
                return $"http://127.0.0.1:{port}";

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
                return baseUrl;

            // 地址未显式指定端口时，补上配置的插件服务器端口
            if (uri.IsDefaultPort)
            {
                var portNum = int.TryParse(port, out var p) ? p : 9000;
                return $"{uri.Scheme}://{uri.Host}:{portNum}{uri.AbsolutePath}".TrimEnd('/');
            }
            return baseUrl;
        }

        /// <summary>
        /// 规范化用户输入的地址（自动补全 http 前缀）
        /// </summary>
        [Log(Level = LogLevel.Debug, Message = "规范化服务器地址")]
        public virtual string NormalizeUrl(string rawUrl)
        {
            var url = rawUrl.Trim();
            if (string.IsNullOrEmpty(url))
                return "";

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "http://" + url;
            }
            return url;
        }

        #endregion
    }
}
