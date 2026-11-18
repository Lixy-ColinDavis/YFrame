using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YFrame.Update.Models;

/// <summary>YF_Version.json 中更新器需要的字段。</summary>
public sealed class RemoteUpdateManifest
{
    [JsonPropertyName("current_version")]
    public string CurrentVersion { get; set; } = string.Empty;

    [JsonPropertyName("pervious_version")]
    public string PreviousVersion { get; set; } = string.Empty;

    [JsonPropertyName("current")]
    public PackageInfo Current { get; set; } = new();

    [JsonPropertyName("pervious")]
    public PackageInfo Previous { get; set; } = new();

    [JsonPropertyName("removed")]
    public List<string> Removed { get; set; } = new();
}

/// <summary>发布包信息。</summary>
public sealed class PackageInfo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; } = string.Empty;
}

/// <summary>更新进度。</summary>
public sealed class UpdateProgress
{
    public UpdateProgress(double percent, string status, string detail = "")
    {
        Percent = percent;
        Status = status;
        Detail = detail;
    }

    /// <summary>0-100 的进度。</summary>
    public double Percent { get; }

    /// <summary>当前阶段。</summary>
    public string Status { get; }

    /// <summary>补充说明。</summary>
    public string Detail { get; }
}

/// <summary>更新执行结果。</summary>
public sealed class UpdateResult
{
    /// <summary>本地版本已经是当前版本，只需要重启。</summary>
    public bool AlreadyCurrent { get; init; }

    /// <summary>更新后的版本。</summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>本次备份目录；无需更新时为空。</summary>
    public string BackupDirectory { get; init; } = string.Empty;
}
