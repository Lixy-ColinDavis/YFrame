using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using YF_Manager;

namespace YFrame
{
    public class UserControlsService
    {
        // 替代双重检查锁定模式，简化线程安全单例实现‌
        private static readonly Lazy<UserControlsService> _instance = new Lazy<UserControlsService>(() => new UserControlsService());
        public static UserControlsService Instance => _instance.Value;

        // 插件库
        private List<UserControl> ChildControls = new List<UserControl>();

        // <ID, Name> => <YF_AIHelper, AI 助手>
        public Dictionary<string, CtrlDataModel> DctControls = new Dictionary<string, CtrlDataModel>();   

        /// <summary>
        /// 读取并返回插件
        /// </summary>
        /// <param name="name">插件名称 AI 助手</param>
        /// <returns></returns>
        public UserControl GetControl(string name)
        {
            try
            {
                // 查询对应的插件
                return ChildControls.Find(x => (x as YF_Manager.I_YF_Detail).YF_Name == name);
            }
            catch (Exception ex)
            {
                MainWindow.logger.ErrorInfo("UserControlsService", ex.Message);
            }
            return null;
        }

        /// <summary>
        ///  添加插件
        /// </summary>
        /// <param name="ctrl">插件-用户控件</param>
        /// <param name="Name">插件名称</param>
        /// <param name="ID">插件ID</param>
        public void AddControl(UserControl ctrl, string name, string ID, Dictionary<string, object> p)
        {
            MainWindow.logger.DebugInfo($"加载模块：{name}, {ID}");
            // 插件添加
            ChildControls.Add(ctrl);
            // 插件添加<ID, 名称>
            DctControls.Add(ID, new CtrlDataModel() 
            { 
                Name = name, 
                Parameters = p 
            });
        }


        /// <summary>
        /// 自动识别并读取插件
        /// </summary>
        public void LoadAndShowUserControl()
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
                        string assemblyPath = @$"{item}\{s}.dll"; // 修改为实际路径或使用 Assembly.LoadFile 或 Assembly.LoadFrom 等方法加载已编译的程序集。
                        Assembly assembly = Assembly.LoadFrom(assemblyPath); // 使用Assembly.LoadFile或Assembly.Load也可以，取决于你的需求。
                        Type userControlType = assembly.GetType($"{s}.MainControl"); // 确保命名空间和类型名正确。
                        if (userControlType != null)
                        {
                            // 2025.7.5 更新接口IDetail，插件继承实现ID、Name
                            YF_Manager.I_YF_Detail detail = Activator.CreateInstance(userControlType) as YF_Manager.I_YF_Detail;
                            YF_Manager.I_YF_Params _params = Activator.CreateInstance(userControlType) as YF_Manager.I_YF_Params;
                            UserControl userControl = Activator.CreateInstance(userControlType) as UserControl;
                            if (detail != null)
                            {
                                // 将读取的插件信息保存
                                UserControlsService.Instance.AddControl(userControl, detail.YF_Name, detail.YF_ID, _params.Parameters);
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
    }
}
