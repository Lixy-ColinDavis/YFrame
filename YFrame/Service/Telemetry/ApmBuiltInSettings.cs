namespace YFrame.Service.Telemetry;

/// <summary>
/// 内置 APM 接入配置。
/// Endpoint 属于非敏感配置；Token 从 DPAPI 读取，不再写入源码或 config.conf。
/// </summary>
internal static class ApmBuiltInSettings
{
    /// <summary>APM OTLP 接入点。</summary>
    internal const string Endpoint = "https://ap-guangzhou.apm.tencentcs.com:4320";
}