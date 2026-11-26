using System.Text.Json.Serialization;

namespace YFrame.upload.cos.Models;

/// <summary>
/// 上传到 COS 的插件配置清单。
/// </summary>
public sealed class PluginManifest
{
    /// <summary>生成时间，UTC ISO 8601。</summary>
    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    /// <summary>插件数量。</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>插件条目。</summary>
    [JsonPropertyName("plugins")]
    public List<PluginManifestItem> Plugins { get; set; } = new();
}

/// <summary>清单中的单个插件。</summary>
public sealed class PluginManifestItem
{
    /// <summary>插件名称，即插件目录名。</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>ZIP 文件名。</summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>ZIP 公开访问地址。</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>ZIP 文件大小，单位字节。</summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }
}
