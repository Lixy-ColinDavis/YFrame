using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using YF_Manager;

namespace YFrame
{
    public class UserControlsService
    {
        #region 依赖（通过 InitializeDependencies 属性注入）

        /// <summary>日志记录器（DI 属性注入）</summary>
        private YF_Manager_Log _logger = null!;

        /// <summary>插件回调处理器（DI 属性注入，将插件回调转发到 PluginService）</summary>
        public Action<string, PluginEventArgs>? OnPluginCallback { get; set; }

        #endregion

        // <ID, Name> => <YF_AIHelper, AI 助手>
        public Dictionary<string, CtrlDataModel> DctControls = new Dictionary<string, CtrlDataModel>();

        /// <summary>
        /// 设置依赖项（由 DI 容器创建代理后调用）
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="onPluginCallback">插件回调处理器，用于将插件回调转发到 PluginService</param>
        public void InitializeDependencies(YF_Manager_Log logger, Action<string, PluginEventArgs> onPluginCallback)
        {
            _logger = logger;
            OnPluginCallback = onPluginCallback;
        }

        /// <summary>
        /// 仅加载插件程序集并创建轻量 ViewModel 实例读取元数据，不创建 UserControl。
        /// </summary>
        /// <param name="assemblyPath">插件 DLL 完整路径</param>
        /// <param name="_logger">日志记录器</param>
        /// <param name="pluginName">插件名称（命名空间前缀）</param>
        /// <param name="detail">读取到的 I_YF_Detail 接口（含 YF_ID / YF_Name），仅在返回 true 时有效</param>
        /// <returns>元数据读取成功返回 true，否则返回 false</returns>
        private static bool TryLoadPluginMetadata(string assemblyPath, YF_Manager_Log _logger, string pluginName, out I_YF_Detail? detail)
        {
            try
            {
                detail = null;
                // 只读元数据，不创建 UserControl（创建会触发完整初始化）
                Assembly assembly = Assembly.LoadFrom(assemblyPath);
                Type? viewModelType = assembly.GetType($"{pluginName}.MainControlViewModel");
                if (viewModelType == null)
                    return false;
                // 轻量创建后随即丢弃，避免干扰插件自身单例
                var viewModel = Activator.CreateInstance(viewModelType);
                detail = viewModel as I_YF_Detail;
                return detail != null;
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("TryLoadPluginMetadata", ex.Message);
                detail = null;
                return false;
            }
        }

        /// <summary>
        /// 加载插件程序集并创建 MainControl / MainControlViewModel 实例
        /// </summary>
        /// <param name="assemblyPath">插件 DLL 完整路径</param>
        /// <param name="pluginName">插件名称（命名空间前缀）</param>
        /// <param name="userControl">创建的 UserControl 实例</param>
        /// <param name="detail">创建的 ViewModel 对应的 I_YF_Detail 接口</param>
        /// <param name="commandHandler">创建的 ViewModel 对应的 I_YF_Command 接口</param>
        /// <returns>加载成功返回 true，类型解析失败返回 false</returns>
        private static bool TryLoadPlugin(string assemblyPath, YF_Manager_Log _logger, string pluginName,
            out UserControl? userControl, out I_YF_Detail? detail, out I_YF_Command? commandHandler)
        {
            try
            {
                userControl = null; detail = null; commandHandler = null;
                // 不引入 AssemblyLoadContext，插件随主程序生命周期加载
                Assembly assembly = Assembly.LoadFrom(assemblyPath);
                Type? userControlType = assembly.GetType($"{pluginName}.MainControl");
                Type? viewModelType = assembly.GetType($"{pluginName}.MainControlViewModel");
                if (userControlType == null || viewModelType == null)
                    return false;
                if (!typeof(UserControl).IsAssignableFrom(userControlType))
                    return false;
                var viewModel = Activator.CreateInstance(viewModelType);
                object uc = Activator.CreateInstance(userControlType);
                userControl = uc as UserControl;
                if (userControl != null)
                {
                    // 插件未自设 DataContext 时由框架设置
                    if (userControl.DataContext == null)
                        userControl.DataContext = viewModel;
                    // 从 DataContext 提取接口，保证与 UI 绑定同一实例
                    detail = userControl.DataContext as I_YF_Detail;
                    commandHandler = userControl.DataContext as I_YF_Command;
                }
                return userControl != null;
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("TryLoadPlugin", ex.Message);
                userControl = null;
                detail = null;
                commandHandler = null;
                return false;
            }
        }

        /// <summary>
        /// 添加插件到字典
        /// </summary>
        /// <param name="name">插件名称</param>
        /// <param name="ID">插件ID</param>
        [Log(Level = LogLevel.Info, Message = "添加插件")]
        public virtual void AddControl(string name, string ID)
        {
            _logger.DebugInfo($"加载模块：{name}, {ID}");
            DctControls.Add(ID, new CtrlDataModel() 
            { 
                Name = name,
            });
        }

        /// <summary>
        /// 清空所有已注册的插件字典，用于重新加载前清理旧数据
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "清空插件字典")]
        public virtual void ClearAllControls()
        {
            DctControls.Clear();
            _logger.DebugInfo("插件字典已清空");
        }


        /// <summary>
        /// 自动识别并读取插件
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "自动识别并读取插件")]
        public virtual void LoadAndShowUserControl()
        {
            try
            {
                if (!Directory.Exists("plugins"))
                {
                    Directory.CreateDirectory("plugins"); // 自动创建多级目录
                }

                // 读取插件的文件夹列表
                string[] allDirectories = Directory.GetDirectories("plugins");

                // 遍历每个插件目录
                foreach (var item in allDirectories)
                {
                    // 读取插件主dll
                    string[] array = Directory.GetFiles(item, "YF_*.dll")
                        .Select(p => System.IO.Path.GetFileNameWithoutExtension(p))
                        .ToArray();
                    // 遍历读取到的插件并忽略Manager
                    foreach (string s in array)
                    {
                        if (s == "YF_Manager")
                            continue;
                        _logger.DebugInfo($"读取到插件: {s}");
                        string assemblyPath = @$"{item}\{s}.dll";
                        // 懒加载：仅读元数据，不实例化控件
                        if (TryLoadPluginMetadata(assemblyPath, _logger, s, out var detail))
                        {
                            if (detail != null)
                            {
                                AddControl(detail.YF_Name.ToString(), detail.YF_ID.ToString());
                            }
                            else
                            {
                                _logger.LogInfo("LoadAndShowUserControl: ", s + "插件IDetail接口读取失败");
                            }
                        }
                        else
                        {
                            _logger.ErrorInfo("LoadAndShowUserControl", s + " MainControlViewModel 加载失败");
                        }

                    }
                }
               
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("LoadAndShowUserControl", ex.Message);
            }
        }


        /// <summary>
        /// 显示指定插件
        /// </summary>
        /// <param name="plugin_Id">插件ID</param>
        [Log(Level = LogLevel.Info, Message = "显示指定的插件")]
        public virtual void ShowUserControl(string plugin_Id)
        {
            string path = "plugins\\" + plugin_Id;
            if (!Directory.Exists(path))
            {
                _logger.ErrorInfo("ShowUserControl", $"插件目录不存在: {path}");
                return;
            }

            // 读取插件主dll
            string[] array = Directory.GetFiles(path, "YF_*.dll")
                .Select(p => System.IO.Path.GetFileNameWithoutExtension(p))
                .ToArray();
            // 遍历读取到的插件并忽略Manager
            foreach (string s in array)
            {
                if (s == "YF_Manager")
                    continue;
                _logger.DebugInfo($"准备显示插件: {s}");

                string assemblyPath = @$"{path}\{s}.dll";
                if (TryLoadPlugin(assemblyPath, _logger, s, out var userControl, out var detail, out var commandHandler))
                {
                    if (detail == null)
                    {
                        _logger.LogInfo("ShowUserControl: ", s + "插件 I_YF_Detail 接口读取失败");
                        continue;
                    }

                    if (!DctControls.TryGetValue(plugin_Id, out var ctrlData))
                    {
                        _logger.ErrorInfo("ShowUserControl",
                            $"插件 {plugin_Id} 未在插件列表中找到，请先执行插件扫描。");
                        continue;
                    }

                    // 只存当前显示实例，切换即重建
                    ctrlData.userControl = userControl;
                    ctrlData.CommandHandler = commandHandler;
                    ctrlData.PluginId = detail.YF_ID;

                    // 避免对静态单例插件重复订阅造成事件泄漏
                    if (commandHandler != null && !ReferenceEquals(ctrlData.LastSubscribedHandler, commandHandler))
                    {
                        SubscribeCallback(ctrlData);
                        ctrlData.LastSubscribedHandler = commandHandler;
                    }
                }
                else
                {
                    _logger.ErrorInfo("ShowUserControl", s + " MainControl/MainControlViewModel 加载失败");
                }
            }
        }

        /// <summary>
        /// 为指定插件订阅回调
        /// </summary>
        /// <param name="ctrlData">插件数据模型</param>
        private void SubscribeCallback(CtrlDataModel ctrlData)
        {
            if (ctrlData.CommandHandler == null)
                return;

            string pluginId = ctrlData.PluginId;
            ctrlData.CommandHandler.OnPluginCallback += (sender, e) =>
            {
                // 通过 DI 注入的回调转发到 PluginService
                if (OnPluginCallback != null)
                    OnPluginCallback(pluginId, e);
                else
                    _logger.ErrorInfo("ShowUserControl", "OnPluginCallback 回调未设置，插件回调丢失");
            };
        }
        /// <summary>
        /// 插件回调
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="e">回调事件参数</param>
        [Log(Level = LogLevel.Info, Message = "插件回调")]
        public virtual void HandlePluginCallback(string pluginId, PluginEventArgs e)
        {
            // 处理插件回调
            _logger.DebugInfo($"插件 {pluginId} 回调: {e.Command} - {e.Data}");
        }
    }
}
