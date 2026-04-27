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
        ///  添加插件
        /// </summary>
        /// <param name="name">插件名称</param>
        /// <param name="ID">插件ID</param>
        [Log(Level = LogLevel.Info, Message = "添加插件")]
        public virtual void AddControl(string name, string ID)
        {
            MainWindow.logger.DebugInfo($"加载模块：{name}, {ID}");
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
                        MainWindow.logger.DebugInfo($"读取到插件: {s}");
                        string assemblyPath = @$"{item}\{s}.dll"; 
                        Assembly assembly = Assembly.LoadFrom(assemblyPath); 

                        Type userControlType = assembly.GetType($"{s}.MainControl"); // 确保命名空间和类型名正确。
                        Type ViewModelType = assembly.GetType($"{s}.MainControlViewModel"); // 确保命名空间和类型名正确。

                        if (userControlType != null)
                        {
                            // 2025.7.5 更新接口IDetail，插件继承实现ID、Name
                            I_YF_Detail detail = Activator.CreateInstance(ViewModelType) as YF_Manager.I_YF_Detail;
                            //I_YF_Command commandHandler = Activator.CreateInstance(ViewModelType) as YF_Manager.I_YF_Command;
                            //UserControl userControl = Activator.CreateInstance(userControlType) as UserControl;
                            if (detail != null)
                            {
                                // 将读取的插件信息保存
                                UserControlsService.Instance.AddControl(detail.YF_Name.ToString(), detail.YF_ID.ToString());
                                //userControl = null;
                                //commandHandler = null;
                                detail = null;
                                ViewModelType = null;
                                userControlType = null;
                                //// 将读取的插件信息保存
                                //UserControlsService.Instance.AddControl(userControl, detail.YF_Name.ToString(), detail.YF_ID.ToString(), commandHandler);
                                
                                //commandHandler.OnPluginCallback += (sender, e) =>
                                //{
                                //    HandlePluginCallback(detail.YF_ID, e);
                                //};
                            }
                            else
                            {
                                MainWindow.logger.LogInfo("LoadAndShowUserControl: ", s + "插件IDetail接口读取失败");
                            }
                        }

                    }
                }
               
            }
            catch (Exception ex)
            {
                MainWindow.logger.ErrorInfo("LoadAndShowUserControl", ex.Message);
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
                MainWindow.logger.DebugInfo($"准备显示插件: {s}");
                string assemblyPath = @$"{path}\{s}.dll";
                Assembly assembly = Assembly.LoadFrom(assemblyPath);

                Type userControlType = assembly.GetType($"{s}.MainControl"); // 确保命名空间和类型名正确。
                Type ViewModelType = assembly.GetType($"{s}.MainControlViewModel"); // 确保命名空间和类型名正确。

                if (userControlType != null)
                {
                    var viewModel = Activator.CreateInstance(ViewModelType);
                    object uc = Activator.CreateInstance(userControlType);

                    // 2025.7.5 更新接口IDetail，插件继承实现ID、Name
                    I_YF_Detail detail = viewModel as YF_Manager.I_YF_Detail;
                    I_YF_Command commandHandler = viewModel as YF_Manager.I_YF_Command;
                    UserControl userControl = uc as UserControl;

                    viewModel = null;
                    uc = null;

                    if (detail != null)
                    {
                        //将读取的插件信息保存
                        UserControlsService.Instance.DctControls[plugin_Id].userControl = userControl;
                        UserControlsService.Instance.DctControls[plugin_Id].CommandHandler = commandHandler;


                        // 将读取的插件信息保存
                        //UserControlsService.Instance.AddControl(userControl, detail.YF_Name.ToString(), detail.YF_ID.ToString(), commandHandler);

                        commandHandler.OnPluginCallback += (sender, e) =>
                        {
                            HandlePluginCallback(detail.YF_ID, e);
                        };
                    }
                    else
                    {
                        MainWindow.logger.LogInfo("LoadAndShowUserControl: ", s + "插件IDetail接口读取失败");
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
            MainWindow.logger.DebugInfo($"插件 {pluginId} 回调: {e.Command} - {e.Data}");

            // 在主线程中更新UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新UI或执行其他操作
            });
        }
    }
}
