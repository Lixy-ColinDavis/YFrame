using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace YF_Manager
{
    /// <summary>
    /// 插件清单的 ECDSA P-256 签名与验签工具。
    /// 签名对象是 plugins.json 的原始字节，并使用独立域前缀，
    /// 避免与更新清单签名互相复用。
    /// </summary>
    public static class YF_PluginManifestSigner
    {
        /// <summary>插件清单签名域前缀。</summary>
        private static readonly byte[] Domain =
            Encoding.UTF8.GetBytes("YFRAME-PLUGIN-MANIFEST-V1\n");

        /// <summary>
        /// 使用私钥对插件清单原始字节签名。
        /// </summary>
        /// <param name="manifestBytes">plugins.json 原始字节</param>
        /// <param name="privateKeyPath">ECDSA P-256 私钥 PEM 路径</param>
        /// <returns>签名结果</returns>
        /// <exception cref="InvalidDataException">密钥格式或曲线不合法</exception>
        public static byte[] Sign(byte[] manifestBytes, string privateKeyPath)
        {
            ArgumentNullException.ThrowIfNull(manifestBytes);
            if (string.IsNullOrWhiteSpace(privateKeyPath) || !File.Exists(privateKeyPath))
                throw new FileNotFoundException("插件清单私钥不存在。", privateKeyPath);

            using ECDsa ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(File.ReadAllText(privateKeyPath, Encoding.UTF8));

            if (ecdsa.KeySize < 256)
                throw new InvalidDataException("插件清单私钥必须是 ECDSA P-256 或更高强度。");

            return ecdsa.SignData(BuildPayload(manifestBytes), HashAlgorithmName.SHA256);
        }

        /// <summary>
        /// 使用公钥验证插件清单签名。
        /// </summary>
        /// <param name="manifestBytes">plugins.json 原始字节</param>
        /// <param name="signature">签名文件字节</param>
        /// <param name="publicKeyBase64">SubjectPublicKeyInfo 的 Base64</param>
        /// <returns>签名是否有效</returns>
        public static bool Verify(byte[] manifestBytes, byte[] signature, string publicKeyBase64)
        {
            if (manifestBytes.Length == 0 || signature.Length == 0 ||
                string.IsNullOrWhiteSpace(publicKeyBase64))
                return false;

            try
            {
                using ECDsa ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(
                    Convert.FromBase64String(publicKeyBase64),
                    out _);

                return ecdsa.VerifyData(
                    BuildPayload(manifestBytes),
                    signature,
                    HashAlgorithmName.SHA256);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
                // 公钥损坏或签名格式错误均按验签失败处理
                return false;
            }
        }

        /// <summary>构造带域前缀的签名载荷，避免跨协议签名复用。</summary>
        /// <param name="manifestBytes">plugins.json 原始字节</param>
        /// <returns>域前缀 + 清单原始字节</returns>
        private static byte[] BuildPayload(byte[] manifestBytes)
        {
            var payload = new byte[Domain.Length + manifestBytes.Length];
            Buffer.BlockCopy(Domain, 0, payload, 0, Domain.Length);
            Buffer.BlockCopy(manifestBytes, 0, payload, Domain.Length, manifestBytes.Length);
            return payload;
        }
    }
}
