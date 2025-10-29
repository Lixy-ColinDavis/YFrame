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
        /// <summary>
        /// 获取默认网关IP
        /// </summary>
        /// <returns></returns>
        public static IPAddress GetDefaultGatewayIP()
        {
            try
            {

                var process = new Process();
                process.StartInfo.FileName = "route.exe";
                process.StartInfo.Arguments = "print";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // 匹配默认网关对应的本地IP
                Match match = Regex.Match(output, @"0.0.0.0\s+0.0.0.0\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)");
                if (match.Success)
                {
                    return IPAddress.Parse(match.Groups[^1].Value);
                }
                return null;
            }
            catch (Exception ex)
            {
                YF_Manager.logger.ErrorInfo("GetDefaultGatewayIP", ex.Message);
            }

            return null;
        }

        /// <summary>
        /// 读取本机默认IP
        /// </summary>
        /// <returns></returns>
        public static string GetLocalIP()
        {
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList.First(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .ToString();
        }
    }
}
