using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using YF_Manager;
using YFrame.Common;

namespace YFrame.Service;

/// <summary>
/// 启动更新检查：读取远端 YF_Version.json，判断本地版本是否可以升级。
/// 相邻版本优先增量，其他较旧版本使用全量包；网络失败、JSON 异常或版本规则不匹配时静默跳过，不阻断主程序启动。
/// </summary>
public static class UpdateService
{
    /// <summary>当前环境对应的清单签名公钥（Base64 SPKI）。</summary>
    public static string TrustedManifestPublicKey => UpdateTrustKeys.GetPublicKey(Config.Env);

    /// <summary>当前环境对应的签名密钥 ID。</summary>
    public static string TrustedManifestKeyId => UpdateTrustKeys.GetKeyId(Config.Env);

    /// <summary>跳过提示的配置键：远端 current_version。</summary>
    private const string SkipVersionKey = "UpdateSkipVersion";

    /// <summary>跳过提示的配置键：UTC 截止时间。</summary>
    private const string SkipUntilKey = "UpdateSkipUntil";

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    /// <summary>
    /// 检查是否需要提示更新。
    /// </summary>
    /// <param name="localVersion">本地程序集版本，例如 2.0.0.0</param>
    /// <param name="versionUrl">可选的版本清单地址，便于测试；为空时按配置中的 Env 生成</param>
    /// <returns>检查结果；无法判断时返回 null</returns>
    public static async Task<UpdateCheckResult?> CheckAsync(string localVersion, string? versionUrl = null)
    {
        try
        {
            string url = versionUrl ?? Config.UpdateVersionUrl;

            // 先取清单和签名，验签通过后才允许解析任何版本/URL 字段
            using var manifestResponse = await Client.GetAsync(url);
            manifestResponse.EnsureSuccessStatusCode();
            byte[] manifestBytes = await manifestResponse.Content.ReadAsByteArrayAsync();

            using var signatureResponse = await Client.GetAsync(url + ".sig");
            signatureResponse.EnsureSuccessStatusCode();
            byte[] signatureBytes = await signatureResponse.Content.ReadAsByteArrayAsync();

            string publicKey = UpdateTrustKeys.GetPublicKey(Config.Env);
            if (!YF_ManifestSigner.Verify(manifestBytes, signatureBytes, publicKey))
            {
                Debug.WriteLine("[UpdateService] 更新清单签名校验失败，忽略本次更新。");
                return null;
            }

            var remote = JsonSerializer.Deserialize<RemoteVersionManifest>(manifestBytes, JsonOptions);
            if (remote == null ||
                string.IsNullOrWhiteSpace(remote.CurrentVersion) ||
                string.IsNullOrWhiteSpace(remote.PreviousVersion))
            {
                return null;
            }

            string current = NormalizeVersion(remote.CurrentVersion);
            string previous = NormalizeVersion(remote.PreviousVersion);
            string local = NormalizeVersion(localVersion);
            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(previous) || string.IsNullOrEmpty(local))
                return null;

            // 已经是最新版本：不提示
            if (string.Equals(current, local, StringComparison.OrdinalIgnoreCase))
                return new UpdateCheckResult(false, current, previous);

            bool isPrevious = string.Equals(previous, local, StringComparison.OrdinalIgnoreCase);
            bool isOlder = IsVersionOlder(local, current);

            // 既不是上一版本、也不比当前版本旧时，不擅自升级或降级
            if (!isPrevious && !isOlder)
                return new UpdateCheckResult(false, current, previous);

            // 相邻版本至少要有增量包；跨版本必须提供全量包
            bool hasIncrement = IsUsablePackage(remote.Previous);
            bool hasFull = IsUsablePackage(remote.Current);
            if (isPrevious)
            {
                if (!hasIncrement && !hasFull)
                    return new UpdateCheckResult(false, current, previous);
            }
            else if (!hasFull)
            {
                return new UpdateCheckResult(false, current, previous);
            }

            // 用户三天内选择过“不更新”，且远端版本没有变化，则不再提示
            if (IsSkipped(current))
                return new UpdateCheckResult(false, current, previous);

            return new UpdateCheckResult(true, current, previous);
        }
        catch
        {
            // 更新检查失败不影响 YFrame 正常启动
            return null;
        }
    }

    /// <summary>
    /// 记录“本次不更新”，三天内不再提示同一远端版本。
    /// </summary>
    /// <param name="currentVersion">远端最新版本</param>
    public static void SaveSkip(string currentVersion)
    {
        try
        {
            YF_ConfigHelper.Instance.Set(SkipVersionKey, currentVersion);
            YF_ConfigHelper.Instance.Set(SkipUntilKey,
                DateTime.UtcNow.AddDays(3).ToString("O", CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] 保存跳过状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 启动根目录下的 YFrame.update.exe。
    /// </summary>
    /// <param name="error">启动失败时的错误说明</param>
    /// <returns>是否成功启动</returns>
    public static bool TryStartUpdater(out string error)
    {
        error = string.Empty;
        string updaterPath = Path.Combine(AppContext.BaseDirectory, "YFrame.update.exe");
        if (!File.Exists(updaterPath))
        {
            error = $"未找到更新程序：{updaterPath}";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(updaterPath)
            {
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            // 把当前环境对应的清单地址和密钥环境传给更新器，避免更新器自行推断
            startInfo.ArgumentList.Add("--env");
            startInfo.ArgumentList.Add(Config.Env);
            startInfo.ArgumentList.Add("--manifest");
            startInfo.ArgumentList.Add(Config.UpdateVersionUrl);
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>判断是否仍在三天跳过期内，且远端版本未变化。</summary>
    private static bool IsSkipped(string currentVersion)
    {
        try
        {
            string skippedVersion = YF_ConfigHelper.Instance.Get(SkipVersionKey);
            string untilText = YF_ConfigHelper.Instance.Get(SkipUntilKey);
            if (!string.Equals(skippedVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!DateTime.TryParse(untilText, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime until))
            {
                return false;
            }

            return DateTime.UtcNow < until;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>归一化为“主.次.修订”，便于忽略四段版本末尾的 0。</summary>
    private static string NormalizeVersion(string value)
    {
        string text = value.Trim().TrimStart('v', 'V');
        if (Version.TryParse(text, out Version? version))
            return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        return text;
    }

    /// <summary>判断本地版本是否低于远端版本；无法解析时返回 false，避免误升级。</summary>
    private static bool IsVersionOlder(string local, string current)
    {
        return Version.TryParse(local, out Version? localVersion) &&
               Version.TryParse(current, out Version? currentVersion) &&
               localVersion < currentVersion;
    }

    /// <summary>判断发布包信息是否完整可用。</summary>
    private static bool IsUsablePackage(RemotePackageInfo? package)
    {
        return package != null &&
               !string.IsNullOrWhiteSpace(package.Url) &&
               package.Size > 0 &&
               !string.IsNullOrWhiteSpace(package.Sha256);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>远端 YF_Version.json 中启动检查需要的字段。</summary>
    private sealed class RemoteVersionManifest
    {
        [JsonPropertyName("current_version")]
        public string CurrentVersion { get; set; } = string.Empty;

        [JsonPropertyName("pervious_version")]
        public string PreviousVersion { get; set; } = string.Empty;

        [JsonPropertyName("current")]
        public RemotePackageInfo? Current { get; set; }

        [JsonPropertyName("pervious")]
        public RemotePackageInfo? Previous { get; set; }
    }

    /// <summary>远端发布包描述。</summary>
    private sealed class RemotePackageInfo
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;
    }
}

/// <summary>启动更新检查结果。</summary>
public sealed class UpdateCheckResult
{
    public UpdateCheckResult(bool shouldPrompt, string currentVersion, string previousVersion)
    {
        ShouldPrompt = shouldPrompt;
        CurrentVersion = currentVersion;
        PreviousVersion = previousVersion;
    }

    /// <summary>是否需要弹出更新提示。</summary>
    public bool ShouldPrompt { get; }

    /// <summary>远端最新版本。</summary>
    public string CurrentVersion { get; }

    /// <summary>远端上一版本，也是本地当前版本。</summary>
    public string PreviousVersion { get; }
}
