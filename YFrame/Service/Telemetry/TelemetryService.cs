using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using YF_Manager;

namespace YFrame.Service.Telemetry;

/// <summary>
/// YFrame APM OpenTelemetry 服务：默认关闭，开启后上报启动链路、插件、更新和异常。
/// </summary>
public static class TelemetryService
{
    private const string ActivitySourceName = "YFrame";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static TracerProvider? _tracerProvider;
    private static ILoggerFactory? _loggerFactory;
    private static ILogger? _logger;
    private static bool _initialized;

    /// <summary>是否已成功启用 APM 上报。</summary>
    public static bool IsEnabled => _initialized;

    /// <summary>初始化 OpenTelemetry；配置关闭或接入信息缺失时不启用。</summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        if (!Config.ApmEnabled)
            return;

        string endpoint = ApmBuiltInSettings.Endpoint;
        string token = ApmBuiltInSettings.Token;
        token = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(token))
        {
            Debug.WriteLine("[Telemetry] APM 已启用，但 Endpoint 或 Token 为空，跳过初始化。");
            return;
        }

        try
        {
            string serviceName = string.IsNullOrWhiteSpace(Config.ApmServiceName)
                ? "YFrame"
                : Config.ApmServiceName.Trim();
            string environment = string.IsNullOrWhiteSpace(Config.ApmEnvironment)
                ? "uat"
                : Config.ApmEnvironment.Trim();
            string version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";
            var endpointUri = new Uri(endpoint);

            ResourceBuilder resource = ResourceBuilder.CreateDefault()
                .AddService(serviceName, serviceVersion: version)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("token", token),
                    new KeyValuePair<string, object>("deployment.environment", environment),
                    new KeyValuePair<string, object>("host.name", Environment.MachineName),
                    new KeyValuePair<string, object>("telemetry.sdk.language", "dotnet")
                });

            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resource)
                .AddSource(ActivitySourceName)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = endpointUri;
                    options.Protocol = OtlpExportProtocol.Grpc;
                })
                .Build();

            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(MsLogLevel.Error);
                builder.AddOpenTelemetry(options =>
                {
                    options.SetResourceBuilder(resource);
                    options.IncludeScopes = true;
                    options.AddOtlpExporter(exporter =>
                    {
                        exporter.Endpoint = endpointUri;
                        exporter.Protocol = OtlpExportProtocol.Grpc;
                    });
                });
            });
            _logger = _loggerFactory.CreateLogger("YFrame");

            YF_Manager_Log.LogEmitted += OnLogEmitted;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _initialized = true;
            Debug.WriteLine("[Telemetry] APM OpenTelemetry 已启用。");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Telemetry] 初始化失败: {ex.Message}");
            DisposeProviders();
        }
    }

    /// <summary>开始一个 Activity；未启用时返回 null。</summary>
    /// <param name="name">Span 名称</param>
    /// <param name="tags">可选标签</param>
    /// <returns>Activity 或 null</returns>
    public static Activity? StartActivity(
        string name,
        params (string Key, object? Value)[] tags)
    {
        if (!_initialized)
            return null;

        Activity? activity = ActivitySource.StartActivity(name, ActivityKind.Internal);
        if (activity == null)
            return null;

        foreach ((string key, object? value) in tags)
        {
            if (value != null)
                activity.SetTag(key, value);
        }

        return activity;
    }

    /// <summary>记录异常到当前 Activity 和 OTLP 日志。</summary>
    /// <param name="exception">异常</param>
    /// <param name="source">异常来源</param>
    public static void TrackException(Exception exception, string source)
    {
        if (exception == null)
            return;

        Activity? activity = Activity.Current;
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.AddEvent(new ActivityEvent("exception",
            DateTimeOffset.UtcNow,
            new ActivityTagsCollection
            {
                ["exception.type"] = exception.GetType().FullName ?? exception.GetType().Name,
                ["exception.message"] = exception.Message,
                ["exception.stacktrace"] = exception.StackTrace ?? string.Empty,
                ["exception.source"] = source
            }));

        _logger?.LogError(exception, "{Source}: {Message}", source, exception.Message);
    }

    /// <summary>记录一条结构化事件日志；当前只允许 Error 上报。</summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">日志内容</param>
    public static void TrackLog(MsLogLevel level, string message)
    {
        if (level != MsLogLevel.Error)
            return;

        _logger?.LogError("{Message}", message);
    }

    /// <summary>停止上报并释放 SDK。</summary>
    public static void Shutdown()
    {
        if (!_initialized)
            return;

        YF_Manager_Log.LogEmitted -= OnLogEmitted;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _tracerProvider?.Dispose();
        _tracerProvider = null;
        _loggerFactory?.Dispose();
        _loggerFactory = null;
        _logger = null;
        _initialized = false;
    }

    /// <summary>释放可能已部分创建的 OpenTelemetry 提供程序。</summary>
    private static void DisposeProviders()
    {
        _tracerProvider?.Dispose();
        _tracerProvider = null;
        _loggerFactory?.Dispose();
        _loggerFactory = null;
        _logger = null;
    }

    /// <summary>将 YF_Manager 的 Error 日志桥接到 OpenTelemetry。</summary>
    private static void OnLogEmitted(YF_LogRecord record)
    {
        if (_logger == null)
            return;

        // 只上传 Error；本地日志仍保留 Info/Command/Tcp/Debug
        if (!string.Equals(record.Level, "[Error]", StringComparison.Ordinal))
            return;

        CreateErrorSpan(record);

        _logger.LogError("[{Component}/{Id}] {Message}",
            record.Name, record.Id, record.Message);
    }

    /// <summary>
    /// 将 Error 日志转换成 Error.&lt;类别&gt; Span，便于在 APM 链路追踪和错误分析中查看。
    /// </summary>
    /// <param name="record">错误日志记录</param>
    private static void CreateErrorSpan(YF_LogRecord record)
    {
        if (!_initialized)
            return;

        string category = ClassifyError(record);
        using Activity? activity = ActivitySource.StartActivity(
            $"Error.{category}", ActivityKind.Internal);
        if (activity == null)
            return;

        activity.SetTag("error.category", category);
        activity.SetTag("error.source", record.Id);
        activity.SetTag("error.component", record.Name);
        activity.SetTag("error.message", record.Message);
        activity.SetTag("error.type", record.Exception?.GetType().FullName ?? "LogError");
        activity.SetStatus(ActivityStatusCode.Error, record.Message);

        Match statusMatch = Regex.Match(record.Message, @"\bHTTP\s+(\d{3})\b",
            RegexOptions.IgnoreCase);
        if (statusMatch.Success && int.TryParse(statusMatch.Groups[1].Value, out int status))
            activity.SetTag("error.http_status", status);

        if (record.Exception != null)
        {
            activity.AddEvent(new ActivityEvent("exception",
                DateTimeOffset.UtcNow,
                new ActivityTagsCollection
                {
                    ["exception.type"] = record.Exception.GetType().FullName
                                         ?? record.Exception.GetType().Name,
                    ["exception.message"] = record.Exception.Message,
                    ["exception.stacktrace"] = record.Exception.StackTrace ?? string.Empty,
                    ["exception.source"] = record.Id
                }));
        }
        else
        {
            activity.AddEvent(new ActivityEvent("error",
                DateTimeOffset.UtcNow,
                new ActivityTagsCollection
                {
                    ["error.category"] = category,
                    ["error.message"] = record.Message,
                    ["error.source"] = record.Id
                }));
        }
    }

    /// <summary>
    /// 根据异常类型名、来源和消息归类错误；异常类型直接使用类型名并移除 Exception 后缀。
    /// </summary>
    /// <param name="record">错误日志记录</param>
    /// <returns>错误类别名称，不包含 Error. 前缀</returns>
    private static string ClassifyError(YF_LogRecord record)
    {
        string source = $"{record.Id} {record.Name}";
        string message = record.Message ?? string.Empty;

        // 上传业务优先归为 upload；即使底层是 401/权限错误也保留上传入口语义
        if (source.Contains("cloudbase", StringComparison.OrdinalIgnoreCase)
            || source.Contains("upload", StringComparison.OrdinalIgnoreCase)
            || message.Contains("上传", StringComparison.Ordinal)
            || message.Contains("upload", StringComparison.OrdinalIgnoreCase))
            return "upload";

        // 有真实异常时直接使用类型名：NullReferenceException -> NullReference
        if (record.Exception != null)
        {
            const string exceptionSuffix = "Exception";
            string typeName = record.Exception.GetType().Name;
            if (typeName.EndsWith(exceptionSuffix, StringComparison.Ordinal))
                typeName = typeName[..^exceptionSuffix.Length];

            return string.IsNullOrWhiteSpace(typeName) ? "Unknown" : typeName;
        }

        if (message.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("401", StringComparison.Ordinal)
            || message.Contains("403", StringComparison.Ordinal)
            || message.Contains("权限", StringComparison.Ordinal))
            return "PermissionDenied";

        if (message.Contains("不存在", StringComparison.Ordinal)
            || message.Contains("未找到", StringComparison.Ordinal)
            || message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return "NotFound";

        if (message.Contains("索引", StringComparison.Ordinal)
            || message.Contains("越界", StringComparison.Ordinal)
            || message.Contains("index out of range", StringComparison.OrdinalIgnoreCase)
            || message.Contains("outside the bounds", StringComparison.OrdinalIgnoreCase))
            return "IndexOutOfRange";

        if (message.Contains("out of the range", StringComparison.OrdinalIgnoreCase))
            return "ArgumentOutOfRange";

        if (message.Contains("not present in the dictionary", StringComparison.OrdinalIgnoreCase))
            return "KeyNotFound";

        if (message.Contains("Object reference not set", StringComparison.OrdinalIgnoreCase))
            return "NullReference";

        if (message.Contains("Could not find file", StringComparison.OrdinalIgnoreCase))
            return "FileNotFound";

        if (message.Contains("Access to the path", StringComparison.OrdinalIgnoreCase))
            return "Unauthorized";

        return "Unknown";
    }

    /// <summary>记录 AppDomain 未处理异常。</summary>
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            TrackException(exception, "appdomain.unhandled");
    }

    /// <summary>记录未观察到的 Task 异常。</summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TrackException(e.Exception, "task.unobserved");
        e.SetObserved();
    }
}
