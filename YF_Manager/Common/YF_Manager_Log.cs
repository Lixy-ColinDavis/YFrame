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

        // DI 注入的文件工具（未注入时回退到静态 Instance）
        private readonly YF_FileHelper? _fileHelper;

        /// <summary>
        /// 创建日志对象（向后兼容旧版插件）
        /// </summary>
        /// <param name="Name">日志名称（通常为组件名称）</param>
        /// <param name="ID">日志标识</param>
        public YF_Manager_Log(string Name, string ID) : this(Name, ID, null) { }

        /// <summary>
        /// 创建日志对象（支持 DI 注入文件工具）
        /// </summary>
        /// <param name="Name">日志名称（通常为组件名称）</param>
        /// <param name="ID">日志标识</param>
        /// <param name="fileHelper">文件操作工具（DI 注入，为空时回退到静态实例）</param>
        public YF_Manager_Log(string Name, string ID, YF_FileHelper? fileHelper)
        {
            _name = Name;
            _id = ID;
            _fileHelper = fileHelper;
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
        /// log路径可用检查，自动创建
        /// </summary>
        /// <param name="path">日志目录路径</param>
        /// <returns>完整的日志文件路径</returns>
        private string CheckPath(string path)
        {
            string fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".htm";
            string fullPath = Path.Combine(path, fileName);

            // 优先用 DI 注入的实例，否则回退静态实例
            var fileHelper = _fileHelper ?? YF_FileHelper.Instance;
            fileHelper.EnsureDirectoryForFile(fullPath);

            if (!File.Exists(fullPath))
            {
                using (File.Create(fullPath)) { } // 创建后立即释放资源
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
                        AutoFlush = true // 自动刷新缓冲区
                    };

                    sw.WriteLine("<HR Size=1>");
                    sw.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]: {msg}\r\n");
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
