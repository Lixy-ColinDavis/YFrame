using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YF_Manager;

namespace YFrame.Sign.Services;

/// <summary>
/// 清单签名结果。
/// </summary>
/// <param name="SignaturePath">生成的 .sig 文件路径</param>
/// <param name="ManifestSha256">被签清单的 SHA-256</param>
/// <param name="SignatureBase64">签名的 Base64 文本，便于界面展示</param>
/// <param name="KeySize">签名密钥位数</param>
public sealed record SignatureResult(
    string SignaturePath,
    string ManifestSha256,
    string SignatureBase64,
    int KeySize);

/// <summary>
/// YF_Version.json 的 ECDSA P-256 签名器。
/// 签名对象是清单原始字节，签名文件默认写为 YF_Version.json.sig。
/// </summary>
public static class ManifestSigner
{
    /// <summary>
    /// 对清单文件进行 ECDSA + SHA-256 签名。
    /// </summary>
    /// <param name="manifestPath">YF_Version.json 完整路径</param>
    /// <param name="privateKeyPath">ECDSA 私钥 PEM 路径</param>
    /// <returns>签名结果</returns>
    /// <exception cref="FileNotFoundException">清单或私钥不存在时抛出</exception>
    /// <exception cref="InvalidDataException">清单不是 SHA-256 格式或私钥不可用时抛出</exception>
    public static SignatureResult Sign(string manifestPath, string privateKeyPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            throw new FileNotFoundException("待签名清单不存在。", manifestPath);
        if (string.IsNullOrWhiteSpace(privateKeyPath) || !File.Exists(privateKeyPath))
            throw new FileNotFoundException("签名私钥不存在。", privateKeyPath);

        // 必须对文件原始字节签名，不能反序列化后重新序列化
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        ValidateManifest(manifestBytes);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(File.ReadAllText(privateKeyPath, Encoding.UTF8));
        if (ecdsa.KeySize < 256)
            throw new InvalidDataException("签名密钥长度不足 256 位，不能用于 SHA-256 清单签名。");

        byte[] signature = ecdsa.SignData(manifestBytes, HashAlgorithmName.SHA256);

        // 先写临时文件再替换，避免生成半截签名文件
        string signaturePath = manifestPath + ".sig";
        string temporaryPath = signaturePath + ".tmp";
        File.WriteAllBytes(temporaryPath, signature);
        File.Move(temporaryPath, signaturePath, overwrite: true);

        string manifestSha256;
        using (var stream = new MemoryStream(manifestBytes))
        {
            manifestSha256 = YF_Sha256Hasher.ComputeStream(stream);
        }

        return new SignatureResult(
            signaturePath,
            manifestSha256,
            Convert.ToBase64String(signature),
            ecdsa.KeySize);
    }

    /// <summary>
    /// 生成一份临时 ECDSA P-256 密钥对，并写入指定目录。
    /// </summary>
    /// <param name="directory">密钥输出目录</param>
    /// <returns>私钥和公钥的完整路径</returns>
    public static (string PrivateKeyPath, string PublicKeyPath) GenerateTemporaryKey(string directory)
    {
        Directory.CreateDirectory(directory);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string privatePath = Path.Combine(directory, $"temp-{stamp}.private.pem");
        string publicPath = Path.Combine(directory, $"temp-{stamp}.public.pem");
        int suffix = 1;
        while (File.Exists(privatePath) || File.Exists(publicPath))
        {
            privatePath = Path.Combine(directory, $"temp-{stamp}-{suffix}.private.pem");
            publicPath = Path.Combine(directory, $"temp-{stamp}-{suffix}.public.pem");
            suffix++;
        }

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string privatePem = PemEncoding.WriteString("PRIVATE KEY", ecdsa.ExportPkcs8PrivateKey());
        string publicPem = PemEncoding.WriteString("PUBLIC KEY", ecdsa.ExportSubjectPublicKeyInfo());

        File.WriteAllText(privatePath, privatePem, new UTF8Encoding(false));
        File.WriteAllText(publicPath, publicPem, new UTF8Encoding(false));

        return (privatePath, publicPath);
    }

    /// <summary>
    /// 校验清单是否包含 current/pervious 的 sha256 字段。
    /// 防止误签仍使用 MD5 的旧清单。
    /// </summary>
    /// <param name="manifestBytes">清单原始字节</param>
    private static void ValidateManifest(byte[] manifestBytes)
    {
        try
        {
            using var document = JsonDocument.Parse(manifestBytes);
            JsonElement root = document.RootElement;

            ValidatePackage(root, "current");
            ValidatePackage(root, "pervious");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("YF_Version.json 不是合法 JSON：" + ex.Message, ex);
        }
    }

    /// <summary>校验清单中的一个发布包节点。</summary>
    /// <param name="root">JSON 根节点</param>
    /// <param name="propertyName">current 或 pervious</param>
    private static void ValidatePackage(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement package) ||
            package.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"清单缺少 {propertyName} 节点。");

        if (!package.TryGetProperty("sha256", out JsonElement sha256) ||
            sha256.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(sha256.GetString()))
        {
            throw new InvalidDataException(
                $"{propertyName} 缺少 sha256，清单可能仍是旧 MD5 格式，请先用新版 Pack 重新生成。");
        }
    }
}
