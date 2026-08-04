using System.Windows;
using System.Windows.Controls;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// 插件管理服务
    /// </summary>
    public class PluginService
    {
        #region 成员变量

        /// <summary>日志对象（由外部注入）</summary>
        private readonly YF_Manager_Log _logger;

        /// <summary>插件显示区域的 Grid 控件（由外部注入）</summary>
        private Grid? _gridShowArea;

        /// <summary>当前显示的插件数据</summary>
        public CtrlDataModel? CurrentPlugin { get; private set; }

        #endregion

        #region 依赖

        /// <summary>消息中介（DI 注入）</summary>
        private readonly YF_Messenger _messenger;

        /// <summary>插件加载服务（DI 注入）</summary>
        private readonly UserControlsService _userControlsService;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建插件管理服务
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="messenger">消息中介（DI 注入）</param>
        /// <param name="userControlsService">插件加载服务（DI 注入）</param>
        public PluginService(YF_Manager_Log logger, YF_Messenger messenger, UserControlsService userControlsService)
        {
            _logger = logger;
            _messenger = messenger;
            _userControlsService = userControlsService;

            // 订阅插件显示请求
            messenger.Register<PluginShownMessage>(msg =>
            {
                ShowPluginInternal(msg.PluginId);
            });

            // 订阅热键触发
            messenger.Register<HotkeyTriggeredMessage>(_ =>
            {
                OnHotkeyPressedInternal();
            });

            // 订阅脚本命令
            messenger.Register<ScriptCommandMessage>(msg =>
            {
                ExecuteScriptCommand(msg.Command);
            });

            _logger.LogInfo("PluginService 初始化完成，已订阅插件/热键/脚本消息");
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 设置插件显示区域（由 MainWindowViewModel 在 InitUI 时调用）
        /// </summary>
        public void SetGridShowArea(Grid grid)
        {
            _gridShowArea = grid;
        }

        #endregion

        #region 插件显示

        /// <summary>
        /// 卸载当前显示的插件（清空显示区并重置引用）
        /// </summary>
        public void UnloadCurrentPlugin()
        {
            if (_gridShowArea != null)
            {
                _gridShowArea.Children.Clear();
            }
            CurrentPlugin = null;
            _logger.DebugInfo("当前插件已卸载");
        }

        /// <summary>
        /// 显示指定 ID 的插件（公开入口，由命令绑定调用）
        /// </summary>
        public bool ShowPlugin(string pluginId)
        {
            return ShowPluginInternal(pluginId);
        }

        /// <summary>
        /// 内部插件显示逻辑：从 UserControlsService 获取控件并放入显示区
        /// </summary>
        private bool ShowPluginInternal(string pluginId)
        {
            try
            {
                if (_gridShowArea == null)
                {
                    _logger.ErrorInfo("PluginService.ShowPlugin", "Grid 显示区域未设置");
                    return false;
                }

                _gridShowArea.Children.Clear();

                if (!_userControlsService.DctControls.TryGetValue(pluginId, out var ctrlData))
                {
                    _logger.ErrorInfo("PluginService.ShowPlugin", $"插件 {pluginId} 未找到");
                    return false;
                }

                CurrentPlugin = ctrlData;
                _userControlsService.ShowUserControl(pluginId);
                UserControl? uc = CurrentPlugin.userControl;
                if (uc != null)
                {
                    _gridShowArea.Children.Add(uc);
                    // 通过 Mediator 通知：插件已切换
                    _messenger.Send(new LogAppendMessage($"已切换到插件: {pluginId}"));
                    return true;
                }
                else
                {
                    _logger.ErrorInfo("PluginService.ShowPlugin", $"插件 {pluginId} 加载失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("PluginService.ShowPlugin", ex.Message);
                return false;
            }
        }

        #endregion

        #region 命令转发

        /// <summary>
        /// 向当前激活的插件发送命令
        /// </summary>
        public void SendCommand(string command, object? parameter = null)
        {
            try
            {
                if (CurrentPlugin == null || CurrentPlugin.CommandHandler == null)
                {
                    _logger.CommandInfo("[命令无目标插件] : " + command);
                }
                else
                {
                    CurrentPlugin.CommandHandler.ExecuteCommand(command, parameter);
                    _logger.CommandInfo(command);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("PluginService.SendCommand", ex.Message);
            }
        }

        #endregion

        #region 热键路由

        /// <summary>
        /// Ctrl+Y 热键路由：根据当前激活的插件 ID 分发对应命令
        /// </summary>
        public void OnHotkeyPressed()
        {
            OnHotkeyPressedInternal();
        }

        private void OnHotkeyPressedInternal()
        {
            try
            {
                if (CurrentPlugin?.CommandHandler == null)
                {
                    _logger.LogInfo("热键 Ctrl+Y 触发，但没有激活的插件");
                    return;
                }

                // 统一向当前插件发送热键命令，由插件自行决定是否处理
                CurrentPlugin.CommandHandler.ExecuteCommand("Hotkey");
                _logger.CommandInfo("热键 Ctrl+Y 触发，已向当前插件发送 Hotkey 命令");
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("PluginService.OnHotkeyPressed", ex.Message);
            }
        }

        #endregion

        #region 脚本操作

        /// <summary>
        /// 向当前插件发送脚本操作命令（New / Open / Save）
        /// </summary>
        /// <param name="command">New / Open / Save</param>
        public void ExecuteScriptCommand(string command)
        {
            try
            {
                if (CurrentPlugin?.CommandHandler == null)
                {
                    _logger.LogInfo("没有激活的插件，无法执行脚本操作: " + command);
                    return;
                }

                // 统一命令名直接转发，由插件自行决定是否处理
                CurrentPlugin.CommandHandler.ExecuteCommand(command);
                _logger.LogInfo($"脚本操作 '{command}' 已发送到插件");
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("PluginService.ExecuteScriptCommand", ex.Message);
            }
        }

        #endregion

        #region 插件回调处理

        /// <summary>
        /// 处理插件回调，转发到日志面板
        /// </summary>
        public void HandlePluginCallback(string pluginId, PluginEventArgs e)
        {
            try
            {
                string msg = $"[{pluginId}] 命令:{e.Command} 内容:{e.Data} 时间:{e.Timestamp:HH:mm:ss}";
                _logger.LogInfo(msg);

                // 通过 Mediator 发送到日志面板
                _messenger.Send(new LogAppendMessage(msg));
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("PluginService.HandlePluginCallback", ex.Message);
            }
        }

        #endregion
    }
}
