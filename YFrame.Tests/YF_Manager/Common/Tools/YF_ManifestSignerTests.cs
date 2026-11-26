using System.IO;
using System.Text;
using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common.Tools;

/// <summary>
/// YF_ManifestSigner 签名/验签单元测试。
/// </summary>
public sealed class YF_ManifestSignerTests : IDisposable
{
    private readonly string _directory;

    /// <summary>为每个测试创建独立临时目录。</summary>
    public YF_ManifestSignerTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"YFrame_SignTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    /// <summary>
    /// 正确私钥签名的清单必须能通过对应公钥验证。
    /// </summary>
    [Fact]
    public void SignAndVerify_WithMatchingKey_ReturnsTrue()
    {
        string manifestPath = WriteManifest();
        var key = YF_ManifestSigner.GenerateTemporaryKey(_directory);

        var result = YF_ManifestSigner.Sign(manifestPath, key.PrivateKeyPath);
        string publicKey = ReadPublicKeyBase64(key.PublicKeyPath);

        Assert.True(YF_ManifestSigner.Verify(
            File.ReadAllBytes(manifestPath),
            File.ReadAllBytes(result.SignaturePath),
            publicKey));
    }

    /// <summary>
    /// 修改清单后签名必须失效。
    /// </summary>
    [Fact]
    public void Verify_TamperedManifest_ReturnsFalse()
    {
        string manifestPath = WriteManifest();
        var key = YF_ManifestSigner.GenerateTemporaryKey(_directory);
        var result = YF_ManifestSigner.Sign(manifestPath, key.PrivateKeyPath);
        string publicKey = ReadPublicKeyBase64(key.PublicKeyPath);

        File.AppendAllText(manifestPath, " ", Encoding.UTF8);

        Assert.False(YF_ManifestSigner.Verify(
            File.ReadAllBytes(manifestPath),
            File.ReadAllBytes(result.SignaturePath),
            publicKey));
    }

    /// <summary>
    /// 使用另一把公钥验证必须失败。
    /// </summary>
    [Fact]
    public void Verify_WithWrongPublicKey_ReturnsFalse()
    {
        string manifestPath = WriteManifest();
        var signingKey = YF_ManifestSigner.GenerateTemporaryKey(_directory);
        var otherKey = YF_ManifestSigner.GenerateTemporaryKey(_directory);
        var result = YF_ManifestSigner.Sign(manifestPath, signingKey.PrivateKeyPath);

        Assert.False(YF_ManifestSigner.Verify(
            File.ReadAllBytes(manifestPath),
            File.ReadAllBytes(result.SignaturePath),
            ReadPublicKeyBase64(otherKey.PublicKeyPath)));
    }

    /// <summary>
    /// 缺少 sha256 的旧清单必须拒绝签名。
    /// </summary>
    [Fact]
    public void Sign_ManifestWithoutSha256_Throws()
    {
        string manifestPath = Path.Combine(_directory, "YF_Version.json");
        File.WriteAllText(manifestPath,
            "{\"current\":{\"md5\":\"abc\"},\"pervious\":{\"md5\":\"def\"}}", Encoding.UTF8);
        var key = YF_ManifestSigner.GenerateTemporaryKey(_directory);

        Assert.Throws<InvalidDataException>(() =>
            YF_ManifestSigner.Sign(manifestPath, key.PrivateKeyPath));
    }

    /// <summary>写入一份包含 sha256 的合法测试清单。</summary>
    private string WriteManifest()
    {
        string path = Path.Combine(_directory, "YF_Version.json");
        File.WriteAllText(path,
            "{\"current\":{\"url\":\"https://example/current.zip\",\"size\":1,\"sha256\":\"" +
            new string('a', 64) +
            "\"},\"pervious\":{\"url\":\"https://example/increment.zip\",\"size\":1,\"sha256\":\"" +
            new string('b', 64) + "\"}}",
            Encoding.UTF8);
        return path;
    }

    /// <summary>读取公钥 PEM 并去掉头尾，得到 Base64。</summary>
    private static string ReadPublicKeyBase64(string path)
        => string.Concat(File.ReadAllLines(path)
            .Where(line => !line.StartsWith("-----", StringComparison.Ordinal))
            .Select(line => line.Trim()));

    /// <summary>清理测试临时目录。</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // 测试清理失败不影响断言结果
        }
    }
}
