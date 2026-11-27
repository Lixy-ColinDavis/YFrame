using System;
using System.IO;
using System.Text.Json;
using YF_Manager;

namespace YFrame.Service.Security;

/// <summary>
/// 插件清单验签器。
/// 只从已下载的原始字节中读取 keyId，验签通过前不信任任何其他字段。
/// </summary>
public static class PluginManifestVerifier
{
    /// <summary>清单最大允许大小，避免异常大文件拖垮解析。</summary>
    private const int MaxManifestBytes = 4 * 1024 * 1024;

    /// <summary>
    /// 验证插件清单签名。
    /// </summary>
    /// <param name="manifestBytes">plugins.json 原始字节</param>
    /// <param name="signature">plugins.json.sig 原始字节</param>
    /// <returns>验签通过时返回 keyId</returns>
    /// <exception cref="InvalidDataException">清单过大、格式错误或验签失败</exception>
    public static string Verify(byte[] manifestBytes, byte[] signature)
    {
        if (manifestBytes.Length == 0)
            throw new InvalidDataException("插件清单为空。");
        if (manifestBytes.Length > MaxManifestBytes)
            throw new InvalidDataException("插件清单超过大小限制。");
        if (signature.Length == 0)
            throw new InvalidDataException("插件清单签名文件为空。");

        string keyId;
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                manifestBytes,
                new JsonDocumentOptions { MaxDepth = 16 });
            if (!document.RootElement.TryGetProperty("keyId", out JsonElement keyElement) ||
                keyElement.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("插件清单缺少 keyId。");

            keyId = keyElement.GetString() ?? string.Empty;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("插件清单不是合法 JSON。", ex);
        }

        string? publicKey = PluginTrustKeys.GetPublicKey(keyId);
        if (string.IsNullOrWhiteSpace(publicKey))
            throw new InvalidDataException($"未知的插件清单签名 KeyId：{keyId}");

        if (!YF_PluginManifestSigner.Verify(manifestBytes, signature, publicKey))
            throw new InvalidDataException("插件清单签名验证失败。");

        return keyId;
    }
}
