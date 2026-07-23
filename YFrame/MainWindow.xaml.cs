using System.ComponentModel;
using System.Windows;
using YF_Manager;

namespace YFrame
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly HotkeyService _hotkeyService;
        private readonly TrayIconService _trayIconService;

        /// <summary>
        /// 创建主窗口（DI 构造函数注入）
        /// </summary>
        /// <param name="viewModel">主窗口 ViewModel</param>
        /// <param name="hotkeyService">全局热键服务</param>
        /// <param name="trayIconService">托盘图标服务</param>
        public MainWindow(MainWindowViewModel viewModel, HotkeyService hotkeyService, TrayIconService trayIconService)
        {
            _viewModel = viewModel;
            _hotkeyService = hotkeyService;
            _trayIconService = trayIconService;

            InitializeComponent();
            DataContext = viewModel;
            viewModel.Init();
        }

        /// <summary>
        /// 拦截窗口关闭事件：默认隐藏到托盘而非真正退出
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_trayIconService.IsExiting)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                _trayIconService.Cleanup();
                base.OnClosing(e);
            }
        }

        /// <summary>
        /// 窗口消息源初始化完成后注册托盘图标和全局热键
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 初始化托盘图标服务
            _trayIconService.Initialize(this);

            // 初始化全局热键服务，传入主窗口以注册 WndProc 消息钩子
            _hotkeyService.Initialize(this);
        }
    }
}
