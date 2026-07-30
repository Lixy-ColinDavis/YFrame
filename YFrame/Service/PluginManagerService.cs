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
    /// 插件管理器服务，负责与插件服务器的 HTTP 通信和插件文件下载/安装
    /// </summary>
    public class PluginManagerService
    {
        #region AOP 单例

        /// <summary>
        /// 单例模式 + AOP 日志拦截代理
        /// </summary>
        private static readonly Lazy<PluginManagerService> _instance = new Lazy<PluginManagerService>(
            () => new ProxyGenerator().CreateClassProxy<PluginManagerService>(new LogInterceptor())
        );

        public static PluginManagerService Instance => _instance.Value;

        #endregion

        #region JSON 响应模型

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
            public int FileCount { get; set; }
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

            // 缓存基地址供下载使用
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

        #endregion

        #region 插件下载安装

        /// <summary>
        /// 从服务器流式下载插件 ZIP 并解压到本地 plugins 目录
        /// </summary>
        /// <param name="folderName">插件文件夹名称</param>
        /// <param name="progress">下载进度报告器（0-100）</param>
        /// <param name="cancellationToken">取消令牌</param>
        [Log(Level = LogLevel.Info, Message = "下载并安装插件")]
        public virtual async Task InstallPluginAsync(string folderName, IProgress<int> progress, CancellationToken cancellationToken)
        {
            var downloadUrl = $"{_lastBaseUrl}/api/plugins/{folderName}/download";

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var tempZip = Path.Combine(Path.GetTempPath(), $"{folderName}_{Guid.NewGuid():N}.zip");
            var pluginDir = Path.Combine(_pluginsDir, folderName);

            // 流式写入临时文件并报告进度
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
            try { File.Delete(tempZip); } catch { }
        }

        #endregion

        #region URL 构建

        /// <summary>
        /// 将配置中的地址和端口拼接为完整 URL
        /// </summary>
        [Log(Level = LogLevel.Debug, Message = "构建完整 URL")]
        public virtual string BuildFullUrl(string baseUrl, string port)
        {
            if (string.IsNullOrEmpty(baseUrl))
                return $"http://127.0.0.1:{port}";

            var uri = new UriBuilder(baseUrl);
            if (uri.Port == 80 && !baseUrl.Contains(':'))
            {
                uri.Port = int.TryParse(port, out var p) ? p : 9000;
                return uri.Uri.AbsoluteUri.TrimEnd('/');
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
