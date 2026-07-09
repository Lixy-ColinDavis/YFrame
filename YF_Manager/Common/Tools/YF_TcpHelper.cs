using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YF_Manager
{
    public class YF_TcpHelper
    {
        // 单例实例
        public static YF_TcpHelper Instance { get; } = new YF_TcpHelper();

        public YF_TcpHelper() { }  // 私有构造函数

        /// <summary>
        /// 获取默认网关IP
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "获取默认网关IP")]
        public virtual IPAddress? GetDefaultGatewayIP()
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "route.exe";
                process.StartInfo.Arguments = "print";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(5000))
                {
                    process.Kill();
                    YF_Manager_Main.logger.ErrorInfo("GetDefaultGatewayIP", "route.exe 执行超时");
                    return null;
                }

                Match match = Regex.Match(output, @"0.0.0.0\s+0.0.0.0\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)");
                if (match.Success)
                    return IPAddress.Parse(match.Groups[^1].Value);
                return null;
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger.ErrorInfo("GetDefaultGatewayIP", ex.Message);
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
