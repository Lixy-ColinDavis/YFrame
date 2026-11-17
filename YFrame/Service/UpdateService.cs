using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using YF_Manager;

namespace YFrame.Service;

/// <summary>
/// 启动更新检查：读取远端 YF_Version.json，判断本地版本是否处于可升级的上一版本。
/// 网络失败、JSON 异常或版本规则不匹配时静默跳过，不阻断主程序启动。
/// </summary>
public static class UpdateService
{
    /// <summary>远端版本清单固定地址。</summary>
    public const string VersionUrl =
        "https://yfcos-1256976024.cos.ap-beijing.myqcloud.com/YFrame/update/uat/YF_Version.json";

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
    /// <param name="versionUrl">可选的版本清单地址，便于测试；为空时使用固定地址</param>
    /// <returns>检查结果；无法判断时返回 null</returns>
    public static async Task<UpdateCheckResult?> CheckAsync(string localVersion, string? versionUrl = null)
    {
        try
        {
            string json = await Client.GetStringAsync(versionUrl ?? VersionUrl);
            var remote = JsonSerializer.Deserialize<RemoteVersionManifest>(json, JsonOptions);
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

            // 只有本地正好是上一版本时才提示；其他跨版本情况不擅自处理
            if (!string.Equals(previous, local, StringComparison.OrdinalIgnoreCase))
                return new UpdateCheckResult(false, current, previous);

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
            Process.Start(new ProcessStartInfo(updaterPath)
            {
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory
            });
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
