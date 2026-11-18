using System.IO;
using System.Text.Json;
using YFrame.Common;

namespace YFrame.Pack.Models;

/// <summary>
/// Pack 工具的本地配置，保存在 exe 同目录，记录最近使用的两个版本包和发布地址。
/// </summary>
public sealed class PackConfig
{
    /// <summary>配置文件固定名称，跟随 YFrame.Pack.exe 所在目录。</summary>
    public const string FileName = "YFrame.Pack.config.json";

    /// <summary>增量包和版本清单的输出目录。</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>最新完整包在服务器上的固定地址。</summary>
    public string CurrentUrl { get; set; } =
        Cos_UpdateDefaults.CurrentUrl(Cos_UpdateDefaults.DefaultEnv);

    /// <summary>增量包在服务器上的固定地址。</summary>
    public string IncrementUrl { get; set; } =
        Cos_UpdateDefaults.IncrementUrl(Cos_UpdateDefaults.DefaultEnv);

    /// <summary>配置文件完整路径。</summary>
    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>
    /// 从 exe 同目录读取配置；文件不存在或格式损坏时返回默认配置。
    /// </summary>
    /// <returns>可用配置对象</returns>
    public static PackConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return CreateDefault();

            string json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<PackConfig>(json, JsonOptions);
            if (config == null) return CreateDefault();

            // 旧配置可能缺少输出目录，补齐为默认目录
            if (string.IsNullOrWhiteSpace(config.OutputDirectory))
                config.OutputDirectory = Path.Combine(AppContext.BaseDirectory, "output");

            if (string.IsNullOrWhiteSpace(config.CurrentUrl))
                config.CurrentUrl = CreateDefault().CurrentUrl;
            if (string.IsNullOrWhiteSpace(config.IncrementUrl))
                config.IncrementUrl = CreateDefault().IncrementUrl;

            return config;
        }
        catch
        {
            // 配置损坏不应阻止工具启动，直接回退默认值
            return CreateDefault();
        }
    }

    /// <summary>
    /// 将当前配置写回 exe 同目录。
    /// </summary>
    public void Save()
    {
        try
        {
            string? directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // 配置保存失败不阻断打包，界面日志会提示实际输出结果
        }
    }

    /// <summary>创建默认配置。</summary>
    private static PackConfig CreateDefault() => new()
    {
        OutputDirectory = Path.Combine(AppContext.BaseDirectory, "output")
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
