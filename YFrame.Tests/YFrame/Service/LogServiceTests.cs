using YF_Manager;

namespace YFrame.Tests.YFrame.Service
{
    /// <summary>
    /// LogService 单元测试
    /// </summary>
    public class LogServiceTests
    {
        #region 测试基础设施

        private readonly YF_Manager_Log _logger;
        private readonly YF_Messenger _messenger;

        public LogServiceTests()
        {
            // 使用三参数构造函数，传入普通 FileHelper 实例避免触发 AOP 代理
            _logger = new YF_Manager_Log("LogService测试", "Test", new YF_FileHelper());
            _messenger = new YF_Messenger();
        }

        #endregion

        #region 构造函数测试

        /// <summary>
        /// 构造函数应注册 LogAppendMessage 消息处理器
        /// </summary>
        [Fact]
        public void Constructor_RegistersLogAppendMessageHandler()
        {
            var service = new LogService(_logger, _messenger);

            _messenger.Send(new LogAppendMessage("测试消息"));

            string fullText = service.GetFullText();
            Assert.Contains("测试消息", fullText);
        }

        /// <summary>
        /// 构造函数应注册 LogClearMessage 消息处理器
        /// </summary>
        [Fact]
        public void Constructor_RegistersLogClearMessageHandler()
        {
            var service = new LogService(_logger, _messenger);

            service.AppendLog("待清除");
            Assert.Equal(1, service.GetLineCount());

            _messenger.Send(new LogClearMessage());

            Assert.Equal(0, service.GetLineCount());
            Assert.Empty(service.GetFullText());
        }

        #endregion

        #region AppendLog 测试

        /// <summary>
        /// 追加日志文本到缓冲区
        /// </summary>
        [Fact]
        public void AppendLog_AddsTextToBuffer()
        {
            var service = new LogService(_logger, _messenger);

            service.AppendLog("第一行");
            service.AppendLog("第二行");

            string fullText = service.GetFullText();
            Assert.Contains("第一行", fullText);
            Assert.Contains("第二行", fullText);
            Assert.Equal(2, service.GetLineCount());
        }

        /// <summary>
        /// 追加日志时触发 OnLogTextChanged 回调
        /// </summary>
        [Fact]
        public void AppendLog_TriggersOnLogTextChanged()
        {
            var service = new LogService(_logger, _messenger);
            string? receivedText = null;
            service.OnLogTextChanged = text => receivedText = text;

            service.AppendLog("回调测试");

            Assert.NotNull(receivedText);
            Assert.Contains("回调测试", receivedText);
        }

        /// <summary>
        /// 追加日志达到上限（500行）后自动裁剪旧行
        /// </summary>
        [Fact]
        public void AppendLog_TrimsWhenExceedsMaxLines()
        {
            var service = new LogService(_logger, _messenger);

            for (int i = 0; i < 501; i++)
            {
                service.AppendLog($"行 {i}");
            }

            Assert.True(service.GetLineCount() <= 500);

            string fullText = service.GetFullText();
            Assert.DoesNotContain("行 0", fullText);
            Assert.Contains("行 500", fullText);
        }

        /// <summary>
        /// 追加空字符串不引发异常
        /// </summary>
        [Fact]
        public void AppendLog_EmptyString_DoesNotThrow()
        {
            var service = new LogService(_logger, _messenger);

            var exception = Record.Exception(() => service.AppendLog(""));
            Assert.Null(exception);
            Assert.Equal(1, service.GetLineCount());
        }

        /// <summary>
        /// 追加 null 值不引发异常
        /// </summary>
        [Fact]
        public void AppendLog_NullValue_DoesNotThrow()
        {
            var service = new LogService(_logger, _messenger);

            var exception = Record.Exception(() => service.AppendLog(null!));
            Assert.Null(exception);
        }

        /// <summary>
        /// 追加包含换行的多行文本，可正确计数
        /// </summary>
        [Fact]
        public void AppendLog_MultiLineText_CountsCorrectly()
        {
            var service = new LogService(_logger, _messenger);

            service.AppendLog("第一段\n第二段\n第三段");

            Assert.Equal(1, service.GetLineCount());
        }

        #endregion

        #region ClearLog 测试

        /// <summary>
        /// 清除日志缓冲区
        /// </summary>
        [Fact]
        public void ClearLog_ClearsAllBuffers()
        {
            var service = new LogService(_logger, _messenger);

            service.AppendLog("消息1");
            service.AppendLog("消息2");
            service.AppendLog("消息3");

            service.ClearLog();

            Assert.Equal(0, service.GetLineCount());
            Assert.Empty(service.GetFullText());
        }

        /// <summary>
        /// 清除日志后 GetFullText 返回空字符串
        /// </summary>
        [Fact]
        public void ClearLog_ReturnsEmptyString()
        {
            var service = new LogService(_logger, _messenger);

            service.AppendLog("内容");
            string result = service.ClearLog();

            Assert.Empty(result);
            Assert.Equal(0, service.GetLineCount());
        }

        /// <summary>
        /// 重复清除无副作用
        /// </summary>
        [Fact]
        public void ClearLog_CalledTwice_NoSideEffects()
        {
            var service = new LogService(_logger, _messenger);

            var exception = Record.Exception(() =>
            {
                service.ClearLog();
                service.ClearLog();
            });

            Assert.Null(exception);
            Assert.Equal(0, service.GetLineCount());
        }

        #endregion

        #region GetFullText 测试

        /// <summary>
        /// 获取当前完整日志文本
        /// </summary>
        [Fact]
        public void GetFullText_ReturnsBuffer()
        {
            var service = new LogService(_logger, _messenger);

            service.AppendLog("内容");
            string text = service.GetFullText();

            Assert.Contains("内容", text);
        }

        /// <summary>
        /// 未追加任何内容时返回空字符串
        /// </summary>
        [Fact]
        public void GetFullText_ReturnsEmptyWhenNoLogs()
        {
            var service = new LogService(_logger, _messenger);

            string text = service.GetFullText();

            Assert.Empty(text);
        }

        #endregion

        #region GetLineCount 测试

        /// <summary>
        /// 获取当前日志行数
        /// </summary>
        [Fact]
        public void GetLineCount_ReturnsCorrectCount()
        {
            var service = new LogService(_logger, _messenger);

            Assert.Equal(0, service.GetLineCount());

            service.AppendLog("A");
            Assert.Equal(1, service.GetLineCount());

            service.AppendLog("B");
            Assert.Equal(2, service.GetLineCount());
        }

        #endregion

        #region 线程安全测试

        /// <summary>
        /// 多线程并发追加日志不崩溃，行数统计最终一致
        /// </summary>
        [Fact]
        public async Task AppendLog_ConcurrentAccess_IsThreadSafe()
        {
            var service = new LogService(_logger, _messenger);
            int threadCount = 4;
            int logsPerThread = 50;
            var tasks = new List<Task>();

            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < logsPerThread; i++)
                    {
                        service.AppendLog($"线程{threadId}-消息{i}");
                    }
                }));
            }

            await Task.WhenAll(tasks);

            Assert.Equal(threadCount * logsPerThread, service.GetLineCount());
        }

        #endregion
    }
}
