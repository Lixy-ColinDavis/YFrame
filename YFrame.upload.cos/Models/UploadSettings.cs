using System.IO;
using System.Text.Json;

namespace YFrame.upload.cos.Models;

/// <summary>
/// 上传器本地配置，保存 Pack 输出根目录、密钥 CSV 路径和 COS 基础参数。
/// </summary>
public sealed class UploadSettings
{
    /// <summary>Pack 输出根目录，例如 ...\YFrame.Pack\bin\Release\net8.0-windows\output。</summary>
    public string PackOutputRoot { get; set; } =
        @"C:\Users\Administrator\Desktop\code\C#\YFrame\YFrame.Pack\bin\Release\net8.0-windows\output";

    /// <summary>SecretKey.csv 路径。</summary>
    public string SecretKeyFile { get; set; } =
        @"C:\Users\Administrator\Desktop\code\C#\YFrame\SigningKeys\coskey\SecretKey.csv";

    /// <summary>COS 存储桶。</summary>
    public string Bucket { get; set; } = "yfcos-1256976024";

    /// <summary>COS 地域。</summary>
    public string Region { get; set; } = "ap-beijing";

    /// <summary>发布环境：uat 或 pdt。</summary>
    public string Environment { get; set; } = "uat";

    /// <summary>本地插件目录，下面每个子文件夹对应一个插件。</summary>
    public string PluginSourceRoot { get; set; } =
        @"C:\Users\Administrator\Desktop\code\C#\YFrame\YFrame\bin\Release\net8.0-windows\plugins";

    /// <summary>插件 ZIP 和清单的本地输出目录。</summary>
    public string PluginZipOutputRoot { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "PluginPackages");

    /// <summary>COS 上的插件目录前缀。</summary>
    public string PluginRemotePrefix { get; set; } = "YFrame/plugins/file";

    /// <summary>插件包和清单的公开访问根地址。</summary>
    public string PluginBaseUrl { get; set; } =
        "https://yfcos-1256976024.cos.ap-beijing.myqcloud.com/YFrame/plugins/file";

    /// <summary>COS 上 plugins.json 的独立目录前缀。</summary>
    public string PluginManifestRemotePrefix { get; set; } = "YFrame/plugins";

    /// <summary>plugins.json 的公开访问根地址。</summary>
    public string PluginManifestBaseUrl { get; set; } =
        "https://yfcos-1256976024.cos.ap-beijing.myqcloud.com/YFrame/plugins";

    /// <summary>插件配置清单文件名。</summary>
    public string PluginManifestFileName { get; set; } = "plugins.json";

    /// <summary>插件清单签名私钥 PEM 路径；私钥不得上传或提交。</summary>
    public string PluginPrivateKeyFile { get; set; } =
        @"C:\Users\Administrator\Desktop\code\C#\YFrame\SigningKeys\plugins\plugin-manifest-2026-01.private.pem";

    /// <summary>插件清单签名 KeyId，必须与客户端内置公钥匹配。</summary>
    public string PluginKeyId { get; set; } = "plugin-2026-01";

    /// <summary>
    /// 已勾选的插件目录名。null 表示旧配置尚未初始化，默认全选；
    /// 空集合表示用户明确取消全部勾选。
    /// </summary>
    public List<string>? SelectedPlugins { get; set; }
}

/// <summary>
/// 本地配置文件读写服务。
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>配置文件路径。</summary>
    public string FilePath { get; } = Path.Combine(
        AppContext.BaseDirectory, "Config", "upload.settings.json");

    /// <summary>读取配置；不存在或损坏时返回默认配置。</summary>
    /// <returns>上传设置</returns>
    public UploadSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new UploadSettings();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<UploadSettings>(json, JsonOptions)
                   ?? new UploadSettings();
        }
        catch
        {
            return new UploadSettings();
        }
    }

    /// <summary>保存配置。</summary>
    /// <param name="settings">待保存设置</param>
    public void Save(UploadSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, FilePath, overwrite: true);
    }
}
