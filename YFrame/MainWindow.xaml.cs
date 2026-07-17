using System.ComponentModel;
using System.Windows;
using YF_Manager;

namespace YFrame
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = MainWindowViewModel.Instance;
            MainWindowViewModel.Instance.Init();
        }

        /// <summary>
        /// 拦截窗口关闭事件：默认隐藏到托盘而非真正退出
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!TrayIconService.Instance.IsExiting)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                TrayIconService.Instance.Cleanup();
                base.OnClosing(e);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 初始化托盘图标服务
            TrayIconService.Instance.Initialize(this);

            // 初始化全局热键服务，传入主窗口以注册 WndProc 消息钩子
            HotkeyService.Instance.Initialize(this);
        }
    }
}
