using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        public List<string> ControlsName = new List<string>();
        public Dictionary<string, string> DctControls = new Dictionary<string, string>();

        public UserControl GetControl(string name)
        {
            try
            {
                foreach (var control in ChildControls)
                {
                    var a = (control.Content as Grid)?.Parent.ToString()?.Replace(".MainControl", "");
                }

                return ChildControls.Find(x => (x as YF_Manager.IDetail).YF_Name == name);
            }
            catch (Exception ex)
            {
                MainWindow.logger.ErrorInfo("UserControlsService", ex.Message);
            }
            return null;
        }

        public void AddControl(UserControl ctrl, string Name, string ID)
        {
            //YF_Manager_Log.DebugInfo("MainWindow.Id", $"加载模块：{(ctrl.Content as Grid).Parent.ToString().Replace(".MainControl", "")}");
            ChildControls.Add(ctrl);
            DctControls.Add(ID, Name);
            //ControlsName.Add(Name);
            //ControlsName.Add((ctrl.Content as Grid).Parent.ToString().Replace(".MainControl", ""));
        }


        public void LoadAndShowUserControl()
        {
            try
            {
                string[] array = Directory.GetFiles("plugins", "YF_*.dll")
                    .Select(p => System.IO.Path.GetFileNameWithoutExtension(p))
                    .ToArray();

                foreach (string s in array)
                {

                    string assemblyPath = @$"Plugins\{s}.dll"; // 修改为实际路径或使用 Assembly.LoadFile 或 Assembly.LoadFrom 等方法加载已编译的程序集。
                    Assembly assembly = Assembly.LoadFrom(assemblyPath); // 使用Assembly.LoadFile或Assembly.Load也可以，取决于你的需求。
                    Type userControlType = assembly.GetType($"{s}.MainControl"); // 确保命名空间和类型名正确。
                    if (userControlType != null)
                    {
                        // 2025.7.5 更新接口IDetail，插件继承实现ID、Name
                        YF_Manager.IDetail detail = Activator.CreateInstance(userControlType) as YF_Manager.IDetail;
                        UserControl userControl = Activator.CreateInstance(userControlType) as UserControl; // 创建实例。
                        if (detail != null)
                        {
                            UserControlsService.Instance.AddControl(userControl, detail.YF_Name, detail.YF_ID);
                        }
                        else
                        {

                        }
                    }

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载UserControl时出错: " + ex.Message);
            }
        }
    }
}
