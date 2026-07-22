using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace YFrame.View
{
    /// <summary>
    /// 关于窗口：显示版权、作者、邮箱及自动递增的版本号
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            LoadVersion();
        }

        /// <summary>
        /// 从程序集加载并显示版本号及生成日期
        /// </summary>
        private void LoadVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version != null)
                VersionText.Text = $"版本 {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            else
                VersionText.Text = "版本 1.0.0.0";

            // 读取 DLL 最后写入时间作为生成日期
            var assemblyPath = assembly.Location;
            if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
            {
                var lastWriteTime = File.GetLastWriteTime(assemblyPath);
                BuildDateText.Text = $"生成日期：{lastWriteTime:yyyy-MM-dd HH:mm}";
            }
        }

        /// <summary>
        /// 标题栏拖拽移动窗口
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 邮箱链接点击：打开默认邮件客户端
        /// </summary>
        private void EmailLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
