using System.Windows.Controls;
using YF_Manager;

namespace YFrame.Tests.YFrame.Service
{
    /// <summary>
    /// PluginService 单元测试
    /// </summary>
    public class PluginServiceTests
    {
        #region 测试基础设施

        private readonly YF_Manager_Log _logger;
        private readonly YF_Messenger _messenger;
        private readonly TestUserControlsService _ucService;

        public PluginServiceTests()
        {
            _logger = new YF_Manager_Log("PluginService测试", "Test", new YF_FileHelper());
            _messenger = new YF_Messenger();
            _ucService = new TestUserControlsService();
        }

        /// <summary>
        /// 测试用 UserControlsService 子类，跳过磁盘访问，由测试代码直接控制 DctControls
        /// </summary>
        private class TestUserControlsService : UserControlsService
        {
            public override void ShowUserControl(string plugin_Id)
            {
                // 测试不访问磁盘，由外部测试代码预先填充 DctControls
            }
        }

        /// <summary>
        /// WPF UI 元素（Grid/UserControl）需要在 STA 线程中创建
        /// </summary>
        private static void RunOnSta(Action action)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
                return;
            }

            Exception? capturedException = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { capturedException = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (capturedException != null)
                throw capturedException;
        }

        #endregion

        #region 构造函数测试

        /// <summary>
        /// 构造函数应注册 PluginShownMessage 消息处理器
        /// </summary>
        [Fact]
        public void Constructor_RegistersPluginShownMessage()
        {
            var mockCmd = CreateMockCommand(out _);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_TestPlugin"] = CreateCtrlDataModelWithUC("测试插件", mockCmd);
                var service = CreateServiceWithGrid();
                _messenger.Send(new PluginShownMessage("YF_TestPlugin"));
                Assert.NotNull(service.CurrentPlugin);
            });
        }

        /// <summary>
        /// 构造函数应注册 HotkeyTriggeredMessage 消息处理器
        /// </summary>
        [Fact]
        public void Constructor_RegistersHotkeyTriggeredMessage()
        {
            var mockCmd = CreateMockCommand(out var receivedCommands);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_Clicker"] = CreateCtrlDataModelWithUC("点击器", mockCmd);
                _ucService.DctControls["YF_Other"] = CreateCtrlDataModelWithUC("其他", mockCmd);
                var service = CreateServiceWithGrid();
                _messenger.Send(new PluginShownMessage("YF_Clicker"));
                _messenger.Send(new HotkeyTriggeredMessage());

                Assert.Contains("ToggleClick", receivedCommands);
            });
        }

        /// <summary>
        /// 构造函数应注册 ScriptCommandMessage 消息处理器
        /// </summary>
        [Fact]
        public void Constructor_RegistersScriptCommandMessage()
        {
            var mockCmd = CreateMockCommand(out var receivedCommands);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_Test"] = CreateCtrlDataModelWithUC("测试", mockCmd);
                var service = CreateServiceWithGrid();
                _messenger.Send(new PluginShownMessage("YF_Test"));
                _messenger.Send(new ScriptCommandMessage("NewScript"));

                Assert.Contains("NewScript", receivedCommands);
            });
        }

        #endregion

        #region ShowPlugin 测试

        /// <summary>
        /// Grid 显示区域未设置时返回 false
        /// </summary>
        [Fact]
        public void ShowPlugin_ReturnsFalseWhenGridNotSet()
        {
            var service = new PluginService(_logger, _messenger, _ucService);

            bool result = service.ShowPlugin("任意插件");

            Assert.False(result);
        }

        /// <summary>
        /// 请求的插件在字典中不存在时返回 false
        /// </summary>
        [Fact]
        public void ShowPlugin_ReturnsFalseWhenPluginNotFound()
        {
            RunOnSta(() =>
            {
                var service = CreateServiceWithGrid();
                bool result = service.ShowPlugin("不存在的插件");
                Assert.False(result);
            });
        }

        /// <summary>
        /// 成功显示插件时返回 true 并设置 CurrentPlugin
        /// </summary>
        [Fact]
        public void ShowPlugin_ReturnsTrueAndSetsCurrentPlugin()
        {
            var mockCmd = CreateMockCommand(out _);

            RunOnSta(() =>
            {
                var ctrlData = CreateCtrlDataModelWithUC("成功插件", mockCmd);
                _ucService.DctControls["YF_Success"] = ctrlData;
                var service = CreateServiceWithGrid();
                bool result = service.ShowPlugin("YF_Success");

                Assert.True(result);
                Assert.NotNull(service.CurrentPlugin);
                Assert.Same(ctrlData, service.CurrentPlugin);
            });
        }

        #endregion

        #region SetGridShowArea 测试

        /// <summary>
        /// 设置 Grid 显示区域后 ShowPlugin 可正常工作
        /// </summary>
        [Fact]
        public void SetGridShowArea_StoresGridReference()
        {
            var mockCmd = CreateMockCommand(out _);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_Test"] = CreateCtrlDataModelWithUC("测试", mockCmd);
                var grid = new Grid();
                var service = new PluginService(_logger, _messenger, _ucService);
                service.SetGridShowArea(grid);

                bool result = service.ShowPlugin("YF_Test");
                Assert.True(result);
            });
        }

        #endregion

        #region SendCommand 测试

        /// <summary>
        /// 向当前插件转发命令
        /// </summary>
        [Fact]
        public void SendCommand_ExecutesOnCurrentPlugin()
        {
            var mockCmd = CreateMockCommand(out var receivedCommands);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_Target"] = CreateCtrlDataModelWithUC("目标", mockCmd);
                var service = CreateServiceWithGrid();
                service.ShowPlugin("YF_Target");
                service.SendCommand("自定义命令", 12345);

                Assert.Contains("自定义命令", receivedCommands);
            });
        }

        /// <summary>
        /// 无激活插件时不抛异常
        /// </summary>
        [Fact]
        public void SendCommand_HandlesNullPlugin()
        {
            var service = new PluginService(_logger, _messenger, _ucService);

            var exception = Record.Exception(() => service.SendCommand("无目标"));
            Assert.Null(exception);
        }

        /// <summary>
        /// 插件存在但 CommandHandler 为 null 时不抛异常
        /// </summary>
        [Fact]
        public void SendCommand_HandlesNullCommandHandler()
        {
            RunOnSta(() =>
            {
                _ucService.DctControls["YF_NoHandler"] = new CtrlDataModel
                {
                    Name = "无处理器",
                    CommandHandler = null!,
                    userControl = new UserControl()
                };
                var service = CreateServiceWithGrid();
                service.ShowPlugin("YF_NoHandler");

                var exception = Record.Exception(() => service.SendCommand("测试命令"));
                Assert.Null(exception);
            });
        }

        #endregion

        #region OnHotkeyPressed 测试

        /// <summary>
        /// 无激活插件时热键按下不抛异常
        /// </summary>
        [Fact]
        public void OnHotkeyPressed_DoesNotThrowWhenNoPlugin()
        {
            var service = new PluginService(_logger, _messenger, _ucService);

            var exception = Record.Exception(() => service.OnHotkeyPressed());
            Assert.Null(exception);
        }

        /// <summary>
        /// ScreenOCRTranslate 插件激活时热键发送 CaptureScreen 命令
        /// </summary>
        [Fact]
        public void OnHotkeyPressed_SendsCaptureScreenToOCRPlugin()
        {
            var mockCmd = CreateMockCommand(out var receivedCommands);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_ScreenOCRTranslate"] = CreateCtrlDataModelWithUC("OCR", mockCmd);
                var service = CreateServiceWithGrid();
                service.ShowPlugin("YF_ScreenOCRTranslate");
                service.OnHotkeyPressed();

                Assert.Contains("CaptureScreen", receivedCommands);
            });
        }

        /// <summary>
        /// Clicker 插件激活时热键发送 ToggleClick 命令
        /// </summary>
        [Fact]
        public void OnHotkeyPressed_SendsToggleClickToClickerPlugin()
        {
            var mockCmd = CreateMockCommand(out var receivedCommands);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_Clicker"] = CreateCtrlDataModelWithUC("点击器", mockCmd);
                var service = CreateServiceWithGrid();
                service.ShowPlugin("YF_Clicker");
                service.OnHotkeyPressed();

                Assert.Contains("ToggleClick", receivedCommands);
            });
        }

        /// <summary>
        /// 其他未知插件激活时热键发送 HotkeyTrigger 命令
        /// </summary>
        [Fact]
        public void OnHotkeyPressed_SendsHotkeyTriggerToOtherPlugins()
        {
            var mockCmd = CreateMockCommand(out var receivedCommands);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_OtherPlugin"] = CreateCtrlDataModelWithUC("其他", mockCmd);
                var service = CreateServiceWithGrid();
                service.ShowPlugin("YF_OtherPlugin");
                service.OnHotkeyPressed();

                Assert.Contains("HotkeyTrigger", receivedCommands);
            });
        }

        #endregion

        #region ExecuteScriptCommand 测试

        /// <summary>
        /// 将 NewScript 命令转发到插件
        /// </summary>
        [Fact]
        public void ExecuteScriptCommand_NewScript_ForwardsToPlugin()
        {
            var mockCmd = CreateMockCommand(out var receivedCommands);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_KMScript"] = CreateCtrlDataModelWithUC("脚本", mockCmd);
                var service = CreateServiceWithGrid();
                service.ShowPlugin("YF_KMScript");
                service.ExecuteScriptCommand("NewScript");

                Assert.Contains("NewScript", receivedCommands);
            });
        }

        /// <summary>
        /// 将 OpenScript 命令转发到插件
        /// </summary>
        [Fact]
        public void ExecuteScriptCommand_OpenScript_ForwardsToPlugin()
        {
            var mockCmd = CreateMockCommand(out var receivedCommands);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_KMScript"] = CreateCtrlDataModelWithUC("脚本", mockCmd);
                var service = CreateServiceWithGrid();
                service.ShowPlugin("YF_KMScript");
                service.ExecuteScriptCommand("OpenScript");

                Assert.Contains("OpenScript", receivedCommands);
            });
        }

        /// <summary>
        /// 将 SaveScript 命令转换为 TriggerSave 转发到插件
        /// </summary>
        [Fact]
        public void ExecuteScriptCommand_SaveScript_MapsToTriggerSave()
        {
            var mockCmd = CreateMockCommand(out var receivedCommands);

            RunOnSta(() =>
            {
                _ucService.DctControls["YF_KMScript"] = CreateCtrlDataModelWithUC("脚本", mockCmd);
                var service = CreateServiceWithGrid();
                service.ShowPlugin("YF_KMScript");
                service.ExecuteScriptCommand("SaveScript");

                Assert.Contains("TriggerSave", receivedCommands);
            });
        }

        /// <summary>
        /// 无激活插件时执行脚本命令不抛异常
        /// </summary>
        [Fact]
        public void ExecuteScriptCommand_HandlesNullPlugin()
        {
            var service = new PluginService(_logger, _messenger, _ucService);

            var exception = Record.Exception(() => service.ExecuteScriptCommand("NewScript"));
            Assert.Null(exception);
        }

        #endregion

        #region HandlePluginCallback 测试

        /// <summary>
        /// 处理插件回调消息，将信息发送到 Mediator
        /// </summary>
        [Fact]
        public void HandlePluginCallback_SendsLogMessage()
        {
            string? capturedMessage = null;
            _messenger.Register<LogAppendMessage>(msg => capturedMessage = msg.Text);

            var service = new PluginService(_logger, _messenger, _ucService);

            var eventArgs = new PluginEventArgs
            {
                PluginId = "YF_Test",
                Command = "测试命令",
                Data = "测试数据",
                Timestamp = new DateTime(2026, 7, 24, 10, 30, 0)
            };

            service.HandlePluginCallback("YF_Test", eventArgs);

            Assert.NotNull(capturedMessage);
            Assert.Contains("YF_Test", capturedMessage);
            Assert.Contains("测试命令", capturedMessage);
            Assert.Contains("测试数据", capturedMessage);
            Assert.Contains("10:30:00", capturedMessage);
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 在 STA 线程上创建 PluginService 并设置 Grid
        /// </summary>
        private PluginService CreateServiceWithGrid()
        {
            var service = new PluginService(_logger, _messenger, _ucService);
            service.SetGridShowArea(new Grid());
            return service;
        }

        /// <summary>
        /// 创建带 UserControl 的 CtrlDataModel（必须在 STA 线程调用）
        /// </summary>
        private static CtrlDataModel CreateCtrlDataModelWithUC(string name, I_YF_Command commandHandler)
        {
            return new CtrlDataModel
            {
                Name = name,
                CommandHandler = commandHandler,
                userControl = new UserControl(),
            };
        }

        /// <summary>
        /// 创建 Mock 命令处理器，捕获所有接收到的命令名
        /// </summary>
        private static I_YF_Command CreateMockCommand(out List<string> receivedCommands)
        {
            var commands = new List<string>();
            receivedCommands = commands;
            return new MockCommand(commands);
        }

        /// <summary>
        /// 测试用 Mock 命令处理器，记录所有接收到的命令
        /// </summary>
        private class MockCommand : I_YF_Command
        {
            private readonly List<string> _receivedCommands;

            public MockCommand(List<string> receivedCommands)
            {
                _receivedCommands = receivedCommands;
            }

            public event EventHandler<PluginEventArgs>? OnPluginCallback;

            public void ExecuteCommand(string command, object parameter = null!)
            {
                _receivedCommands.Add(command);
            }
        }

        #endregion
    }
}
