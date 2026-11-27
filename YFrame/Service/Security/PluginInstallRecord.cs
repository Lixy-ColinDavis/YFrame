using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using YF_Manager;

namespace YFrame.Service.Security;

/// <summary>
/// 插件安装记录，用于安装后校验主程序集是否被替换。
/// </summary>
public sealed class PluginInstallRecord
{
    /// <summary>记录格式版本。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>插件标识。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>插件版本。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>安装包 SHA-256。</summary>
    [JsonPropertyName("zipSha256")]
    public string ZipSha256 { get; set; } = string.Empty;

    /// <summary>入口程序集文件名。</summary>
    [JsonPropertyName("entryAssembly")]
    public string EntryAssembly { get; set; } = string.Empty;

    /// <summary>入口程序集 SHA-256。</summary>
    [JsonPropertyName("entrySha256")]
    public string EntrySha256 { get; set; } = string.Empty;

    /// <summary>清单签名 KeyId。</summary>
    [JsonPropertyName("keyId")]
    public string KeyId { get; set; } = string.Empty;

    /// <summary>安装时间，UTC ISO 8601。</summary>
    [JsonPropertyName("installedAt")]
    public string InstalledAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    /// <summary>安装记录文件名。</summary>
    public const string FileName = ".yframe-plugin.json";

    /// <summary>
    /// 读取并校验插件安装记录。
    /// 没有记录时返回 null，调用方可按 legacy 插件处理；
    /// 有记录但哈希不匹配时抛出异常，拒绝加载。
    /// </summary>
    /// <param name="pluginDirectory">插件目录</param>
    /// <param name="assemblyPath">入口程序集路径</param>
    /// <returns>安装记录；legacy 插件返回 null</returns>
    public static PluginInstallRecord? VerifyIfPresent(string pluginDirectory, string assemblyPath)
    {
        string recordPath = Path.Combine(pluginDirectory, FileName);
        if (!File.Exists(recordPath))
            return null;

        PluginInstallRecord record;
        try
        {
            string json = File.ReadAllText(recordPath);
            record = JsonSerializer.Deserialize<PluginInstallRecord>(json)
                     ?? throw new InvalidDataException("插件安装记录为空。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("插件安装记录格式错误。", ex);
        }

        if (!string.Equals(record.Id, Path.GetFileName(pluginDirectory), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("插件安装记录 ID 与目录不一致。");
        if (string.IsNullOrWhiteSpace(record.EntrySha256))
            throw new InvalidDataException("插件安装记录缺少入口程序集哈希。");
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException("插件入口程序集不存在。", assemblyPath);

        string actualHash = YF_Sha256Hasher.ComputeFile(assemblyPath);
        if (!string.Equals(actualHash, record.EntrySha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("插件入口程序集哈希不匹配，可能已被替换。");

        return record;
    }
}
