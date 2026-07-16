using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager
{
    public class YF_Manager_Log
    {
        // log回调，界面输出
        public static YF_DelegateFunctionModel.dvFunc_Vs d_LogWrite;

        // 初始化的log对象信息
        private string _name = "Default";
        private string _id = "Default";

        private static long maxFileSize = 1024 * 1024; // 1MB

        public YF_Manager_Log(string Name, string ID)
        {
            _name = Name;
            _id = ID;
        }

        // log锁
        private static readonly object _fileLock = new();
        public void DebugInfo(string msg)
        {
            Write(CheckPath(@$"{Config.LogPath}\DebugLog"), _name + ": " + msg);
            LogInfo(msg, "[Debug]");
        }

        public void ErrorInfo(string functionName, string msg)
        {
            Write(CheckPath(@$"{Config.LogPath}\ErrorLog"), _name + ": " + functionName + ": " + msg);
            LogInfo(functionName + ": " + msg, "[Error]");
        }

        public void CommandInfo(string msg)
        {
            Write(CheckPath(@$"{Config.LogPath}\CommandLog"), _name + ": " + msg);
            LogInfo(msg, "[Command]");
        }

        public void TcpInfo(string msg) => Write(CheckPath(@$"{Config.LogPath}\TcpLog"), _name + ": " + msg);

        public void LogInfo(string msg, string type = "[Info]")
        {
            Write(CheckPath(@$"{Config.LogPath}\InfoLog"), _name + $": {type}" + msg);
            if (d_LogWrite != null)
            {
                var mainLogger = YF_Manager_Main.logger;
                if (mainLogger == null || _name != mainLogger._name)
                    d_LogWrite(_name + ": " + msg);
            }
        }

        public void InterceptorsLog(string msg, string type)
        {
            Write(CheckPath(@$"{Config.LogPath}\InterceptorsLog"), _name + $": [{type}]" + msg);
            LogInfo(msg);
        }

        /// <summary>
        /// log路径可用检查,自动创建
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string CheckPath(string path)
        {
            string fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".htm";
            string fullPath = Path.Combine(path, fileName);

            YF_FileHelper.Instance.EnsureDirectoryForFile(fullPath);

            if (!File.Exists(fullPath))
            {
                using (File.Create(fullPath)) { } // 创建后立即释放资源‌
            }

            return fullPath;
        }

        /// <summary>
        /// 写入log信息
        /// </summary>
        /// <param name="path"></param>
        /// <param name="msg"></param>
        private void Write(string path, string msg)
        {
            lock (_fileLock)
            {
                FileInfo fileInfo = new FileInfo(path);
                // log文件大小限制
                if (fileInfo.Exists && fileInfo.Length > maxFileSize)
                {
                    CreateNewLogFile(path);
                }


                try
                {
                    using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                    using var sw = new StreamWriter(fs, Encoding.UTF8)
                    {
                        AutoFlush = true // 自动刷新缓冲区‌
                    };

                    sw.WriteLine("<HR Size=1>");
                    sw.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {msg}\r\n");
                }
                catch (Exception ex)
                {
                    // 日志系统自身出错时至少输出到调试器
                    System.Diagnostics.Debug.WriteLine(
                        $"[YF_Manager_Log] 日志操作失败: {ex.Message}");
                    System.Diagnostics.Trace.TraceError(
                        $"[YF_Manager_Log] 日志操作失败: {ex.Message}");
                }
            }

        }

        /// <summary>
        /// log递增重命名且创建信的log文件
        /// </summary>
        /// <param name="logDirectory"></param>
        private static void CreateNewLogFile(string logDirectory)
        {
            try
            {
                string strPath = logDirectory;

                for (int i = 1; i < 1000; i++)
                {
                    if (File.Exists(logDirectory.Replace(".htm", $"_{i}.htm")))
                        continue;
                    else
                    {
                        File.Move(strPath, logDirectory.Replace(".htm", $"_{i}.htm"));
                        using (File.Create(logDirectory)) { }
                        ;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                // 日志系统自身出错时至少输出到调试器
                System.Diagnostics.Debug.WriteLine(
                    $"[YF_Manager_Log] 日志操作失败: {ex.Message}");
                System.Diagnostics.Trace.TraceError(
                    $"[YF_Manager_Log] 日志操作失败: {ex.Message}");
            }

        }
    }
}
