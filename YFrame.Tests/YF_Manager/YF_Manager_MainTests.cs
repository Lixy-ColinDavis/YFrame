using YF_Manager;

namespace YFrame.Tests.YF_Manager
{
    /// <summary>
    /// YF_Manager_Main 单元测试
    /// </summary>
    public class YF_Manager_MainTests
    {
        /// <summary>
        /// YF_ID 应为 "YF_Manager"
        /// </summary>
        [Fact]
        public void YF_ID_IsCorrect()
        {
            var main = new YF_Manager_Main();
            Assert.Equal("YF_Manager", main.YF_ID);
        }

        /// <summary>
        /// YF_Name 应为 "主控类"
        /// </summary>
        [Fact]
        public void YF_Name_IsCorrect()
        {
            var main = new YF_Manager_Main();
            Assert.Equal("主控类", main.YF_Name);
        }

        /// <summary>
        /// 实现了 I_YF_Detail 接口
        /// </summary>
        [Fact]
        public void Implements_I_YF_Detail()
        {
            var main = new YF_Manager_Main();
            Assert.IsAssignableFrom<I_YF_Detail>(main);
        }

        /// <summary>
        /// 静态构造函数确保 logger 在类首次引用时就初始化
        /// </summary>
        [Fact]
        public void StaticLogger_IsNotNull()
        {
            // 访问任意静态成员触发静态构造函数
            Assert.NotNull(YF_Manager_Main.logger);
        }
    }
}
