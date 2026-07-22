using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common
{
    /// <summary>
    /// YF_DelegateFunctionModel 委托类型单元测试
    /// </summary>
    public class YF_DelegateFunctionModelTests
    {
        /// <summary>
        /// dvFunc_Vs 单参数委托可以正常调用
        /// </summary>
        [Fact]
        public void DvFunc_Vs_InvokesCorrectly()
        {
            string? received = null;
            YF_DelegateFunctionModel.dvFunc_Vs del = (str) => received = str;

            del("测试字符串");

            Assert.Equal("测试字符串", received);
        }

        /// <summary>
        /// dvFunc_Vs_s 双参数委托可以正常调用
        /// </summary>
        [Fact]
        public void DvFunc_Vs_s_InvokesCorrectly()
        {
            string? received1 = null;
            string? received2 = null;
            YF_DelegateFunctionModel.dvFunc_Vs_s del = (s1, s2) =>
            {
                received1 = s1;
                received2 = s2;
            };

            del("参数1", "参数2");

            Assert.Equal("参数1", received1);
            Assert.Equal("参数2", received2);
        }
    }
}
