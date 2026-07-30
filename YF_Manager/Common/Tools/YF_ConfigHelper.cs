using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace YF_Manager
{
    /// <summary>
    /// 配置读写助手，从 Config/config.conf 读取键值对配置
    /// 文件不存在时自动创建并使用默认值，供框架及插件统一调用
    /// </summary>
    public class YF_ConfigHelper
    {
        // AOP 日志拦截，采用 ProxyGenerator 模式：
        private static readonly Lazy<YF_ConfigHelper> _instance =
            new Lazy<YF_ConfigHelper>(() =>
                new Castle.DynamicProxy.ProxyGenerator()
                    .CreateClassProxy<YF_ConfigHelper>(new LogInterceptor()));

        /// <summary>
        /// 全局单例入口
        /// </summary>
        public static YF_ConfigHelper Instance => _instance.Value;

        /// <summary>
        /// 配置文件路径（相对于应用程序根目录）
        /// </summary>
        private readonly string _configFilePath;

        /// <summary>
        /// 内存中的配置字典（线程安全）
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _configValues = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 配置文件注释字符
        /// </summary>
        private const string CommentPrefix = "#";

        /// <summary>
        /// 配置键值分隔符
        /// </summary>
        private const char KeyValueSeparator = '=';

        /// <summary>
        /// 无参构造函数供 Castle 动态代理，构造后立即加载（或创建）配置
        /// </summary>
        public YF_ConfigHelper()
        {
            _configFilePath = Path.Combine(AppContext.BaseDirectory, "Config", "config.conf");
            LoadOrDefault();
        }

        /// <summary>
        /// 加载配置文件，不存在则创建默认配置并写入文件
        /// 注意：非virtual，不走AOP代理；初始化阶段不使用 YF_Manager_Main.logger 避免循环依赖
        /// </summary>
        private void LoadOrDefault()
        {
            try
            {
                // 确保目录存在
                var dir = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (!File.Exists(_configFilePath))
                {
                    // 文件不存在：加载默认值并写入文件
                    LoadDefaults();
                    SaveToFileInternal();
                    return;
                }

                // 文件存在：从文件读取配置
                LoadFromFileInternal();
            }
            catch (Exception ex)
            {
                // 初始化阶段不使用 YF_Manager_Main.logger（避免循环依赖），使用调试输出
                Debug.WriteLine($"[YF_ConfigHelper] 配置初始化失败: {ex.Message}，使用默认值");
                LoadDefaults();
            }
        }

        /// <summary>
        /// 加载默认配置值（与 Config.cs 原 const 值保持一致）
        /// 注意：非virtual，不走AOP代理
        /// </summary>
        private void LoadDefaults()
        {
            // PaddlOCR 模型根路径
            _configValues["Paddlepath"] = @"plugins\YF_ScreenOCRTranslate\inference";
            // 日志路径
            _configValues["LogPath"] = @"Log";
            // 脚本保存路径
            _configValues["ScriptPath"] = @"Config\Script";
            // 插件路径
            _configValues["PluginPath"] = @"Plugins";
            // 服务端端口
            _configValues["TcpHelper_Port_Server"] = "8021";
            // 客户端端口
            _configValues["TcpHelper_Port_Client"] = "8022";
            // 插件服务器端口
            _configValues["PluginServerPort"] = "9000";
            // 插件管理器上次连接的服务器地址（不含端口）
            _configValues["PluginManagerServerURL"] = "http://127.0.0.1";
        }

        /// <summary>
        /// 从配置文件读取所有键值对到内存字典
        /// 注意：非virtual，不走AOP代理；初始化阶段不使用 logger 避免循环依赖
        /// </summary>
        private void LoadFromFileInternal()
        {
            try
            {
                var lines = File.ReadAllLines(_configFilePath, Encoding.UTF8);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // 跳过空行和注释行
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(CommentPrefix))
                        continue;

                    // 解析 key=value 格式
                    var separatorIndex = trimmed.IndexOf(KeyValueSeparator);
                    if (separatorIndex <= 0 || separatorIndex >= trimmed.Length - 1)
                        continue;

                    var key = trimmed.Substring(0, separatorIndex).Trim();
                    var value = trimmed.Substring(separatorIndex + 1).Trim();

                    // 如果值用双引号包裹则去除引号
                    if (value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
                        value = value.Substring(1, value.Length - 2);

                    if (!string.IsNullOrEmpty(key))
                        _configValues[key] = value;
                }

                // 确保所有默认键都存在（兼容旧配置文件缺少新配置项的场景）
                EnsureAllDefaultsExist();
            }
            catch (Exception ex)
            {
                // 初始化阶段不使用 YF_Manager_Main.logger（避免循环依赖），使用调试输出
                Debug.WriteLine($"[YF_ConfigHelper] 加载配置文件失败: {ex.Message}，使用默认值");
                LoadDefaults();
            }
        }

        /// <summary>
        /// 确保所有默认配置键都存在于字典中（缺失则补默认值但不覆盖已有值）
        /// 注意：非virtual，不走AOP代理
        /// </summary>
        private void EnsureAllDefaultsExist()
        {
            // 保存当前所有已读入的键
            var existingKeys = new HashSet<string>(_configValues.Keys, StringComparer.OrdinalIgnoreCase);

            // 对缺失的键补充默认值（未直接创建临时字典，直接在原地判断后赋值）
            if (!existingKeys.Contains("Paddlepath"))
                _configValues.TryAdd("Paddlepath", @"plugins\YF_ScreenOCRTranslate\inference");
            if (!existingKeys.Contains("LogPath"))
                _configValues.TryAdd("LogPath", @"Log");
            if (!existingKeys.Contains("ScriptPath"))
                _configValues.TryAdd("ScriptPath", @"Config\Script");
            if (!existingKeys.Contains("PluginPath"))
                _configValues.TryAdd("PluginPath", @"Plugins");
            if (!existingKeys.Contains("TcpHelper_Port_Server"))
                _configValues.TryAdd("TcpHelper_Port_Server", "8021");
            if (!existingKeys.Contains("TcpHelper_Port_Client"))
                _configValues.TryAdd("TcpHelper_Port_Client", "8022");
            if (!existingKeys.Contains("PluginServerPort"))
                _configValues.TryAdd("PluginServerPort", "9000");
            if (!existingKeys.Contains("PluginManagerServerURL"))
                _configValues.TryAdd("PluginManagerServerURL", "http://127.0.0.1");
        }

        /// <summary>
        /// 将配置字典写入文件
        /// 注意：非virtual，不走AOP代理；使用调试输出避免循环依赖
        /// </summary>
        private void SaveToFileInternal()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("# YFrame 配置文件");
                sb.AppendLine("# 修改后重启应用生效");
                sb.AppendLine();

                foreach (var kvp in _configValues)
                {
                    // 值中包含空格或特殊字符时用双引号包裹
                    var value = kvp.Value;
                    if (value.Contains(" ") || value.Contains("#") || value.Contains("="))
                        value = $"\"{value}\"";
                    sb.AppendLine($"{kvp.Key}={value}");
                }

                File.WriteAllText(_configFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 使用调试输出避免与 YF_Manager_Main.logger 产生循环依赖
                Debug.WriteLine($"[YF_ConfigHelper] 保存配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定键的配置值，键不存在则返回默认值
        /// 注意：非virtual，不走AOP代理，避免被 LogInterceptor 拦截后触发 Config.LogPath → YF_Manager_Log → 无限递归
        /// </summary>
        /// <param name="key">配置键名（忽略大小写）</param>
        /// <param name="defaultValue">当键不存在时返回的默认值</param>
        /// <returns>配置值</returns>
        public string Get(string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(key))
                return defaultValue;

            if (_configValues.TryGetValue(key, out var value))
                return value;

            return defaultValue;
        }

        /// <summary>
        /// 设置指定键的配置值并立即持久化到文件
        /// </summary>
        /// <param name="key">配置键名</param>
        /// <param name="value">配置值</param>
        [Log(Level = LogLevel.Info, Message = "写入配置项")]
        public virtual void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _configValues[key] = value;
            SaveToFileInternal();
        }

        /// <summary>
        /// 检查指定键是否存在于配置中
        /// 注意：非virtual，不走AOP代理，避免 AOP 拦截链中触发递归
        /// </summary>
        /// <param name="key">配置键名（忽略大小写）</param>
        /// <returns>存在返回 true</returns>
        public bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            return _configValues.ContainsKey(key);
        }

        /// <summary>
        /// 重新从文件加载配置（覆盖内存中的值）
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "重新加载配置文件")]
        public virtual void Reload()
        {
            _configValues.Clear();
            if (File.Exists(_configFilePath))
                LoadFromFileInternal();
            else
            {
                LoadDefaults();
                SaveToFileInternal();
            }
        }

        /// <summary>
        /// 获取配置文件所在的目录路径
        /// 注意：非virtual，不走AOP代理，避免 AOP 拦截链中触发递归
        /// </summary>
        /// <returns>配置文件目录绝对路径</returns>
        public string GetConfigDirectory()
        {
            var dir = Path.GetDirectoryName(_configFilePath);
            return dir ?? "";
        }
    }
}
