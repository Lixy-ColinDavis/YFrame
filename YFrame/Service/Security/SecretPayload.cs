using System.Text.Json.Serialization;

namespace YFrame.Service.Security;

/// <summary>
/// 需要 DPAPI 保护的敏感配置。
/// 由安装包/更新包引导文件或安装器写入，不进入 config.conf。
/// </summary>
public sealed class SecretPayload
{
    /// <summary>引导文件版本。</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>APM 应用 Token。</summary>
    [JsonPropertyName("apmToken")]
    public string? ApmToken { get; set; }

    /// <summary>CloudBase API Key。</summary>
    [JsonPropertyName("cloudBaseApiKey")]
    public string? CloudBaseApiKey { get; set; }
}
