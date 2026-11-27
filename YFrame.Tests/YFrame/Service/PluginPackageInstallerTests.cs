using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using YFrame.Service.Security;
using YF_Manager;

namespace YFrame.Tests.YFrame.Service
{
    /// <summary>
    /// 插件安全安装与安装记录测试。
    /// </summary>
    public sealed class PluginPackageInstallerTests : IDisposable
    {
        private readonly string _root;
        private readonly string _pluginsRoot;

        /// <summary>创建临时插件根目录。</summary>
        public PluginPackageInstallerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "YFrame.PluginInstaller.Tests",
                Guid.NewGuid().ToString("N"));
            _pluginsRoot = Path.Combine(_root, "plugins");
            Directory.CreateDirectory(_pluginsRoot);
        }

        /// <summary>合法 ZIP 安装后写入安装记录，并且哈希校验通过。</summary>
        [Fact]
        public void Install_ValidZip_CreatesRecordAndVerifies()
        {
            string zipPath = CreateZip("TestPlugin", "payload");
            string hash = YF_Sha256Hasher.ComputeFile(zipPath);

            PluginPackageInstaller.Install(
                _pluginsRoot, "TestPlugin", zipPath, hash, "1.0.0", "test-key",
                null, default);

            string pluginDir = Path.Combine(_pluginsRoot, "TestPlugin");
            string assemblyPath = Path.Combine(pluginDir, "TestPlugin.dll");
            Assert.True(File.Exists(Path.Combine(pluginDir, PluginInstallRecord.FileName)));
            Assert.NotNull(PluginInstallRecord.VerifyIfPresent(pluginDir, assemblyPath));
        }

        /// <summary>安装后替换 DLL，安装记录校验必须失败。</summary>
        [Fact]
        public void VerifyIfPresent_ModifiedAssembly_Throws()
        {
            string zipPath = CreateZip("TestPlugin", "payload");
            string hash = YF_Sha256Hasher.ComputeFile(zipPath);
            PluginPackageInstaller.Install(
                _pluginsRoot, "TestPlugin", zipPath, hash, "1.0.0", "test-key",
                null, default);

            string pluginDir = Path.Combine(_pluginsRoot, "TestPlugin");
            string assemblyPath = Path.Combine(pluginDir, "TestPlugin.dll");
            File.AppendAllText(assemblyPath, "tampered");

            Assert.Throws<InvalidDataException>(() =>
                PluginInstallRecord.VerifyIfPresent(pluginDir, assemblyPath));
        }

        /// <summary>路径穿越 ZIP 必须被拒绝，且不产生插件目录。</summary>
        [Fact]
        public void Install_PathTraversalZip_Throws()
        {
            string zipPath = Path.Combine(_root, "evil.zip");
            using (var stream = new FileStream(zipPath, FileMode.CreateNew))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("../evil.txt");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write("evil");
            }

            string hash = YF_Sha256Hasher.ComputeFile(zipPath);
            Assert.Throws<InvalidDataException>(() =>
                PluginPackageInstaller.Install(
                    _pluginsRoot, "TestPlugin", zipPath, hash, "1.0.0", "test-key",
                    null, default));
            Assert.False(Directory.Exists(Path.Combine(_pluginsRoot, "TestPlugin")));
        }

        /// <summary>创建一个包含入口 DLL 的测试 ZIP。</summary>
        /// <param name="folderName">插件目录名</param>
        /// <param name="content">DLL 内容</param>
        /// <returns>ZIP 路径</returns>
        private string CreateZip(string folderName, string content)
        {
            string zipPath = Path.Combine(_root, folderName + ".zip");
            using var stream = new FileStream(zipPath, FileMode.CreateNew);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            ZipArchiveEntry entry = archive.CreateEntry(folderName + ".dll");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
            return zipPath;
        }

        /// <summary>清理临时目录。</summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // 测试清理失败不影响测试结果
            }
        }
    }
}
