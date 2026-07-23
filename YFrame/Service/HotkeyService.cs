using Castle.DynamicProxy;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// 全局热键服务：封装 Win32 RegisterHotKey/UnregisterHotKey 及 WndProc 消息处理，
    /// 通过事件通知订阅者热键被按下。降低 MainWindow 与 ViewModel 的耦合。
    /// </summary>
    public class HotkeyService
    {


        #region Win32 API
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        #endregion

        #region 常量
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_Y = 0x59;
        private const int HOTKEY_ID = 9001;
        private const int WM_HOTKEY = 0x0312;
        #endregion

        #region 事件
        /// <summary>
        /// 全局热键 Ctrl+Y 被按下时触发
        /// </summary>
        public event Action? OnHotkeyPressed;
        #endregion

        #region 状态
        private HwndSource? _hwndSource;
        private IntPtr _windowHandle;
        private bool _isRegistered;
        private bool _isInitialized;

        /// <summary>
        /// 热键是否已注册
        /// </summary>
        public bool IsRegistered => _isRegistered;
        #endregion

        public HotkeyService() { }

        /// <summary>
        /// 初始化热键服务，需传入主窗口句柄以注册 WndProc 消息钩子
        /// </summary>
        /// <param name="window">主窗口实例</param>
        [Log(Level = LogLevel.Info, Message = "初始化热键服务")]
        public virtual void Initialize(Window window)
        {
            if (_isInitialized) return;

            var helper = new WindowInteropHelper(window);
            _windowHandle = helper.Handle;
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(WndProc);
            _isInitialized = true;
        }

        /// <summary>
        /// 注册全局热键 Ctrl+Y
        /// </summary>
        /// <returns>注册成功返回 true</returns>
        [Log(Level = LogLevel.Info, Message = "注册全局热键")]
        public virtual bool Register()
        {
            if (_isRegistered) return true;
            if (!_isInitialized) return false;

            if (RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_CONTROL, VK_Y))
            {
                _isRegistered = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 注销全局热键 Ctrl+Y
        /// </summary>
        /// <returns>注销成功返回 true</returns>
        [Log(Level = LogLevel.Info, Message = "注销全局热键")]
        public virtual bool Unregister()
        {
            if (!_isRegistered) return true;

            if (UnregisterHotKey(_windowHandle, HOTKEY_ID))
            {
                _isRegistered = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 窗口消息处理钩子：拦截 WM_HOTKEY 消息并触发事件
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                OnHotkeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}
