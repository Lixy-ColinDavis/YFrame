using Castle.DynamicProxy;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// 托盘服务
    /// </summary>
    public class TrayIconService
    {
        #region Win32 API
        [DllImport("shell32.dll")]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr tpmpParams);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
        #endregion

        #region 常量
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;

        private const uint WM_TRAYICON = 0x8000;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONUP = 0x0205;

        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint TPM_RIGHTALIGN = 0x0008;
        private const uint TPM_BOTTOMALIGN = 0x0020;

        private const uint TRAY_ID = 1;
        private const uint MENU_ID_SHOW = 1001;
        private const uint MENU_ID_EXIT = 1002;
        #endregion

        #region 事件
        /// <summary>
        /// 托盘左键单击或菜单"显示主窗口"触发
        /// </summary>
        public event Action? OnShowWindow;

        /// <summary>
        /// 托盘右键菜单"退出"触发
        /// </summary>
        public event Action? OnExitApplication;
        #endregion

        #region 状态
        private HwndSource? _hwndSource;
        private IntPtr _windowHandle;
        private IntPtr _hTrayIcon;
        private bool _isInitialized;
        private bool _isExiting;

        /// <summary>
        /// 是否正在执行退出流程
        /// </summary>
        public bool IsExiting => _isExiting;
        #endregion

        public TrayIconService() { }

        /// <summary>
        /// 初始化托盘图标服务，创建托盘图标并注册 WndProc 消息钩子
        /// </summary>
        /// <param name="window">主窗口实例</param>
        [Log(Level = LogLevel.Info, Message = "初始化托盘图标服务")]
        public virtual void Initialize(Window window)
        {
            if (_isInitialized) return;

            var helper = new WindowInteropHelper(window);
            _windowHandle = helper.Handle;
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(WndProc);

            // 从 exe 提取图标
            _hTrayIcon = LoadIconFromExe();
            AddTrayIcon();

            _isInitialized = true;
        }

        /// <summary>
        /// 标记进入退出流程（阻止关闭被拦截）
        /// </summary>
        public void MarkExiting()
        {
            _isExiting = true;
        }

        /// <summary>
        /// 移除托盘图标并清理资源
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "移除托盘图标")]
        public virtual void Cleanup()
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _windowHandle,
                uID = TRAY_ID
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);

            if (_hTrayIcon != IntPtr.Zero)
            {
                DestroyIcon(_hTrayIcon);
                _hTrayIcon = IntPtr.Zero;
            }
            _isInitialized = false;
        }

        /// <summary>
        /// 从当前 exe 提取应用程序图标
        /// </summary>
        private IntPtr LoadIconFromExe()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return IntPtr.Zero;
            return ExtractIcon(IntPtr.Zero, exePath, 0);
        }

        /// <summary>
        /// 向系统托盘添加图标
        /// </summary>
        private void AddTrayIcon()
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _windowHandle,
                uID = TRAY_ID,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hTrayIcon,
                szTip = "YF Tools"
            };

            Shell_NotifyIcon(NIM_ADD, ref nid);
        }

        /// <summary>
        /// 显示托盘右键弹出菜单
        /// </summary>
        private void ShowContextMenu()
        {
            var menu = CreatePopupMenu();
            AppendMenu(menu, MF_STRING, MENU_ID_SHOW, "显示主窗口");
            AppendMenu(menu, MF_SEPARATOR, 0, string.Empty);
            AppendMenu(menu, MF_STRING, MENU_ID_EXIT, "退出");

            GetCursorPos(out var pt);
            SetForegroundWindow(_windowHandle);
            TrackPopupMenuEx(menu, TPM_RIGHTALIGN | TPM_BOTTOMALIGN, pt.x, pt.y, _windowHandle, IntPtr.Zero);
            DestroyMenu(menu);
        }

        /// <summary>
        /// 窗口消息处理钩子：拦截托盘消息和菜单命令
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                var lVal = lParam.ToInt32();
                if (lVal == WM_RBUTTONUP)
                {
                    ShowContextMenu();
                    handled = true;
                }
                else if (lVal == WM_LBUTTONUP)
                {
                    OnShowWindow?.Invoke();
                    handled = true;
                }
            }
            else if (msg == WM_COMMAND)
            {
                var menuId = wParam.ToInt32() & 0xFFFF;
                if (menuId == MENU_ID_SHOW)
                {
                    OnShowWindow?.Invoke();
                    handled = true;
                }
                else if (menuId == MENU_ID_EXIT)
                {
                    // 先标记退出状态，再触发事件
                    _isExiting = true;
                    OnExitApplication?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }
    }
}
