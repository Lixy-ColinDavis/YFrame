using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using YFrame.Installer.Models;

namespace YFrame.Installer.Services;

/// <summary>
/// 安装核心服务：负责文件复制、快捷方式创建、注册表卸载信息写入
/// 仅安装框架本体，不安装插件和模型文件
/// </summary>
public class InstallService
{
    /// <summary>
    /// 执行安装流程：复制框架核心文件、创建快捷方式、写入注册表
    /// </summary>
    /// <param name="payloadPath">payload 根目录（包含 core/ 子目录）</param>
    /// <param name="config">安装配置（路径、快捷方式选项）</param>
    /// <param name="progressCallback">进度回调（0.0 ~ 1.0）</param>
    /// <param name="statusCallback">状态文本回调</param>
    public async Task InstallAsync(string payloadPath, InstallConfig config,
        IProgress<double>? progressCallback = null, IProgress<string>? statusCallback = null)
    {
        var installPath = config.InstallPath;
        var corePath = Path.Combine(payloadPath, "core");

        // 复制核心框架文件
        statusCallback?.Report("正在安装 YFrame 框架文件...");
        await Task.Run(() => CopyDirectory(corePath, installPath));
        progressCallback?.Report(0.6);

        // 创建快捷方式
        var exePath = Path.Combine(installPath, "YFrame.exe");
        if (File.Exists(exePath))
        {
            if (config.CreateDesktopShortcut)
            {
                statusCallback?.Report("正在创建桌面快捷方式...");
                CreateDesktopShortcut(exePath, "YFrame 工具集", installPath);
            }
            if (config.AddToStartMenu)
            {
                statusCallback?.Report("正在添加到开始菜单...");
                CreateStartMenuEntry(exePath, "YFrame 工具集", installPath);
            }
        }

        progressCallback?.Report(0.9);

        // 注册表卸载信息
        statusCallback?.Report("正在注册卸载信息...");
        WriteUninstallRegistry(installPath, exePath);

        progressCallback?.Report(1.0);
        statusCallback?.Report("安装完成！");
    }

    /// <summary>
    /// 检测 .NET Desktop Runtime 8.0 是否已安装
    /// </summary>
    public bool CheckDotNetRuntime()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet", Arguments = "--list-runtimes",
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output.Contains("Microsoft.WindowsDesktop.App 8.");
        }
        catch { return false; }
    }

    /// <summary>
    /// 获取 .NET 8.0 运行时下载地址
    /// </summary>
    public string GetDotNetRuntimeUrl() => "https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0";

    #region File Operations

    /// <summary>
    /// 递归复制目录及所有文件
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir)) return;
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }

    #endregion

    #region Shortcuts

    /// <summary>
    /// 在桌面创建快捷方式
    /// </summary>
    public void CreateDesktopShortcut(string targetPath, string description, string workingDir)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        CreateShortcut(Path.Combine(desktopPath, $"{description}.lnk"), targetPath, description, workingDir);
    }

    /// <summary>
    /// 在开始菜单创建快捷方式
    /// </summary>
    public void CreateStartMenuEntry(string targetPath, string description, string workingDir)
    {
        var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var appFolder = Path.Combine(startMenuPath, "YFrame");
        Directory.CreateDirectory(appFolder);
        CreateShortcut(Path.Combine(appFolder, $"{description}.lnk"), targetPath, description, workingDir);
    }

    /// <summary>
    /// 使用 WScript.Shell COM 创建 .lnk 快捷方式
    /// </summary>
    private static void CreateShortcut(string shortcutPath, string targetPath, string description, string workingDir)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.Description = description;
            shortcut.WorkingDirectory = workingDir;
            shortcut.IconLocation = targetPath;
            shortcut.Save();
        }
        catch { }
    }

    #endregion

    #region Registry

    /// <summary>
    /// 写入 Windows 注册表卸载信息（HKCU）
    /// </summary>
    public void WriteUninstallRegistry(string installPath, string exePath)
    {
        try
        {
            var uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\YFrame";
            using var key = Registry.CurrentUser.CreateSubKey(uninstallKey);
            if (key == null) return;
            key.SetValue("DisplayName", "YFrame 工具集");
            key.SetValue("DisplayVersion", "1.0.0");
            key.SetValue("Publisher", "YFrame");
            key.SetValue("DisplayIcon", exePath);
            key.SetValue("InstallLocation", installPath);
            key.SetValue("UninstallString", $"{exePath} --uninstall");
            key.SetValue("NoModify", 1);
            key.SetValue("NoRepair", 1);
        }
        catch { }
    }

    #endregion
}
