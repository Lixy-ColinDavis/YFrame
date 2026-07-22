using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common
{
    /// <summary>
    /// LogAttribute 和 LogLevel 枚举单元测试
    /// </summary>
    public class LogAttributeTests
    {
        /// <summary>
        /// 默认构造函数 Level 应为 LogLevel.Info
        /// </summary>
        [Fact]
        public void DefaultConstructor_LevelIsInfo()
        {
            var attr = new LogAttribute();
            Assert.Equal(LogLevel.Info, attr.Level);
        }

        /// <summary>
        /// 可以传入指定 LogLevel
        /// </summary>
        [Fact]
        public void Constructor_WithLevel_SetsCorrectly()
        {
            var attr = new LogAttribute(LogLevel.Error);
            Assert.Equal(LogLevel.Error, attr.Level);
        }

        /// <summary>
        /// Message 属性可设置和读取
        /// </summary>
        [Fact]
        public void Message_Property_SetAndGet()
        {
            var attr = new LogAttribute { Message = "测试消息" };
            Assert.Equal("测试消息", attr.Message);
        }

        /// <summary>
        /// AttributeUsage 限定只能用于方法
        /// </summary>
        [Fact]
        public void AttributeUsage_TargetsMethod()
        {
            var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
                typeof(LogAttribute), typeof(AttributeUsageAttribute))!;
            Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Method));
            Assert.False(usage.Inherited);
        }

        /// <summary>
        /// LogLevel 枚举值验证
        /// </summary>
        [Fact]
        public void LogLevel_AllValues_Exist()
        {
            Assert.Equal(0, (int)LogLevel.Debug);
            Assert.Equal(1, (int)LogLevel.Info);
            Assert.Equal(2, (int)LogLevel.Warning);
            Assert.Equal(3, (int)LogLevel.Error);
        }
    }
}
