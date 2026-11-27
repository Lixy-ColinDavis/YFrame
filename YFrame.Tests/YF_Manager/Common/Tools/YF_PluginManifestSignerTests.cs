using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common.Tools
{
    /// <summary>
    /// 插件清单签名工具测试。
    /// </summary>
    public sealed class YF_PluginManifestSignerTests : IDisposable
    {
        private readonly string _directory;

        /// <summary>创建临时测试目录。</summary>
        public YF_PluginManifestSignerTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "YFrame.PluginSigner.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        /// <summary>验证签名与验签成功。</summary>
        [Fact]
        public void SignAndVerify_ValidManifest_ReturnsTrue()
        {
            (string privatePath, string publicPath) = YF_ManifestSigner.GenerateTemporaryKey(_directory);
            byte[] manifest = Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":2,\"keyId\":\"test\",\"plugins\":[]}");

            byte[] signature = YF_PluginManifestSigner.Sign(manifest, privatePath);
            string publicBase64 = ExportPublicKeyBase64(publicPath);

            Assert.True(YF_PluginManifestSigner.Verify(manifest, signature, publicBase64));
        }

        /// <summary>验证清单被篡改后验签失败。</summary>
        [Fact]
        public void Verify_TamperedManifest_ReturnsFalse()
        {
            (string privatePath, string publicPath) = YF_ManifestSigner.GenerateTemporaryKey(_directory);
            byte[] manifest = Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":2,\"keyId\":\"test\",\"plugins\":[]}");
            byte[] signature = YF_PluginManifestSigner.Sign(manifest, privatePath);
            string publicBase64 = ExportPublicKeyBase64(publicPath);

            manifest[0] ^= 0x01;

            Assert.False(YF_PluginManifestSigner.Verify(manifest, signature, publicBase64));
        }

        /// <summary>验证其他密钥不能通过验签。</summary>
        [Fact]
        public void Verify_WrongKey_ReturnsFalse()
        {
            (string privatePath, _) = YF_ManifestSigner.GenerateTemporaryKey(_directory);
            (_, string otherPublicPath) = YF_ManifestSigner.GenerateTemporaryKey(
                Path.Combine(_directory, "other"));

            byte[] manifest = Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":2,\"keyId\":\"test\",\"plugins\":[]}");
            byte[] signature = YF_PluginManifestSigner.Sign(manifest, privatePath);
            string publicBase64 = ExportPublicKeyBase64(otherPublicPath);

            Assert.False(YF_PluginManifestSigner.Verify(manifest, signature, publicBase64));
        }

        /// <summary>把公钥 PEM 转换为 SubjectPublicKeyInfo Base64。</summary>
        /// <param name="publicKeyPath">公钥 PEM 路径</param>
        /// <returns>Base64 文本</returns>
        private static string ExportPublicKeyBase64(string publicKeyPath)
        {
            using ECDsa ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(File.ReadAllText(publicKeyPath, Encoding.UTF8));
            return Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
        }

        /// <summary>清理临时目录。</summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                    Directory.Delete(_directory, recursive: true);
            }
            catch
            {
                // 测试清理失败不影响测试结果
            }
        }
    }
}
