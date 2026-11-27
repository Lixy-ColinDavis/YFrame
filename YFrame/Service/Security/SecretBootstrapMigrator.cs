using System;
using System.IO;
using System.Text.Json;
using YF_Manager;

namespace YFrame.Service.Security;

/// <summary>
/// 安装/更新引导迁移器。
/// 安装包或更新包在程序根目录放置 YFrame.secrets.bootstrap，
/// 新版本首次启动时自动转成 DPAPI CurrentUser 密文。
/// </summary>
public static class SecretBootstrapMigrator
{
    /// <summary>引导文件名；必须位于程序根目录，不能放 Config。</summary>
    public const string BootstrapFileName = "YFrame.secrets.bootstrap";

    /// <summary>
    /// 如果当前用户尚未配置密钥，则读取引导文件并写入 DPAPI。
    /// </summary>
    /// <returns>本次是否完成迁移</returns>
    public static bool TryProvision()
    {
        try
        {
            // 已有可用密钥时不覆盖，避免升级覆盖用户自己的配置
            if (DpapiSecretStore.IsConfigured())
                return false;

            string bootstrapPath = Path.Combine(
                AppContext.BaseDirectory,
                BootstrapFileName);

            if (!File.Exists(bootstrapPath))
                return false;

            string json = File.ReadAllText(bootstrapPath);
            SecretPayload? payload = JsonSerializer.Deserialize<SecretPayload>(json);
            if (payload == null || payload.Version <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(payload.ApmToken)
                && string.IsNullOrWhiteSpace(payload.CloudBaseApiKey))
                return false;

            DpapiSecretStore.Save(payload);
            YF_Manager_Main.logger?.LogInfo(
                "SecretBootstrap",
                "检测到安装/更新引导文件，已写入当前用户 DPAPI 配置");

            // 按用户安装时程序目录可写，迁移后删除引导文件；
            // 按机器安装（Program Files）时保留，供其他用户首次迁移。
            if (IsPerUserInstall())
                TryDeleteBootstrap(bootstrapPath);

            return true;
        }
        catch (Exception ex)
        {
            // 引导失败不能阻止程序启动，后续按未配置处理
            YF_Manager_Main.logger?.ErrorInfo("SecretBootstrap", ex.Message);
            return false;
        }
    }

    /// <summary>判断程序是否位于当前用户的 LocalAppData 下。</summary>
    private static bool IsPerUserInstall()
    {
        string baseDir = AppContext.BaseDirectory;
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        return !string.IsNullOrWhiteSpace(localAppData)
               && baseDir.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>删除引导文件；失败时保留，避免影响下一次启动。</summary>
    private static void TryDeleteBootstrap(string bootstrapPath)
    {
        try
        {
            File.Delete(bootstrapPath);
        }
        catch (Exception ex)
        {
            // 文件被占用或权限不足时保留，下一次启动会再次尝试
            System.Diagnostics.Debug.WriteLine(
                $"[SecretBootstrap] 删除引导文件失败: {ex.Message}");
        }
    }
}
