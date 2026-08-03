using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common.Tools
{
    /// <summary>
    /// YF_TcpHelper 单元测试
    /// </summary>
    public class YF_TcpHelperTests
    {
        /// <summary>
        /// GetLocalIP 返回非空字符串（在联网环境中）
        /// </summary>
        [Fact]
        public void GetLocalIP_ReturnsNonEmptyString()
        {
            var helper = new YF_TcpHelper();
            string result = helper.GetLocalIP();

            Assert.False(string.IsNullOrEmpty(result));
        }

        /// <summary>
        /// GetLocalIP 返回合法的 IPv4 格式
        /// </summary>
        [Fact]
        public void GetLocalIP_ReturnsValidIpFormat()
        {
            var helper = new YF_TcpHelper();
            string result = helper.GetLocalIP();

            Assert.Matches(@"^(\d{1,3}\.){3}\d{1,3}$", result);
        }

        /// <summary>
        /// GetDefaultGatewayIP 方法调用不崩溃
        /// </summary>
        [Fact]
        public void GetDefaultGatewayIP_DoesNotThrow()
        {
            var helper = new YF_TcpHelper();

            // 在网络环境中应能正常执行，不抛异常
            var exception = Record.Exception(() => helper.GetDefaultGatewayIP());

            Assert.Null(exception);
        }

        /// <summary>
        /// YF_TcpHelper 可以正常实例化
        /// </summary>
        [Fact]
        public void CanBeInstantiated()
        {
            var helper = new YF_TcpHelper();
            Assert.NotNull(helper);
        }

        /// <summary>
        /// AOP 单例 Instance 不为 null（需要 Castle.Core 正常加载）
        /// </summary>
        [Fact]
        public void AopInstance_IsNotNull()
        {
            var instance = YF_TcpHelper.Instance;
            Assert.NotNull(instance);
        }
    }
}
