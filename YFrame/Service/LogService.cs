using System.Text;
using System.Windows;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// 日志面板服务 — 从 MainWindowViewModel 中提取的日志管理逻辑
    /// 职责：管理日志缓冲区（500行上限）、追加日志、清除日志
    /// 通过 YF_Messenger 接收日志消息，通过回调更新 UI 绑定属性
    /// 
    /// 可独立单元测试：Mock YF_Messenger，发送 LogAppendMessage 后验证缓冲区内容
    /// </summary>
    public class LogService
    {
        #region 日志缓冲区

        /// <summary>日志字符串构建器（内存缓冲区）</summary>
        private readonly StringBuilder _logBuilder = new();

        /// <summary>当前日志行数</summary>
        private int _logLineCount = 0;

        /// <summary>最大日志行数上限</summary>
        private const int MaxLogLines = 500;

        /// <summary>日志缓冲区线程锁</summary>
        private readonly object _logLock = new();

        #endregion

        #region 回调

        /// <summary>
        /// 当日志内容变更时回调，由 MainWindowViewModel 订阅以更新 LogText 绑定属性
        /// </summary>
        public Action<string>? OnLogTextChanged;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建日志面板服务
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="messenger">消息中介（DI 注入）</param>
        public LogService(YF_Manager_Log logger, YF_Messenger messenger)
        {
            // 订阅 Mediator 消息：任意组件发送的日志追加请求
            messenger.Register<LogAppendMessage>(msg =>
            {
                AppendLog(msg.Text);
            });

            // 订阅 Mediator 消息：任意组件发送的日志清除请求
            messenger.Register<LogClearMessage>(_ =>
            {
                ClearLog();
                logger.LogInfo("日志面板已清除（通过 Mediator）");
            });

            logger.LogInfo("LogService 初始化完成，已订阅日志消息");
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 追加一条日志到缓冲区，超出上限时从头部裁剪
        /// </summary>
        /// <param name="msg">日志文本</param>
        public void AppendLog(string msg)
        {
            lock (_logLock)
            {
                _logBuilder.AppendLine(msg);
                _logLineCount++;

                // 从头部裁剪超出行数
                while (_logLineCount > MaxLogLines)
                {
                    var text = _logBuilder.ToString();
                    var newlineIdx = text.IndexOf('\n');
                    if (newlineIdx < 0) break;
                    _logBuilder.Remove(0, newlineIdx + 1);
                    _logLineCount--;
                }
            }

            // 取出当前完整文本用于刷新
            string currentText;
            lock (_logLock) { 
                currentText = _logBuilder.ToString(); 
            }

          
            // 避免跨线程直接更新控件导致 InvalidOperationException 甚至崩溃。
            // 若当前无可用的 Dispatcher（如单元测试环境），则退化为同步直接调用，保证行为一致。
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.Invoke(() => OnLogTextChanged?.Invoke(currentText));
            else
                OnLogTextChanged?.Invoke(currentText);
        }

        /// <summary>
        /// 清空日志缓冲区
        /// </summary>
        public string ClearLog()
        {
            lock (_logLock)
            {
                _logBuilder.Clear();
                _logLineCount = 0;
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取当前完整日志文本（用于测试或外部读取）
        /// </summary>
        public string GetFullText()
        {
            lock (_logLock)
            {
                return _logBuilder.ToString();
            }
        }

        /// <summary>
        /// 获取当前日志行数（用于测试验证）
        /// </summary>
        public int GetLineCount()
        {
            lock (_logLock)
            {
                return _logLineCount;
            }
        }

        #endregion
    }
}
