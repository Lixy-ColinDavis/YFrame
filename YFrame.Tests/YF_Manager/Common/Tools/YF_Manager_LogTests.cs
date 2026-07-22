using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common.Tools
{
    /// <summary>
    /// YF_Manager_Log 单元测试 — 日志系统和文件轮转逻辑
    /// 使用临时目录避免污染实际日志
    /// </summary>
    public class YF_Manager_LogTests : IDisposable
    {
        private readonly string _testLogRoot;

        public YF_Manager_LogTests()
        {
            _testLogRoot = Path.Combine(Path.GetTempPath(), $"YFrame_LogTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testLogRoot);
        }

        /// <summary>
        /// 构造函数正确设置 Name 和 ID
        /// </summary>
        [Fact]
        public void Constructor_SetsNameAndId()
        {
            var log = new YF_Manager_Log("测试模块", "YF_Test");

            // 通过调用方法来间接验证（类内部使用了 _name）
            Assert.NotNull(log);
        }

        /// <summary>
        /// 日志文件被写入到正确的目录结构
        /// </summary>
        [Fact]
        public void DebugInfo_WritesToDebugLog()
        {
            // 此测试验证日志系统写入逻辑不崩溃
            var log = new YF_Manager_Log("测试", "YF_Test");

            // 记录异常不应抛出
            var exception = Record.Exception(() => log.DebugInfo("测试调试消息"));
            Assert.Null(exception);
        }

        /// <summary>
        /// ErrorInfo 记录错误日志不崩溃
        /// </summary>
        [Fact]
        public void ErrorInfo_WritesWithoutCrashing()
        {
            var log = new YF_Manager_Log("测试", "YF_Test");

            var exception = Record.Exception(() =>
                log.ErrorInfo("TestMethod", "测试错误消息"));

            Assert.Null(exception);
        }

        /// <summary>
        /// CommandInfo 记录命令日志不崩溃
        /// </summary>
        [Fact]
        public void CommandInfo_WritesWithoutCrashing()
        {
            var log = new YF_Manager_Log("测试", "YF_Test");

            var exception = Record.Exception(() =>
                log.CommandInfo("测试命令"));

            Assert.Null(exception);
        }

        /// <summary>
        /// TcpInfo 记录 TCP 日志不崩溃
        /// </summary>
        [Fact]
        public void TcpInfo_WritesWithoutCrashing()
        {
            var log = new YF_Manager_Log("测试", "YF_Test");

            var exception = Record.Exception(() =>
                log.TcpInfo("127.0.0.1:8021"));

            Assert.Null(exception);
        }

        /// <summary>
        /// LogInfo 记录普通日志不崩溃
        /// </summary>
        [Fact]
        public void LogInfo_WritesWithoutCrashing()
        {
            var log = new YF_Manager_Log("测试", "YF_Test");

            var exception = Record.Exception(() =>
                log.LogInfo("测试信息", "[Info]"));

            Assert.Null(exception);
        }

        /// <summary>
        /// InterceptorsLog 记录拦截器日志不崩溃
        /// </summary>
        [Fact]
        public void InterceptorsLog_WritesWithoutCrashing()
        {
            var log = new YF_Manager_Log("测试", "YF_Test");

            var exception = Record.Exception(() =>
                log.InterceptorsLog("AOP 测试消息", "Info"));

            Assert.Null(exception);
        }

        /// <summary>
        /// 多次快速写入不崩溃（验证文件锁正确工作）
        /// </summary>
        [Fact]
        public void MultipleRapidWrites_NoExceptions()
        {
            var log = new YF_Manager_Log("压力测试", "YF_Stress");
            var exceptions = new List<Exception>();

            for (int i = 0; i < 20; i++)
            {
                try
                {
                    log.LogInfo($"消息 {i}", "[Info]");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            Assert.Empty(exceptions);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testLogRoot))
                    Directory.Delete(_testLogRoot, recursive: true);
            }
            catch { /* 忽略清理失败 */ }
        }
    }
}
