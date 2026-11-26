using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace YFrame.Tests;

/// <summary>
/// 测试程序集初始化。
/// </summary>
internal static class TestBootstrap
{
    /// <summary>
    /// 模块加载时创建测试配置，早于任何测试类型和 Config 访问。
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            string configDirectory = Path.Combine(AppContext.BaseDirectory, "Config");
            Directory.CreateDirectory(configDirectory);

            string configPath = Path.Combine(configDirectory, "config.conf");
            if (File.Exists(configPath))
                return;

            // 只写最小键值；其余键由 YF_ConfigHelper.EnsureAllDefaultsExist 补齐
            string content =
                "# YFrame test config" + Environment.NewLine +
                "Theme=Common/Themes/CloudWhiteTheme.xaml" + Environment.NewLine +
                "LogPath=Log" + Environment.NewLine +
                "PluginPath=Plugins" + Environment.NewLine +
                "ScreenScale=125" + Environment.NewLine +
                "Env=uat" + Environment.NewLine;

            File.WriteAllText(configPath, content, new UTF8Encoding(false));
        }
        catch
        {
            // 测试初始化失败不应在模块加载阶段直接终止进程，
            // 具体测试会通过自身失败暴露问题。
        }
    }
}
