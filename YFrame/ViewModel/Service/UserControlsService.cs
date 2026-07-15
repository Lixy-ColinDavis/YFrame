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
        // 使用Lazy<T>确保线程安全的延迟初始化，避免双重检查锁定的复杂性
        // 单例模式+日志拦截器
        public static readonly Lazy<UserControlsService> _instance = new Lazy<UserControlsService>(
            () => new ProxyGenerator().CreateClassProxy<UserControlsService>(new LogInterceptor())
            );

        public static UserControlsService Instance => _instance.Value;

        // <ID, Name> => <YF_AIHelper, AI 助手>
        public Dictionary<string, CtrlDataModel> DctControls = new Dictionary<string, CtrlDataModel>();

        /// <summary>
        /// 加载插件程序集并创建 MainControl / MainControlViewModel 实例
        /// </summary>
        /// <param name="assemblyPath">插件 DLL 完整路径</param>
        /// <param name="pluginName">插件名称（命名空间前缀）</param>
        /// <param name="userControl">创建的 UserControl 实例</param>
        /// <param name="detail">创建的 ViewModel 对应的 I_YF_Detail 接口</param>
        /// <param name="commandHandler">创建的 ViewModel 对应的 I_YF_Command 接口</param>
        /// <returns>加载成功返回 true，类型解析失败返回 false</returns>
        private static bool TryLoadPlugin(string assemblyPath, string pluginName,
    out UserControl? userControl, out I_YF_Detail? detail, out I_YF_Command? commandHandler)
        {
            userControl = null; detail = null; commandHandler = null;
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            Type? userControlType = assembly.GetType($"{pluginName}.MainControl");
            Type? viewModelType = assembly.GetType($"{pluginName}.MainControlViewModel");
            if (userControlType == null || viewModelType == null)
                return false;
            if (!typeof(UserControl).IsAssignableFrom(userControlType))
                return false;
            var viewModel = Activator.CreateInstance(viewModelType);
            object uc = Activator.CreateInstance(userControlType);
            detail = viewModel as I_YF_Detail;
            commandHandler = viewModel as I_YF_Command;
            userControl = uc as UserControl;
            return userControl != null;
        }

        /// <summary>
        ///  添加插件
        /// </summary>
        /// <param name="name">插件名称</param>
        /// <param name="ID">插件ID</param>
        [Log(Level = LogLevel.Info, Message = "添加插件")]
        public virtual void AddControl(string name, string ID)
        {
            MainWindowViewModel.Instance.logger.DebugInfo($"加载模块：{name}, {ID}");
            // 插件添加
            // 插件添加<ID, 名称>
            DctControls.Add(ID, new CtrlDataModel() 
            { 
                Name = name,
            });
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
                    Directory.CreateDirectory("plugins"); // 自动创建多级目录‌
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
                        MainWindowViewModel.Instance.logger.DebugInfo($"读取到插件: {s}");
                        string assemblyPath = @$"{item}\{s}.dll";
                        if (TryLoadPlugin(assemblyPath, s, out _, out var detail, out _))
                        {
                            if (detail != null)
                            {
                                UserControlsService.Instance.AddControl(detail.YF_Name.ToString(), detail.YF_ID.ToString());
                            }
                            else
                            {
                                MainWindowViewModel.Instance.logger.LogInfo("LoadAndShowUserControl: ", s + "插件IDetail接口读取失败");
                            }
                        }
                        else
                        {
                            MainWindowViewModel.Instance.logger.ErrorInfo("LoadAndShowUserControl", s + " MainControl/MainControlViewModel 加载失败");
                        }

                    }
                }
               
            }
            catch (Exception ex)
            {
                MainWindowViewModel.Instance.logger.ErrorInfo("LoadAndShowUserControl", ex.Message);
            }
        }


        /// <summary>
        /// 显示指定的插件
        /// </summary>
        /// <param name="plugin_Id">插件ID</param>
        [Log(Level = LogLevel.Info, Message = "显示指定的插件")]
        public virtual void ShowUserControl(string plugin_Id)
        {
            string path = "plugins\\" + plugin_Id;
            // 读取插件主dll
            string[] array = Directory.GetFiles(path, "YF_*.dll")
                .Select(p => System.IO.Path.GetFileNameWithoutExtension(p))
                .ToArray();
            // 遍历读取到的插件并忽略Manager
            foreach (string s in array)
            {
                if (s == "YF_Manager")
                    continue;
                MainWindowViewModel.Instance.logger.DebugInfo($"准备显示插件: {s}");

                string assemblyPath = @$"{path}\{s}.dll";
                if (TryLoadPlugin(assemblyPath, s, out var userControl, out var detail, out var commandHandler))
                {
                    if (detail != null)
                    {
                        if (UserControlsService.Instance.DctControls.TryGetValue(plugin_Id, out var ctrlData))
                        {
                            ctrlData.userControl = userControl;
                            ctrlData.CommandHandler = commandHandler;
                        }
                        else
                        {
                            MainWindowViewModel.Instance.logger.ErrorInfo("ShowUserControl",
                                $"插件 {plugin_Id} 未在插件列表中找到，请先执行插件扫描。");
                        }

                        if (commandHandler != null)
                        {
                            commandHandler.OnPluginCallback += (sender, e) =>
                            {
                                MainWindowViewModel.Instance.HandlePluginCallback(detail.YF_ID, e);
                            };
                        }
                    }
                }

            }
        }
        /// <summary>
        /// 插件回调
        /// </summary>
        /// <param name="pluginId"></param>
        /// <param name="e"></param>
        [Log(Level = LogLevel.Info, Message = "插件回调")]
        public virtual void HandlePluginCallback(string pluginId, PluginEventArgs e)
        {
            // 处理插件回调
            MainWindowViewModel.Instance.logger.DebugInfo($"插件 {pluginId} 回调: {e.Command} - {e.Data}");

            // 在主线程中更新UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新UI或执行其他操作
            });
        }
    }
}
