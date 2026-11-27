using System;
using System.IO;
using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common.Tools
{
    /// <summary>
    /// YF_ZipHelper 解压失败行为测试。
    /// </summary>
    public sealed class YF_ZipHelperTests : IDisposable
    {
        private readonly string _directory;

        /// <summary>创建临时测试目录。</summary>
        public YF_ZipHelperTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "YFrame.ZipHelper.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        /// <summary>损坏 ZIP 必须抛出异常，不能静默返回 false。</summary>
        [Fact]
        public void ExtractToDirectory_InvalidZip_Throws()
        {
            string zipPath = Path.Combine(_directory, "invalid.zip");
            File.WriteAllText(zipPath, "not a zip");
#pragma warning disable CS0618
            Assert.ThrowsAny<Exception>(() =>
                YF_ZipHelper.Instance.ExtractToDirectory(zipPath, Path.Combine(_directory, "out")));
#pragma warning restore CS0618
        }

        /// <summary>ZIP 不存在时必须抛出 FileNotFoundException。</summary>
        [Fact]
        public void ExtractToDirectory_MissingZip_ThrowsFileNotFound()
        {
            string zipPath = Path.Combine(_directory, "missing.zip");
#pragma warning disable CS0618
            Assert.Throws<FileNotFoundException>(() =>
                YF_ZipHelper.Instance.ExtractToDirectory(zipPath, Path.Combine(_directory, "out")));
#pragma warning restore CS0618
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
                // 测试清理失败不影响结果
            }
        }
    }
}
