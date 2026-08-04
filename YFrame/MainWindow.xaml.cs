using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using YF_Manager;

namespace YFrame
{
    public partial class MainWindow : Window
    {
        #region Win32 全屏相关 API 声明

        // Win32 矩形结构：表示屏幕/窗口的坐标范围
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;   // 左边界
            public int Top;    // 上边界
            public int Right;  // 右边界
            public int Bottom; // 下边界
        }

        // Win32 监视器信息结构：包含完整区域与工作区域
        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;    // 结构体大小
            public RECT rcMonitor; // 监视器完整区域（含任务栏）
            public RECT rcWork;    // 监视器工作区域（不含任务栏）
            public uint dwFlags;   // 标志位
        }

        // 监视器查询标志：返回离窗口最近的监视器
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // SetWindowPos 标志：不调整 Z 序
        private const uint SWP_NOZORDER = 0x0004;

        /// <summary>
        /// 获取窗口的屏幕矩形区域
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpRect">输出矩形</param>
        /// <returns>是否成功</returns>
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>
        /// 获取窗口所在的监视器句柄
        /// </summary>
        /// <param name="hwnd">窗口句柄</param>
        /// <param name="dwFlags">查询标志</param>
        /// <returns>监视器句柄</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        /// <summary>
        /// 获取监视器信息
        /// </summary>
        /// <param name="hMonitor">监视器句柄</param>
        /// <param name="lpmi">监视器信息输出</param>
        /// <returns>是否成功</returns>
        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        /// <summary>
        /// 设置窗口的位置和大小
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="hWndInsertAfter">插入后的窗口序</param>
        /// <param name="X">X 坐标</param>
        /// <param name="Y">Y 坐标</param>
        /// <param name="cx">宽度</param>
        /// <param name="cy">高度</param>
        /// <param name="uFlags">标志位</param>
        /// <returns>是否成功</returns>
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        #endregion

        // 是否处于全屏状态
        private bool _isFullScreen;

        // 进入全屏前的窗口位置和大小（物理像素，用于退出全屏时恢复，兼容 DPI 缩放）
        private RECT _restoreBounds;

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

            // 注册 ESC 键退出全屏
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        /// <summary>
        /// 预览按键事件：全屏状态下按 ESC 退出全屏
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">按键事件参数</param>
        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape && _isFullScreen)
            {
                ToggleFullScreen();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 切换全屏显示：进入/退出全屏
        /// 由于窗口是无边框且 AllowsTransparency=True，
        /// 使用 Win32 SetWindowPos 将窗口覆盖到当前监视器完整区域（含任务栏）实现真全屏
        /// </summary>
        public void ToggleFullScreen()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            if (_isFullScreen)
            {
                // 退出全屏：恢复进入全屏前保存的窗口物理像素矩形
                SetWindowPos(handle, IntPtr.Zero,
                    _restoreBounds.Left, _restoreBounds.Top,
                    _restoreBounds.Right - _restoreBounds.Left,
                    _restoreBounds.Bottom - _restoreBounds.Top,
                    SWP_NOZORDER);
                _isFullScreen = false;
            }
            else
            {
                // 进入全屏：先记录当前窗口的物理像素矩形（含位置和大小）
                GetWindowRect(handle, out _restoreBounds);

                // 获取窗口所在监视器的完整区域
                var monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
                var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(monitor, ref mi))
                {
                    SetWindowPos(handle, IntPtr.Zero,
                        mi.rcMonitor.Left, mi.rcMonitor.Top,
                        mi.rcMonitor.Right - mi.rcMonitor.Left,
                        mi.rcMonitor.Bottom - mi.rcMonitor.Top,
                        SWP_NOZORDER);
                }
                _isFullScreen = true;
            }
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
