using System;
using System.Runtime.CompilerServices;

namespace YF_Manager
{
    /// <summary>
    /// 全局配置类，从 Config/config.conf 文件读取配置值
    /// 文件不存在时自动创建并使用默认值
    /// </summary>
    public class Config
    {
        /// <summary>
        /// 配置助手实例（懒加载，首次访问时初始化并加载配置文件）
        /// </summary>
        private static YF_ConfigHelper? _helper;
        private static YF_ConfigHelper Helper =>
            _helper ??= YF_ConfigHelper.Instance;

        /// <summary>
        /// 辅助方法 —— 自动获取调用者的属性名作为配置键
        /// </summary>
        private static string Get(string defaultValue, [CallerMemberName] string propertyName = "")
            => Helper.Get(propertyName, defaultValue);

        /// <summary>
        /// 辅助方法 —— 自动获取调用者的属性名作为配置键并持久化到文件
        /// </summary>
        private static void Set(string value, [CallerMemberName] string propertyName = "")
            => Helper.Set(propertyName, value);

        /// <summary>
        /// PaddlOCR 模型根路径
        /// </summary>
        public static string Paddlepath
        {
            get => Get(@"plugins\YF_ScreenOCRTranslate\inference");
            set => Set(value);
        }

        /// <summary>
        /// AI助手 模型根路径
        /// </summary>
        public static string AIModelpath
        {
            get => Get(@"plugins\YF_AIHelper\Model\DeepSeek-R1-Distill-Qwen-7B-Q4_K_M.gguf");
            set => Set(value);
        }

        /// <summary>
        /// 日志路径
        /// </summary>
        public static string LogPath
        {
            get => Get(@"Log");
            set => Set(value);
        }

        /// <summary>
        /// 脚本保存路径
        /// </summary>
        public static string ScriptPath
        {
            get => Get(@"Config\Script");
            set => Set(value);
        }

        /// <summary>
        /// 插件路径
        /// </summary>
        public static string PluginPath
        {
            get => Get(@"Plugins");
            set => Set(value);
        }

        /// <summary>
        /// 服务端端口
        /// </summary>
        public static string TcpHelper_Port_Server
        {
            get => Get("8021");
            set => Set(value);
        }

        /// <summary>
        /// 客户端端口
        /// </summary>
        public static string TcpHelper_Port_Client
        {
            get => Get("8022");
            set => Set(value);
        }
    }
}
