using YF_Manager;

namespace YFrame.Sign.Services;

/// <summary>
/// Sign 界面的签名结果。
/// </summary>
/// <param name="SignaturePath">生成的 .sig 文件路径</param>
/// <param name="ManifestSha256">被签清单的 SHA-256</param>
/// <param name="SignatureBase64">签名的 Base64 文本</param>
/// <param name="KeySize">签名密钥位数</param>
public sealed record SignatureResult(
    string SignaturePath,
    string ManifestSha256,
    string SignatureBase64,
    int KeySize);

/// <summary>
/// Sign 工具适配层。
/// 实际签名/验签/密钥生成统一由 YF_Manager.YF_ManifestSigner 实现。
/// </summary>
public static class ManifestSigner
{
    /// <summary>对清单文件签名。</summary>
    /// <param name="manifestPath">清单路径</param>
    /// <param name="privateKeyPath">私钥 PEM 路径</param>
    /// <returns>Sign 界面使用的签名结果</returns>
    public static SignatureResult Sign(string manifestPath, string privateKeyPath)
    {
        ManifestSignatureResult result = YF_ManifestSigner.Sign(manifestPath, privateKeyPath);
        return new SignatureResult(
            result.SignaturePath,
            result.ManifestSha256,
            result.SignatureBase64,
            result.KeySize);
    }

    /// <summary>生成临时 ECDSA P-256 密钥对。</summary>
    /// <param name="directory">密钥目录</param>
    /// <returns>私钥和公钥路径</returns>
    public static (string PrivateKeyPath, string PublicKeyPath) GenerateTemporaryKey(string directory)
        => YF_ManifestSigner.GenerateTemporaryKey(directory);
}
