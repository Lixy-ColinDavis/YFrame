using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YF_Manager
{
    /// <summary>清单签名结果。</summary>
    /// <param name="SignaturePath">生成的 .sig 文件路径</param>
    /// <param name="ManifestSha256">被签清单的 SHA-256</param>
    /// <param name="SignatureBase64">签名的 Base64 文本</param>
    /// <param name="KeySize">签名密钥位数</param>
    public sealed record ManifestSignatureResult(
        string SignaturePath,
        string ManifestSha256,
        string SignatureBase64,
        int KeySize);

    /// <summary>
    /// 更新清单的 ECDSA P-256 签名与验签工具。
    /// 签名对象是 YF_Version.json 原始字节，签名文件为 YF_Version.json.sig。
    /// 该类只做密码学操作，不依赖 UI、配置或 DI。
    /// </summary>
    public static class YF_ManifestSigner
    {
        /// <summary>
        /// 对清单文件进行 ECDSA + SHA-256 签名。
        /// </summary>
        /// <param name="manifestPath">YF_Version.json 完整路径</param>
        /// <param name="privateKeyPath">ECDSA 私钥 PEM 路径</param>
        /// <returns>签名结果</returns>
        /// <exception cref="FileNotFoundException">清单或私钥不存在</exception>
        /// <exception cref="InvalidDataException">清单格式错误或私钥不可用</exception>
        public static ManifestSignatureResult Sign(string manifestPath, string privateKeyPath)
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
            string signaturePath = manifestPath + ".sig";
            WriteAtomic(signaturePath, signature);

            string manifestSha256;
            using (var stream = new MemoryStream(manifestBytes))
            {
                manifestSha256 = YF_Sha256Hasher.ComputeStream(stream);
            }

            return new ManifestSignatureResult(
                signaturePath,
                manifestSha256,
                Convert.ToBase64String(signature),
                ecdsa.KeySize);
        }

        /// <summary>
        /// 验证清单签名。
        /// </summary>
        /// <param name="manifestBytes">清单原始字节</param>
        /// <param name="signature">.sig 文件内容</param>
        /// <param name="publicKeyBase64">SubjectPublicKeyInfo Base64 公钥</param>
        /// <returns>签名有效返回 true；任何异常或格式错误返回 false</returns>
        public static bool Verify(byte[] manifestBytes, byte[] signature, string publicKeyBase64)
        {
            if (manifestBytes.Length == 0 ||
                signature.Length == 0 ||
                string.IsNullOrWhiteSpace(publicKeyBase64))
                return false;

            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
                return ecdsa.VerifyData(manifestBytes, signature, HashAlgorithmName.SHA256);
            }
            catch
            {
                // 验签失败必须按不可信处理，不能回退到哈希校验
                return false;
            }
        }

        /// <summary>
        /// 读取清单和签名文件并验证。
        /// </summary>
        /// <param name="manifestPath">清单路径</param>
        /// <param name="signaturePath">签名路径</param>
        /// <param name="publicKeyBase64">公钥 Base64</param>
        /// <returns>验证通过返回 true</returns>
        public static bool Verify(string manifestPath, string signaturePath, string publicKeyBase64)
        {
            try
            {
                if (!File.Exists(manifestPath) || !File.Exists(signaturePath))
                    return false;

                return Verify(File.ReadAllBytes(manifestPath), File.ReadAllBytes(signaturePath),
                    publicKeyBase64);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 生成一份临时 ECDSA P-256 密钥对。
        /// </summary>
        /// <param name="directory">密钥输出目录</param>
        /// <returns>私钥和公钥路径</returns>
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

        /// <summary>校验清单是否包含 current/previous 的 sha256 字段。</summary>
        /// <param name="manifestBytes">清单原始字节</param>
        private static void ValidateManifest(byte[] manifestBytes)
        {
            try
            {
                // 兼容带 UTF-8 BOM 的清单；签名仍覆盖原始字节，不修改文件内容
                ReadOnlyMemory<byte> jsonBytes = manifestBytes;
                if (jsonBytes.Length >= 3 &&
                    jsonBytes.Span[0] == 0xEF &&
                    jsonBytes.Span[1] == 0xBB &&
                    jsonBytes.Span[2] == 0xBF)
                {
                    jsonBytes = jsonBytes[3..];
                }

                using var document = JsonDocument.Parse(jsonBytes);
                JsonElement root = document.RootElement;

                ValidatePackage(root, "current");
                ValidatePackage(root, "previous");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("YF_Version.json 不是合法 JSON：" + ex.Message, ex);
            }
        }

        /// <summary>校验清单中的一个发布包节点。</summary>
        /// <param name="root">JSON 根节点</param>
        /// <param name="propertyName">current 或 previous</param>
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

        /// <summary>先写临时文件再替换，避免生成半截签名。</summary>
        /// <param name="path">目标路径</param>
        /// <param name="content">文件内容</param>
        private static void WriteAtomic(string path, byte[] content)
        {
            string temporaryPath = path + ".tmp";
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
    }
}
