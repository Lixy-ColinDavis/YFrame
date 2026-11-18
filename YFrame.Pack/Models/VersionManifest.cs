using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YFrame.Pack.Models;

/// <summary>
/// YF_Version.json 的数据结构。
/// 字段名按发布端约定固定，pervious 为历史拼写，不能改成 previous。
/// </summary>
public sealed class VersionManifest
{
    [JsonPropertyName("product")]
    public string Product { get; set; } = "YFrame";

    [JsonPropertyName("current_version")]
    public string CurrentVersion { get; set; } = string.Empty;

    [JsonPropertyName("pervious_version")]
    public string PreviousVersion { get; set; } = string.Empty;

    [JsonPropertyName("updatetime")]
    public string UpdateTime { get; set; } = string.Empty;

    [JsonPropertyName("current")]
    public PackageInfo Current { get; set; } = new();

    [JsonPropertyName("pervious")]
    public PackageInfo Previous { get; set; } = new();

    [JsonPropertyName("removed")]
    public List<string> Removed { get; set; } = new();
}

/// <summary>发布包信息：下载地址、字节大小和 MD5。</summary>
public sealed class PackageInfo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; } = string.Empty;
}
