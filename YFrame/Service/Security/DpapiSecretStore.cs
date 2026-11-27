using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YFrame.Service.Security;

/// <summary>
/// 使用 Windows DPAPI 保存 APM Token 和 CloudBase API Key。
/// 作用域为 CurrentUser：换 Windows 用户或换机器后无法解密。
/// </summary>
public static class DpapiSecretStore
{
    // 固定的附加熵；一旦修改，旧密文将无法解密
    private static readonly byte[] Entropy =
        SHA256.HashData(Encoding.UTF8.GetBytes("YFrame.Secrets.v1"));

    /// <summary>密文文件路径：%LocalAppData%\YFrame\secrets.dat。</summary>
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YFrame",
        "secrets.dat");

    /// <summary>判断密文文件是否存在。</summary>
    public static bool Exists() => File.Exists(FilePath);

    /// <summary>
    /// 读取并解密敏感配置；文件不存在或解密失败时返回空对象。
    /// </summary>
    /// <returns>敏感配置；失败时各字段为空</returns>
    public static SecretPayload Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new SecretPayload();

            byte[] encrypted = File.ReadAllBytes(FilePath);
            byte[] plain = ProtectedData.Unprotect(
                encrypted,
                Entropy,
                DataProtectionScope.CurrentUser);

            string json = Encoding.UTF8.GetString(plain);
            return JsonSerializer.Deserialize<SecretPayload>(json)
                   ?? new SecretPayload();
        }
        catch (Exception ex)
        {
            // 文件来自其他用户/机器、损坏或权限不足时按未配置处理
            System.Diagnostics.Debug.WriteLine(
                $"[DpapiSecretStore] 读取密钥失败: {ex.Message}");
            return new SecretPayload();
        }
    }

    /// <summary>判断当前用户是否已经配置了至少一个有效密钥。</summary>
    public static bool IsConfigured()
    {
        SecretPayload payload = Load();
        return !string.IsNullOrWhiteSpace(payload.ApmToken)
               || !string.IsNullOrWhiteSpace(payload.CloudBaseApiKey);
    }

    /// <summary>
    /// 加密并保存敏感配置。
    /// </summary>
    /// <param name="payload">敏感配置</param>
    /// <exception cref="CryptographicException">DPAPI 加密失败</exception>
    public static void Save(SecretPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        string? dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(payload);
        byte[] plain = Encoding.UTF8.GetBytes(json);
        byte[] encrypted = ProtectedData.Protect(
            plain,
            Entropy,
            DataProtectionScope.CurrentUser);

        // 先写临时文件，再替换，避免写入中途损坏
        string tempPath = FilePath + ".tmp";
        File.WriteAllBytes(tempPath, encrypted);
        File.Move(tempPath, FilePath, true);
    }
}
