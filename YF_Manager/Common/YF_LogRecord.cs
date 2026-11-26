namespace YF_Manager;

/// <summary>
/// 从 YF_Manager_Log 发出的轻量结构化日志记录，供主程序上报 APM。
/// 不依赖任何云 SDK，避免影响插件。
/// </summary>
public sealed class YF_LogRecord
{
    /// <summary>日志时间（本地时间）。</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>日志组件名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>日志组件 ID。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>日志级别，例如 [Info]、[Error]。</summary>
    public string Level { get; init; } = "[Info]";

    /// <summary>日志正文。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>关联异常；没有异常对象时为空。</summary>
    public Exception? Exception { get; init; }
}
