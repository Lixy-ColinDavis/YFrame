using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace YF_Manager
{
    public class YF_TcpHelper
    {
        // AOP 日志拦截，采用 ProxyGenerator 模式：
        private static readonly Lazy<YF_TcpHelper> _instance =
            new Lazy<YF_TcpHelper>(() =>
                new Castle.DynamicProxy.ProxyGenerator()
                    .CreateClassProxy<YF_TcpHelper>(new LogInterceptor()));

        public static YF_TcpHelper Instance => _instance.Value;

        internal YF_TcpHelper() { }

        /// <summary>
        /// 获取本机默认IP（遍历活跃网卡的首个 IPv4 单播地址）
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "获取本机默认IP")]
        public virtual IPAddress? GetDefaultGatewayIP()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            return addr.Address;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger?.ErrorInfo("GetDefaultGatewayIP", ex.Message);
            }
            return null;
        }

        /// <summary>
        /// 读取本机默认IP
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "读取本机默认IP")]
        public virtual string GetLocalIP()
        {
            try
            {
                var localIP = Dns.GetHostEntry(Dns.GetHostName())
                    .AddressList
                    .FirstOrDefault(ip => ip.AddressFamily ==
                        System.Net.Sockets.AddressFamily.InterNetwork);

                if (localIP != null)
                    return localIP.ToString();

                YF_Manager_Main.logger?.ErrorInfo("GetLocalIP", "未找到 IPv4 地址，使用回退地址");
                return "127.0.0.1";
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger?.ErrorInfo("GetLocalIP", ex.Message);
                return "127.0.0.1";
            }
        }
    }
}
