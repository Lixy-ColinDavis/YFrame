using System.IO;
using System.Text;

namespace YFrame.Update.Services;

/// <summary>
/// 更新器文件日志：按天写入 Log\Update\yyyy-MM-dd.log。
/// 日志失败不能影响更新流程，所有异常都会被吞掉。
/// </summary>
public sealed class UpdateLogger
{
    private readonly string _filePath;
    private readonly object _sync = new();

    /// <summary>
    /// 创建日志器。
    /// </summary>
    /// <param name="appRoot">YFrame 根目录；日志写到其 Log\Update 下</param>
    public UpdateLogger(string appRoot)
    {
        string directory = Path.Combine(appRoot, "Log", "Update");
        try
        {
            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, $"{DateTime.Now:yyyy-MM-dd}.log");
        }
        catch
        {
            // 根目录不可写时退回临时目录，保证至少有诊断输出
            directory = Path.Combine(Path.GetTempPath(), "YFrame.Update", "Log");
            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, $"{DateTime.Now:yyyy-MM-dd}.log");
        }
    }

    /// <summary>当前日志文件完整路径。</summary>
    public string FilePath => _filePath;

    /// <summary>写入一条普通日志。</summary>
    public void Info(string message) => Write("INFO", message, null);

    /// <summary>写入一条警告日志。</summary>
    public void Warn(string message) => Write("WARN", message, null);

    /// <summary>写入一条错误日志，附带异常堆栈。</summary>
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var builder = new StringBuilder();
            builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            builder.Append(" [").Append(level).Append("] ");
            builder.AppendLine(message);
            if (exception != null)
                builder.AppendLine(exception.ToString());

            lock (_sync)
            {
                File.AppendAllText(_filePath, builder.ToString(), new UTF8Encoding(false));
            }
        }
        catch
        {
            // 日志本身失败不能阻断更新
        }
    }
}
