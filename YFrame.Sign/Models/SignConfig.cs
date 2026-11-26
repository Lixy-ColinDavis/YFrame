using System.IO;
using System.Text;
using System.Text.Json;

namespace YFrame.Sign.Models;

/// <summary>
/// YFrame.Sign 的本地配置，保存在 exe 同目录。
/// 只保存路径，不保存私钥内容和密码。
/// </summary>
public sealed class SignConfig
{
    /// <summary>配置文件固定名称。</summary>
    public const string FileName = "YFrame.Sign.config.json";

    /// <summary>父目录：待签清单所在目录，默认指向 Pack 的最新输出目录。</summary>
    public string BaseDirectory { get; set; } = string.Empty;

    /// <summary>相对于父目录的清单路径，默认 YF_Version.json。</summary>
    public string RelativeManifestPath { get; set; } = "YF_Version.json";

    /// <summary>ECDSA 私钥 PEM 路径。</summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>临时生成的公钥 PEM 路径，便于后续验证。</summary>
    public string PublicKeyPath { get; set; } = string.Empty;

    /// <summary>配置文件完整路径。</summary>
    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>
    /// 读取本地配置；文件缺失或损坏时回退默认配置。
    /// </summary>
    /// <returns>可用配置对象</returns>
    public static SignConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return CreateDefault();

            string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
            var config = JsonSerializer.Deserialize<SignConfig>(json, JsonOptions);
            if (config == null)
                return CreateDefault();

            if (string.IsNullOrWhiteSpace(config.BaseDirectory))
                config.BaseDirectory = FindDefaultBaseDirectory();
            if (string.IsNullOrWhiteSpace(config.RelativeManifestPath))
                config.RelativeManifestPath = "YF_Version.json";

            return config;
        }
        catch
        {
            // 配置损坏不应阻止工具启动
            return CreateDefault();
        }
    }

    /// <summary>
    /// 将当前配置写回 exe 同目录。
    /// </summary>
    public void Save()
    {
        string? directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions),
            new UTF8Encoding(false));
    }

    /// <summary>创建默认配置，并尝试定位 Pack 的最新输出目录。</summary>
    private static SignConfig CreateDefault() => new()
    {
        BaseDirectory = FindDefaultBaseDirectory(),
        RelativeManifestPath = "YF_Version.json"
    };

    /// <summary>
    /// 返回 Pack 的 output 下最新的 SHA-256 清单目录。
    /// 找不到 SHA-256 时回退最新任意清单；再找不到返回空串。
    /// </summary>
    /// <returns>最新输出目录；不存在时返回空串</returns>
    public static string FindLatestOutputDirectory()
    {
        try
        {
            string packRoot = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\YFrame.Pack"));

            foreach (string configuration in GetConfigurationOrder())
            {
                string output = Path.Combine(packRoot, "bin", configuration,
                    "net8.0-windows", "output");
                if (!Directory.Exists(output))
                    continue;

                string[] candidates = Directory.GetDirectories(output)
                    .Where(directory => File.Exists(Path.Combine(directory, "YF_Version.json")))
                    .OrderByDescending(directory => directory, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                // 优先选择新 SHA-256 清单，避免默认打开旧 MD5 产物
                string? sha256 = candidates.FirstOrDefault(directory =>
                    ContainsSha256Manifest(Path.Combine(directory, "YF_Version.json")));

                string? latest = sha256 ?? candidates.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(latest))
                    return latest;

                if (File.Exists(Path.Combine(output, "YF_Version.json")))
                    return output;
            }
        }
        catch
        {
            // 自动定位失败时由调用方回退
        }

        return string.Empty;
    }

    /// <summary>
    /// 返回 YFrame.Pack.exe 所在目录，作为临时密钥默认生成目录。
    /// 若找不到已构建的 Pack，则回退到 Sign 自身目录。
    /// </summary>
    /// <returns>Pack 目录或 Sign 目录</returns>
    public static string FindPackDirectory()
    {
        try
        {
            // 已发布到同一目录时，直接使用当前目录
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "YFrame.Pack.exe")))
                return AppContext.BaseDirectory;

            string packRoot = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\YFrame.Pack"));

            foreach (string configuration in GetConfigurationOrder())
            {
                string candidate = Path.Combine(packRoot, "bin", configuration, "net8.0-windows");
                if (File.Exists(Path.Combine(candidate, "YFrame.Pack.exe")))
                    return candidate;
            }
        }
        catch
        {
            // Pack 不存在时使用 Sign 目录，保证仍可生成临时密钥
        }

        return AppContext.BaseDirectory;
    }

    /// <summary>根据当前 Sign 的 Debug/Release 配置决定优先查找顺序。</summary>
    private static string[] GetConfigurationOrder()
    {
        bool isRelease = AppContext.BaseDirectory.Contains(@"\Release\", StringComparison.OrdinalIgnoreCase);
        return isRelease
            ? new[] { "Release", "Debug" }
            : new[] { "Debug", "Release" };
    }

    /// <summary>
    /// 查找默认父目录；找不到 Pack 输出时回退到 Sign 自身目录。
    /// </summary>
    private static string FindDefaultBaseDirectory()
    {
        string latest = FindLatestOutputDirectory();
        return string.IsNullOrWhiteSpace(latest) ? AppContext.BaseDirectory : latest;
    }

    /// <summary>判断清单文件是否包含 current/pervious 的 sha256 字段。</summary>
    /// <param name="manifestPath">YF_Version.json 路径</param>
    private static bool ContainsSha256Manifest(string manifestPath)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
            JsonElement root = document.RootElement;

            foreach (string name in new[] { "current", "pervious" })
            {
                if (!root.TryGetProperty(name, out JsonElement package) ||
                    !package.TryGetProperty("sha256", out JsonElement sha256) ||
                    sha256.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(sha256.GetString()))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
