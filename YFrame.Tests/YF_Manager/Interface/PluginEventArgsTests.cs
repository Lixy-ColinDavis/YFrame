using YF_Manager;

namespace YFrame.Tests.YF_Manager.Interface
{
    /// <summary>
    /// PluginEventArgs 单元测试
    /// </summary>
    public class PluginEventArgsTests
    {
        /// <summary>
        /// 默认构造后属性可正常读写
        /// </summary>
        [Fact]
        public void Properties_CanBeSetAndRead()
        {
            var now = DateTime.UtcNow;
            var args = new PluginEventArgs
            {
                PluginId = "YF_Test",
                Command = "TestCommand",
                Data = new { Key = "Value" },
                Timestamp = now
            };

            Assert.Equal("YF_Test", args.PluginId);
            Assert.Equal("TestCommand", args.Command);
            Assert.NotNull(args.Data);
            Assert.Equal(now, args.Timestamp);
        }

        /// <summary>
        /// 默认值验证
        /// </summary>
        [Fact]
        public void DefaultValues_AreExpected()
        {
            var args = new PluginEventArgs();

            Assert.Null(args.PluginId);
            Assert.Null(args.Command);
            Assert.Null(args.Data);
            Assert.Equal(default, args.Timestamp);
        }
    }
}
