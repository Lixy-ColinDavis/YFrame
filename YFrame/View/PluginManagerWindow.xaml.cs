using Castle.DynamicProxy;
using System.Windows;
using YF_Manager;
using YFrame.ViewModel;

namespace YFrame.View
{
    /// <summary>
    /// 插件管理器窗口，通过 AOP 代理创建 ViewModel
    /// </summary>
    public partial class PluginManagerWindow : Window
    {
        public PluginManagerWindow()
        {
            InitializeComponent();
            // 用 Castle 动态代理创建 ViewModel，使 [Log] 的 virtual 方法被拦截
            DataContext = new ProxyGenerator().CreateClassProxy<PluginManagerViewModel>(new LogInterceptor());
        }
    }
}
