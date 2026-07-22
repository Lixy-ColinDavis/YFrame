using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common.Tools
{
    /// <summary>
    /// YF_FileHelper 单元测试 — 非 AOP 方法（EnsureDirectory、EnsureDirectoryForFile、GetFileName 等）
    /// 注意：AOP virtual 方法通过直接 new YF_FileHelper() 实例调用，绕过代理
    /// </summary>
    public class YF_FileHelperTests : IDisposable
    {
        private readonly string _testRootDir;

        public YF_FileHelperTests()
        {
            _testRootDir = Path.Combine(Path.GetTempPath(), $"YFrame_Test_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            // 清理测试目录
            if (Directory.Exists(_testRootDir))
                Directory.Delete(_testRootDir, recursive: true);
        }

        /// <summary>
        /// EnsureDirectory 创建不存在的目录
        /// </summary>
        [Fact]
        public void EnsureDirectory_CreatesNonExistentDir()
        {
            var helper = new YF_FileHelper();
            string dirPath = Path.Combine(_testRootDir, "sub", "dir");

            helper.EnsureDirectory(dirPath);

            Assert.True(Directory.Exists(dirPath));
        }

        /// <summary>
        /// EnsureDirectory 对已存在的目录不报错
        /// </summary>
        [Fact]
        public void EnsureDirectory_ExistingDir_NoException()
        {
            var helper = new YF_FileHelper();
            Directory.CreateDirectory(_testRootDir);

            // 不应抛出异常
            helper.EnsureDirectory(_testRootDir);
            Assert.True(Directory.Exists(_testRootDir));
        }

        /// <summary>
        /// EnsureDirectoryForFile 创建文件所在目录
        /// </summary>
        [Fact]
        public void EnsureDirectoryForFile_CreatesParentDir()
        {
            var helper = new YF_FileHelper();
            string filePath = Path.Combine(_testRootDir, "sub", "test.txt");

            helper.EnsureDirectoryForFile(filePath);

            Assert.True(Directory.Exists(Path.GetDirectoryName(filePath)));
        }

        /// <summary>
        /// GetFileName 返回正确的文件名
        /// </summary>
        [Fact]
        public void GetFileName_ReturnsFileNameOnly()
        {
            var helper = new YF_FileHelper();
            string result = helper.GetFileName(@"C:\path\to\file.txt");

            Assert.Equal("file.txt", result);
        }

        /// <summary>
        /// GetFileName 只有文件名不包含路径时也正常
        /// </summary>
        [Fact]
        public void GetFileName_NoPath_ReturnsSame()
        {
            var helper = new YF_FileHelper();
            string result = helper.GetFileName("file.txt");

            Assert.Equal("file.txt", result);
        }

        /// <summary>
        /// FileExists 对不存在的文件返回 false
        /// </summary>
        [Fact]
        public void FileExists_NonExistentFile_ReturnsFalse()
        {
            var helper = new YF_FileHelper();
            bool exists = helper.FileExists(@"C:\nonexistent\file.xyz");

            Assert.False(exists);
        }

        /// <summary>
        /// FileExists 对存在的文件返回 true
        /// </summary>
        [Fact]
        public void FileExists_ExistingFile_ReturnsTrue()
        {
            var helper = new YF_FileHelper();
            string filePath = Path.Combine(_testRootDir, "test.txt");
            Directory.CreateDirectory(_testRootDir);
            File.WriteAllText(filePath, "hello");

            bool exists = helper.FileExists(filePath);

            Assert.True(exists);
        }

        /// <summary>
        /// FileExists 对空/null 路径返回 false
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void FileExists_EmptyOrNull_ReturnsFalse(string? path)
        {
            var helper = new YF_FileHelper();
            bool exists = helper.FileExists(path!);

            Assert.False(exists);
        }

        /// <summary>
        /// ReadAllText 读取文件内容
        /// </summary>
        [Fact]
        public void ReadAllText_ReadsContentCorrectly()
        {
            var helper = new YF_FileHelper();
            string filePath = Path.Combine(_testRootDir, "readme.txt");
            Directory.CreateDirectory(_testRootDir);
            File.WriteAllText(filePath, "Hello World");

            string content = helper.ReadAllText(filePath);

            Assert.Equal("Hello World", content);
        }

        /// <summary>
        /// WriteAllText 写入并验证内容
        /// </summary>
        [Fact]
        public void WriteAllText_WritesAndCanReadBack()
        {
            var helper = new YF_FileHelper();
            string filePath = Path.Combine(_testRootDir, "output.txt");

            helper.WriteAllText(filePath, "测试内容");

            Assert.True(File.Exists(filePath));
            Assert.Equal("测试内容", File.ReadAllText(filePath));
        }

        /// <summary>
        /// WriteAllText 自动创建不存在的目录
        /// </summary>
        [Fact]
        public void WriteAllText_AutoCreatesDirectory()
        {
            var helper = new YF_FileHelper();
            string filePath = Path.Combine(_testRootDir, "nested", "deep", "out.txt");

            helper.WriteAllText(filePath, "content");

            Assert.True(File.Exists(filePath));
        }

        /// <summary>
        /// CopyDirectory 源目录为空字符串应返回 false
        /// </summary>
        [Fact]
        public void CopyDirectory_EmptySource_ReturnsFalse()
        {
            var helper = new YF_FileHelper();
            bool result = helper.CopyDirectory("", Path.Combine(_testRootDir, "dest"));

            Assert.False(result);
        }

        /// <summary>
        /// CopyDirectory 目标目录为空字符串应返回 false
        /// </summary>
        [Fact]
        public void CopyDirectory_EmptyDest_ReturnsFalse()
        {
            var helper = new YF_FileHelper();
            bool result = helper.CopyDirectory(Path.Combine(_testRootDir, "src"), "");

            Assert.False(result);
        }

        /// <summary>
        /// CopyDirectory 源目录不存在应返回 false
        /// </summary>
        [Fact]
        public void CopyDirectory_SourceNotExists_ReturnsFalse()
        {
            var helper = new YF_FileHelper();
            bool result = helper.CopyDirectory(
                Path.Combine(_testRootDir, "nonexistent"),
                Path.Combine(_testRootDir, "dest"));

            Assert.False(result);
        }

        /// <summary>
        /// CopyDirectory 正常复制目录及其内容
        /// </summary>
        [Fact]
        public void CopyDirectory_CopiesFilesAndSubdirs()
        {
            var helper = new YF_FileHelper();
            string srcDir = Path.Combine(_testRootDir, "src");
            string subDir = Path.Combine(srcDir, "sub");
            string destDir = Path.Combine(_testRootDir, "dest");

            // 创建源目录结构
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(srcDir, "a.txt"), "a");
            File.WriteAllText(Path.Combine(subDir, "b.txt"), "b");

            bool result = helper.CopyDirectory(srcDir, destDir);

            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(destDir, "a.txt")));
            Assert.True(File.Exists(Path.Combine(destDir, "sub", "b.txt")));
            Assert.Equal("a", File.ReadAllText(Path.Combine(destDir, "a.txt")));
        }
    }
}
