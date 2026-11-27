using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YF_Manager;
using YFrame.Service.Security;
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
            public int SchemaVersion { get; set; }
            public string KeyId { get; set; } = "";
            public List<PluginManifestItemResponse> Plugins { get; set; } = new();
        }

        private class PluginManifestItemResponse
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Version { get; set; } = "";
            public string FileName { get; set; } = "";
            public string Url { get; set; } = "";
            public long Size { get; set; }
            public string Sha256 { get; set; } = "";
        }

        #endregion

        #region 成员字段

        private readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            // 签名清单已经固定 URL；禁止自动跟随重定向，避免跳到未受控主机
            AllowAutoRedirect = false
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

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
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? manifestUri) ||
                !string.Equals(manifestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("插件清单必须使用 HTTPS。");

            // 先取原始清单字节和签名；验签通过前不反序列化、不使用任何字段
            using var manifestResponse = await _httpClient.GetAsync(url);
            manifestResponse.EnsureSuccessStatusCode();
            byte[] manifestBytes = await manifestResponse.Content.ReadAsByteArrayAsync();

            using var signatureResponse = await _httpClient.GetAsync(url + ".sig");
            signatureResponse.EnsureSuccessStatusCode();
            byte[] signatureBytes = await signatureResponse.Content.ReadAsByteArrayAsync();

            string keyId = PluginManifestVerifier.Verify(manifestBytes, signatureBytes);
            var manifest = JsonSerializer.Deserialize<PluginManifestResponse>(manifestBytes, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("插件清单格式错误。");

            if (manifest.SchemaVersion != 2)
                throw new InvalidDataException("不支持的插件清单版本。");
            if (!string.Equals(manifest.KeyId, keyId, StringComparison.Ordinal))
                throw new InvalidDataException("插件清单 KeyId 与签名不一致。");

            var result = new List<YF_Model.RemotePluginInfo>();
            foreach (PluginManifestItemResponse item in manifest.Plugins)
            {
                string folderName = string.IsNullOrWhiteSpace(item.Id)
                    ? string.Empty
                    : item.Id.Trim();
                ValidateFolderName(folderName);
                if (string.IsNullOrWhiteSpace(item.Version))
                    throw new InvalidDataException($"插件 {folderName} 缺少版本。");
                if (item.Size <= 0)
                    throw new InvalidDataException($"插件 {folderName} 缺少有效大小。");
                if (!IsValidSha256(item.Sha256))
                    throw new InvalidDataException($"插件 {folderName} 缺少有效 SHA-256。");

                string downloadUrl = ResolveDownloadUrl(manifestUri, item.Url, item.FileName);
                ValidateDownloadUrl(downloadUrl);

                result.Add(new YF_Model.RemotePluginInfo
                {
                    PluginId = folderName,
                    PluginName = string.IsNullOrWhiteSpace(item.Name) ? folderName : item.Name.Trim(),
                    FolderName = folderName,
                    Version = item.Version.Trim(),
                    Sha256 = item.Sha256.Trim(),
                    ManifestKeyId = keyId,
                    TotalSize = item.Size,
                    // 清单没有文件数量字段，用 -1 显示为 “-”
                    FileCount = -1,
                    DownloadUrl = downloadUrl,
                    IsInstalled = Directory.Exists(Path.Combine(_pluginsDir, folderName))
                });
            }

            return result;
        }

        /// <summary>校验 SHA-256 是否为 64 位十六进制。</summary>
        /// <param name="value">待校验文本</param>
        /// <returns>是否合法</returns>
        private static bool IsValidSha256(string? value)
        {
            if (value == null || value.Length != 64)
                return false;

            foreach (char c in value)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }

            return true;
        }

        /// <summary>插件 ZIP 只允许 HTTPS 地址。</summary>
        /// <param name="downloadUrl">下载地址</param>
        private static void ValidateDownloadUrl(string downloadUrl)
        {
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out Uri? uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("插件下载地址必须使用 HTTPS。");
        }

        /// <summary>
        /// 从 TCP 插件服务器下载插件 ZIP 并解压。
        /// </summary>
        /// <param name="folderName">插件文件夹名称</param>
        /// <param name="progress">下载进度</param>
        /// <param name="cancellationToken">取消令牌</param>
        [Log(Level = LogLevel.Info, Message = "下载并安装插件")]
        public virtual Task InstallPluginAsync(string folderName, IProgress<int> progress, CancellationToken cancellationToken)
        {
            // TCP 插件源没有签名清单和 ZIP 哈希，不能安全安装；
            // 必须切换到 HTTPS 签名清单，避免留下无校验旁路。
            throw new InvalidOperationException(
                "TCP 插件源不支持签名校验，请切换到 HTTPS 签名清单。");
        }

        /// <summary>
        /// 下载并安装 HTTPS 清单中的插件 ZIP：
        /// 校验大小和 SHA-256 后，使用 staging/原子替换安装。
        /// </summary>
        /// <param name="folderName">插件文件夹名称</param>
        /// <param name="downloadUrl">ZIP 下载地址</param>
        /// <param name="expectedSize">清单声明的大小</param>
        /// <param name="expectedSha256">清单声明的 SHA-256</param>
        /// <param name="version">插件版本</param>
        /// <param name="keyId">清单签名 KeyId</param>
        /// <param name="progress">下载进度</param>
        /// <param name="cancellationToken">取消令牌</param>
        [Log(Level = LogLevel.Info, Message = "下载并安装插件")]
        public virtual async Task InstallPluginFromUrlAsync(
            string folderName,
            string downloadUrl,
            long expectedSize,
            string expectedSha256,
            string version,
            string keyId,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            ValidateFolderName(folderName);
            ValidateDownloadUrl(downloadUrl);
            if (expectedSize <= 0 || !IsValidSha256(expectedSha256))
                throw new InvalidDataException("插件大小或 SHA-256 不合法。");

            string tempRoot = Path.Combine(Path.GetTempPath(), "YFrame", "plugins");
            Directory.CreateDirectory(tempRoot);
            string tempZip = Path.Combine(tempRoot, $"{folderName}_{Guid.NewGuid():N}.zip");

            try
            {
                using var response = await _httpClient.GetAsync(
                    downloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                long contentLength = response.Content.Headers.ContentLength ?? -1;
                if (contentLength > 0 && contentLength != expectedSize)
                    throw new InvalidDataException(
                        $"插件包大小与清单不一致：期望 {expectedSize}，实际 {contentLength}。");

                using (var fs = new FileStream(tempZip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        totalRead += bytesRead;

                        // 先按清单大小做上限保护，避免下载超预期内容
                        if (totalRead > expectedSize)
                            throw new InvalidDataException("插件包超过清单声明大小。");

                        progress?.Report((int)Math.Clamp(
                            totalRead * 100 / expectedSize, 0, 100));
                    }
                }

                long actualSize = new FileInfo(tempZip).Length;
                if (actualSize != expectedSize)
                    throw new InvalidDataException(
                        $"插件包大小校验失败：期望 {expectedSize}，实际 {actualSize}。");

                string actualSha256 = YF_Sha256Hasher.ComputeFile(tempZip);
                if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("插件包 SHA-256 校验失败。");

                PluginPackageInstaller.Install(
                    _pluginsDir,
                    folderName,
                    tempZip,
                    actualSha256,
                    version,
                    keyId,
                    progress,
                    cancellationToken);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempZip))
                        File.Delete(tempZip);
                }
                catch (Exception ex)
                {
                    _logger.ErrorInfo("InstallPluginFromUrlAsync", "清理临时文件失败" + ex.Message);
                }
            }
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
