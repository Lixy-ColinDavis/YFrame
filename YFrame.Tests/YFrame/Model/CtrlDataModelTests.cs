using YFrame;
using YF_Manager;

namespace YFrame.Tests.YFrame.Model
{
    /// <summary>
    /// CtrlDataModel 单元测试
    /// </summary>
    public class CtrlDataModelTests
    {
        /// <summary>
        /// Name 属性可读写
        /// </summary>
        [Fact]
        public void Name_CanBeSetAndRead()
        {
            var model = new CtrlDataModel { Name = "测试插件" };
            Assert.Equal("测试插件", model.Name);
        }

        /// <summary>
        /// Parameters 字典默认不为 null 且为空
        /// </summary>
        [Fact]
        public void Parameters_IsNotNullAndEmpty()
        {
            var model = new CtrlDataModel();
            Assert.NotNull(model.Parameters);
            Assert.Empty(model.Parameters);
        }

        /// <summary>
        /// Parameters 字典可添加和读取
        /// </summary>
        [Fact]
        public void Parameters_CanAddAndRead()
        {
            var model = new CtrlDataModel();
            model.Parameters["key1"] = "value1";
            model.Parameters["key2"] = 42;

            Assert.Equal("value1", model.Parameters["key1"]);
            Assert.Equal(42, model.Parameters["key2"]);
        }

        /// <summary>
        /// 可存储 I_YF_Command 实现
        /// </summary>
        [Fact]
        public void CommandHandler_CanBeSet()
        {
            // 创建一个最小 I_YF_Command 实现来测试
            var handler = new MockCommandHandler();
            var model = new CtrlDataModel { CommandHandler = handler };

            Assert.NotNull(model.CommandHandler);
            Assert.Same(handler, model.CommandHandler);
        }

        /// <summary>
        /// Mock 命令处理器，用于测试 CtrlDataModel
        /// </summary>
        private class MockCommandHandler : I_YF_Command
        {
            public event EventHandler<PluginEventArgs>? OnPluginCallback;

            public void ExecuteCommand(string command, object parameter = null!)
            {
                OnPluginCallback?.Invoke(this, new PluginEventArgs
                {
                    PluginId = "Mock",
                    Command = command
                });
            }
        }
    }
}
