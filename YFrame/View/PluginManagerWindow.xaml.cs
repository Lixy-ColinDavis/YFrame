using Castle.DynamicProxy;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YF_Manager;
using YFrame.ViewModel;

namespace YFrame.View
{
    /// <summary>
    /// 插件管理器窗口，通过 AOP 代理创建 ViewModel
    /// </summary>
    public partial class PluginManagerWindow : Window
    {
        /// <summary>
        /// 窗口圆角半径（与设计稿保持一致）
        /// </summary>
        private const double WindowCornerRadius = 14;

        public PluginManagerWindow()
        {
            InitializeComponent();
            // 用 Castle 动态代理创建 ViewModel，使 [Log] 的 virtual 方法被拦截
            DataContext = new ProxyGenerator().CreateClassProxy<PluginManagerViewModel>(new LogInterceptor());

            // 圆角边框需要按实际尺寸裁剪，避免内容溢出圆角
            WindowFrame.SizeChanged += (_, _) => UpdateWindowClip();
            UpdateWindowClip();
        }

        /// <summary>
        /// 按窗口当前尺寸生成圆角裁剪区域
        /// </summary>
        private void UpdateWindowClip()
        {
            if (WindowFrame.ActualWidth <= 0 || WindowFrame.ActualHeight <= 0)
                return;

            WindowFrame.Clip = new RectangleGeometry(
                new Rect(0, 0, WindowFrame.ActualWidth, WindowFrame.ActualHeight),
                WindowCornerRadius, WindowCornerRadius);
        }

        /// <summary>
        /// 标题栏拖拽：按住左键移动窗口
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        /// <summary>
        /// 最小化窗口
        /// </summary>
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
