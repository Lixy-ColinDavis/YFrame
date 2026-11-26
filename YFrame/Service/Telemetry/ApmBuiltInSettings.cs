namespace YFrame.Service.Telemetry;

/// <summary>
/// 内置 APM 接入配置。
/// 注意：Endpoint/Token 固定写入源码，不再从 config.conf 读取。
/// 说明：个人项目，Demo环境，无安全风险可将Key上传到Git,但不落地到本机磁盘
/// </summary>
internal static class ApmBuiltInSettings
{
    /// <summary>APM OTLP 接入点。</summary>
    internal const string Endpoint = "https://ap-guangzhou.apm.tencentcs.com:4320";

    /// <summary>APM 应用 Token。</summary>
    internal const string Token = "uRNEPJAeWlEeOMRqXykE";
}