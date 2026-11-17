using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using YF_Manager;

namespace YFrame
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 窗口圆角半径：与设计稿保持一致，全屏时改为 0
        /// </summary>
        private const double WindowCornerRadius = 14;

        /// <summary>
        /// 标题栏显示的版本号（取自程序集版本，避免写死）
        /// </summary>
        public string AppVersion { get; } =
            "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "2.0");
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

        // 首页当前选中的分页（0=欢迎，1=插件加载，2=性能自检，3=启动就绪）
        private int _homeSectionIndex;

        // 首页入场动画是否已执行，避免重复触发
        private bool _homeEntrancePlayed;

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

            // 圆角窗口外壳需要按实际尺寸裁剪，否则标题栏/内容会溢出圆角
            WindowFrame.SizeChanged += (_, _) => UpdateWindowClip();
            UpdateWindowClip();

            // 首次显示后按实际 DPI 重新居中（OnSourceInitialized 阶段 DPI 可能尚未就绪）
            Loaded += (_, _) => CenterOnWorkArea();

            // 注册 ESC 键退出全屏
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        /// <summary>
        /// 跨显示器移动导致 DPI 变化时，重新按新监视器工作区居中
        /// </summary>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            CenterOnWorkArea();
        }

        /// <summary>
        /// 按窗口当前尺寸生成圆角裁剪区域；全屏时使用直角避免边缘留白
        /// </summary>
        private void UpdateWindowClip()
        {
            if (WindowFrame.ActualWidth <= 0 || WindowFrame.ActualHeight <= 0)
                return;

            double radius = _isFullScreen ? 0 : WindowCornerRadius;
            WindowFrame.CornerRadius = new CornerRadius(radius);
            WindowFrame.Clip = new RectangleGeometry(
                new Rect(0, 0, WindowFrame.ActualWidth, WindowFrame.ActualHeight),
                radius, radius);
        }

        /// <summary>
        /// 预览按键：全屏下按 ESC 退出
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
        /// 无边框 + AllowsTransparency=True 时，用 Win32 SetWindowPos 覆盖到监视器完整区域实现真全屏
        /// </summary>
        public void ToggleFullScreen()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            if (_isFullScreen)
            {
                // 退出全屏：恢复保存的窗口物理像素矩形
                SetWindowPos(handle, IntPtr.Zero,
                    _restoreBounds.Left, _restoreBounds.Top,
                    _restoreBounds.Right - _restoreBounds.Left,
                    _restoreBounds.Bottom - _restoreBounds.Top,
                    SWP_NOZORDER);
                _isFullScreen = false;
            }
            else
            {
                // 进入全屏：记录当前窗口物理像素矩形
                GetWindowRect(handle, out _restoreBounds);

                // 获取所在监视器的完整区域
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

            // 全屏/还原后重新计算圆角裁剪
            UpdateWindowClip();
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
        /// 窗口消息源初始化完成后注册托盘图标、全局热键，并按监视器工作区居中
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 初始化托盘图标服务
            _trayIconService.Initialize(this);

            // 初始化全局热键服务，传入主窗口以注册 WndProc 消息钩子
            _hotkeyService.Initialize(this);

            // 高 DPI 下 WPF 的 CenterScreen 仍可能按物理工作区计算，这里按监视器工作区手动居中
            CenterOnWorkArea();
        }

        /// <summary>
        /// 按当前监视器的物理工作区计算 DIP 坐标并居中；工作区不足时收缩窗口高度
        /// </summary>
        private void CenterOnWorkArea()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(monitor, ref mi))
                return;

            // 物理像素 → DIP：WPF 的 Left/Top/Width/Height 使用 DIP
            var dpi = VisualTreeHelper.GetDpi(this);
            double workLeft = mi.rcWork.Left / dpi.DpiScaleX;
            double workTop = mi.rcWork.Top / dpi.DpiScaleY;
            double workWidth = (mi.rcWork.Right - mi.rcWork.Left) / dpi.DpiScaleX;
            double workHeight = (mi.rcWork.Bottom - mi.rcWork.Top) / dpi.DpiScaleY;

            // 预留少量边距，避免窗口紧贴任务栏/屏幕边缘
            const double margin = 12;
            if (Height > workHeight - margin * 2)
                Height = Math.Max(480, workHeight - margin * 2);

            Left = workLeft + Math.Max(0, (workWidth - Width) / 2);
            Top = workTop + Math.Max(0, (workHeight - Height) / 2);
        }
        /// <summary>
        /// 首页可见性变化：首次显示或从插件页返回时播放淡入/上浮动画
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">可见性变化参数</param>
        private void HomeOverviewRoot_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                // 等布局完成后再播放，避免测量阶段动画被重置
                Dispatcher.BeginInvoke(new Action(PlayHomeEntrance), DispatcherPriority.Loaded);
            }
            else
            {
                // 离开首页后允许下次返回时重新播放入场动画
                _homeEntrancePlayed = false;
            }
        }

        /// <summary>
        /// 首页入场动画：整体淡入，四张卡片依次上浮出现
        /// </summary>
        private void PlayHomeEntrance()
        {
            if (_homeEntrancePlayed || HomeOverviewScroll == null)
                return;

            _homeEntrancePlayed = true;

            // 整体内容淡入
            HomeOverviewScroll.Opacity = 0;
            HomeOverviewScroll.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

            // 四张卡片错峰进入，形成轻微层次感
            var cards = new FrameworkElement[] { HomeCard0, HomeCard1, HomeCard2, HomeCard3 };
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null) continue;

                card.Opacity = 0;
                var translate = card.RenderTransform as TranslateTransform;
                if (translate == null)
                {
                    translate = new TranslateTransform();
                    card.RenderTransform = translate;
                }
                translate.Y = 12;

                var delay = TimeSpan.FromMilliseconds(40 * i);
                card.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
                    {
                        BeginTime = delay,
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });
                translate.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(260))
                    {
                        BeginTime = delay,
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });
            }
        }

        /// <summary>
        /// 鼠标进入卡片：只改变独立高亮层的透明度，避免重绘整卡内容。
        /// </summary>
        private void HomeCard_MouseEnter(object sender, MouseEventArgs e)
            => SetHomeCardHover(sender, true);

        /// <summary>
        /// 鼠标离开卡片：淡出高亮层。
        /// </summary>
        private void HomeCard_MouseLeave(object sender, MouseEventArgs e)
            => SetHomeCardHover(sender, false);

        /// <summary>
        /// 设置卡片高亮层的目标透明度；使用 FillBehavior.Stop，避免留下动画时钟。
        /// </summary>
        private void SetHomeCardHover(object sender, bool isHover)
        {
            if (sender is not Border card) return;

            Border? overlay = card.Tag?.ToString() switch
            {
                "0" => HomeCard0Hover,
                "1" => HomeCard1Hover,
                "2" => HomeCard2Hover,
                "3" => HomeCard3Hover,
                _ => null
            };
            if (overlay == null) return;

            // 不做透明度动画，鼠标进入/离开立即切换，消除感知延迟
            overlay.BeginAnimation(OpacityProperty, null);
            overlay.Opacity = isHover ? 0.72 : 0;
        }
        /// <summary>
        /// 点击右侧折叠卡：切换到对应分页
        /// </summary>
        /// <param name="sender">被点击的卡片</param>
        /// <param name="e">鼠标事件参数</param>
        private void HomeCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element &&
                int.TryParse(element.Tag?.ToString(), out int index))
            {
                SelectHomeSection(index);
            }
        }

        /// <summary>
        /// 屏幕缩放下拉框：点击任意位置直接展开/收起。
        /// 模板中的 ToggleButton 在部分 DPI/窗口样式下收不到鼠标事件，这里统一兜底。
        /// </summary>
        /// <param name="sender">下拉框</param>
        /// <param name="e">鼠标事件参数</param>
        private void ScreenScaleComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;
                e.Handled = true;
            }
        }
        /// <summary>
        /// 切换首页手风琴：目标卡展开为宽卡，其余卡收起为 64px 竖卡
        /// </summary>
        /// <param name="index">分页索引：0=欢迎，1=插件加载，2=性能自检，3=启动就绪</param>
        private void SelectHomeSection(int index)
        {
            if (index < 0 || index > 3 || index == _homeSectionIndex)
                return;

            _homeSectionIndex = index;

            // 四张卡共用同一组列，只有当前卡占满剩余宽度
            var columns = new ColumnDefinition[] { HomeCol0, HomeCol1, HomeCol2, HomeCol3 };
            var cards = new Border[] { HomeCard0, HomeCard1, HomeCard2, HomeCard3 };
            var collapsedViews = new Grid[] { HomeCard0Collapsed, HomeCard1Collapsed, HomeCard2Collapsed, HomeCard3Collapsed };
            var expandedViews = new ScrollViewer[] { HomeCard0Expanded, HomeCard1Expanded, HomeCard2Expanded, HomeCard3Expanded };

            for (int i = 0; i < columns.Length; i++)
            {
                columns[i].Width = i == index
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(64);

                collapsedViews[i].Visibility = i == index ? Visibility.Collapsed : Visibility.Visible;
                expandedViews[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;

                // 展开卡保持普通描边，收起卡恢复默认样式
                cards[i].ClearValue(Border.BorderBrushProperty);
                cards[i].ClearValue(Border.BackgroundProperty);
            }

            HomePageBadge.Text = $"0{index + 1} / 04";

            // 新展开的内容从右侧滑入并淡入，复刻设计稿的 cardIn 动效
            var content = expandedViews[index];
            content.Opacity = 0;

            var translate = content.RenderTransform as TranslateTransform;
            if (translate == null)
            {
                translate = new TranslateTransform();
                content.RenderTransform = translate;
            }
            translate.X = 14;

            content.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            translate.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(320))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }    }
}
