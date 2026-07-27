namespace YFrame.Installer.Models;

/// <summary>
/// 安装配置模型：包含安装路径、快捷方式选项
/// 仅安装框架本体，不包含插件选择
/// </summary>
public class InstallConfig
{
    /// <summary>目标安装路径</summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>是否创建桌面快捷方式</summary>
    public bool CreateDesktopShortcut { get; set; } = true;

    /// <summary>是否添加到开始菜单</summary>
    public bool AddToStartMenu { get; set; } = true;

    /// <summary>是否开机自启（预留）</summary>
    public bool AutoStart { get; set; }
}
