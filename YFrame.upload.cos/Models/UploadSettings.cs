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
