namespace YFrame.Service.Security;

/// <summary>
/// 插件清单信任公钥。
/// 只保存公钥；私钥离线保存在 SigningKeys/plugins，不进入客户端。
/// </summary>
public static class PluginTrustKeys
{
    /// <summary>当前插件清单签名 KeyId。</summary>
    public const string CurrentKeyId = "plugin-2026-01";

    /// <summary>
    /// 插件清单公钥（SubjectPublicKeyInfo Base64）。
    /// 对应 SigningKeys/plugins/plugin-manifest-2026-01.public.pem。
    /// </summary>
    private const string CurrentPublicKey =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE+POSk9mgUI0tURje6F3neEbwsjv+PgsJ/H5r/4eZTdqYvLesy30lTlXnBaRzLUnb0oHQKpySs0Y1AvrDWrRQ4Q==";

    /// <summary>
    /// 按 KeyId 获取插件清单公钥。
    /// </summary>
    /// <param name="keyId">清单中的签名密钥标识</param>
    /// <returns>公钥 Base64；未知 KeyId 返回 null</returns>
    public static string? GetPublicKey(string? keyId)
    {
        if (string.Equals(keyId, CurrentKeyId, System.StringComparison.Ordinal))
            return CurrentPublicKey;

        return null;
    }
}
