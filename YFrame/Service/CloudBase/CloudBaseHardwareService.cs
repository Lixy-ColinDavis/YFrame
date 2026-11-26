using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YF_Manager;
using YFrame.Model;
using YFrame.Service.Telemetry;

namespace YFrame.Service.CloudBase;

/// <summary>
/// 将硬件扫描结果写入 CloudBase PostgreSQL 的 yframe_hardware 表。
/// 使用 CloudBase PostgREST HTTP API，不引入额外 SDK。
/// </summary>
public static class CloudBaseHardwareService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    /// <summary>
    /// 上传一次硬件快照。
    /// 失败只记录日志，不抛给硬件页面，避免影响本地扫描结果。
    /// </summary>
    /// <param name="snapshot">硬件扫描快照</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task UploadAsync(
        HardwareInfoSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        if (!Config.CloudBaseEnabled)
            return;

        string apiKey = Environment.GetEnvironmentVariable("CLOUDBASE_API_KEY")
                        ?? Config.CloudBaseApiKey;
        apiKey = apiKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            DebugWrite("CloudBase 已启用，但 API Key 为空，跳过上传。");
            return;
        }

        string envId = Config.CloudBaseEnvId.Trim();
        string table = Config.CloudBaseHardwareTable.Trim();
        if (string.IsNullOrWhiteSpace(envId) || string.IsNullOrWhiteSpace(table))
        {
            DebugWrite("CloudBase 环境 ID 或表名为空，跳过上传。");
            return;
        }

        // 独立 Span：即使 APM 日志页不可用，也能在链路追踪中看到上传结果
        using Activity? activity = TelemetryService.StartActivity("cloudbase.upload");
        activity?.SetTag("cloudbase.env_id", envId);
        activity?.SetTag("cloudbase.table", table);

        try
        {
            string baseUrl = $"https://{envId}.api.tcloudbasegateway.com/v1/rdb/rest/{Uri.EscapeDataString(table)}";
            string deviceName = BuildDeviceName(snapshot);
            string filter = $"device_name=eq.{Uri.EscapeDataString(deviceName)}";

            // 先按 device_name 查询；存在则 PATCH，不存在则 POST
            bool exists = await ExistsAsync(baseUrl, filter, apiKey, cancellationToken);
            string requestUrl = exists ? $"{baseUrl}?{filter}" : baseUrl;
            HttpMethod method = exists ? HttpMethod.Patch : HttpMethod.Post;
            activity?.SetTag("cloudbase.operation", exists ? "update" : "insert");
            activity?.SetTag("cloudbase.device_name", deviceName);

            // 只写表结构中的业务字段；id/created_at 使用数据库默认值
            var row = new
            {
                device_name = deviceName,
                cpu = BuildCpu(snapshot),
                gpu = BuildGpu(snapshot),
                memory = BuildMemory(snapshot),
                disk = BuildDisk(snapshot),
                updatetime = DateTimeOffset.UtcNow
            };

            using var request = new HttpRequestMessage(method, requestUrl)
            {
                Content = JsonContent.Create(row)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.TryAddWithoutValidation("Prefer", "return=representation");

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (body.Length > 1000)
                    body = body[..1000];

                activity?.SetTag("cloudbase.success", false);
                activity?.SetTag("cloudbase.http_status", (int)response.StatusCode);
                activity?.SetTag("cloudbase.error", body);
                activity?.SetStatus(ActivityStatusCode.Error,
                    $"CloudBase HTTP {(int)response.StatusCode}");
                activity?.AddEvent(new ActivityEvent("exception",
                    DateTimeOffset.UtcNow,
                    new ActivityTagsCollection
                    {
                        ["exception.type"] = "CloudBaseHttpException",
                        ["exception.message"] = body
                    }));

                YF_Manager_Main.logger?.ErrorInfo(
                    "CloudBaseHardware",
                    $"上传失败 HTTP {(int)response.StatusCode}: {body}");
            }
            else
            {
                activity?.SetTag("cloudbase.success", true);
            }
        }
        catch (Exception ex)
        {
            // 网络、鉴权、表结构变化都不能影响硬件扫描页面
            activity?.SetTag("cloudbase.success", false);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception",
                DateTimeOffset.UtcNow,
                new ActivityTagsCollection
                {
                    ["exception.type"] = ex.GetType().FullName ?? ex.GetType().Name,
                    ["exception.message"] = ex.Message,
                    ["exception.stacktrace"] = ex.StackTrace ?? string.Empty
                }));

            YF_Manager_Main.logger?.ErrorInfo("CloudBaseHardware", ex.Message);
        }
    }

    /// <summary>
    /// 查询指定 device_name 是否已存在。
    /// </summary>
    /// <param name="baseUrl">表 REST 地址</param>
    /// <param name="filter">PostgREST 过滤条件</param>
    /// <param name="apiKey">CloudBase API Key</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>存在返回 true；不存在返回 false</returns>
    private static async Task<bool> ExistsAsync(
        string baseUrl,
        string filter,
        string apiKey,
        CancellationToken cancellationToken)
    {
        string url = $"{baseUrl}?select=id&{filter}&limit=1";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"CloudBase 查询失败 HTTP {(int)response.StatusCode}: {body}");

        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.ValueKind == JsonValueKind.Array
               && document.RootElement.GetArrayLength() > 0;
    }

    /// <summary>构造设备名称，优先使用 WMI 计算机名。</summary>
    private static string BuildDeviceName(HardwareInfoSnapshot snapshot)
    {
        string name = snapshot.System.ComputerName;
        return string.IsNullOrWhiteSpace(name) ? Environment.MachineName : name;
    }

    /// <summary>构造 CPU 摘要文本。</summary>
    private static string BuildCpu(HardwareInfoSnapshot snapshot)
    {
        var cpu = snapshot.Cpu;
        if (string.IsNullOrWhiteSpace(cpu.Name))
            return "未知";

        string frequency = cpu.ClockMHz > 0 ? $" @ {cpu.ClockMHz}MHz" : string.Empty;
        return $"{cpu.Name} ({cpu.Cores}C/{cpu.Threads}T){frequency}";
    }

    /// <summary>构造全部 GPU 名称，多显卡用分隔符连接。</summary>
    private static string BuildGpu(HardwareInfoSnapshot snapshot)
    {
        string names = string.Join(" | ", snapshot.Gpus
            .Select(g => g.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name)));
        return string.IsNullOrWhiteSpace(names) ? "未检测到" : names;
    }

    /// <summary>构造内存容量与内存条数量摘要。</summary>
    private static string BuildMemory(HardwareInfoSnapshot snapshot)
    {
        double totalGb = snapshot.Memory.TotalBytes / 1024d / 1024d / 1024d;
        int moduleCount = snapshot.Memory.Modules.Count;
        return moduleCount > 0
            ? $"{totalGb:F1} GB ({moduleCount} 条)"
            : $"{totalGb:F1} GB";
    }

    /// <summary>构造全部磁盘摘要，多磁盘用分隔符连接。</summary>
    private static string BuildDisk(HardwareInfoSnapshot snapshot)
    {
        if (snapshot.Disks.Count == 0)
            return "未检测到";

        return string.Join(" | ", snapshot.Disks.Select(disk =>
        {
            double sizeGb = disk.SizeBytes / 1024d / 1024d / 1024d;
            string bus = !string.IsNullOrWhiteSpace(disk.BusTypeText)
                ? disk.BusTypeText
                : disk.InterfaceType;
            string media = !string.IsNullOrWhiteSpace(disk.MediaTypeText)
                ? disk.MediaTypeText
                : disk.MediaType;
            string model = string.IsNullOrWhiteSpace(disk.Model) ? "未知型号" : disk.Model;
            return $"{model} {sizeGb:F1}GB {bus} {media}".Trim();
        }));
    }

    /// <summary>输出调试信息，便于定位未启用或缺少配置的情况。</summary>
    private static void DebugWrite(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[CloudBaseHardware] {message}");
    }
}
