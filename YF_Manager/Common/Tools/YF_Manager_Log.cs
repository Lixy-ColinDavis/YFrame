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
        public static DelegateFunctionModel.dvFunc_s d_LogWrite;

        private string _name = "Default";
        private string _id = "Default";

        public YF_Manager_Log(string Name, string ID)
        {
            _name = Name;
            _id = ID;
        }

        private static readonly object _fileLock = new();
        public void DebugInfo(string msg) => Write(CheckPath(@$"{Config.LogPath}\DebugLog"), _name + ": " + msg);

        public void ErrorInfo(string functionName, string msg)
        {
            Write(CheckPath(@$"{Config.LogPath}\ErrorLog"), _name + ": " + functionName + ": " + msg);
            LogInfo(functionName + ": " + msg, "[Error]");
        }

        public void TcpInfo(string msg) => Write(CheckPath(@$"{Config.LogPath}\TcpLog"), _name + ": " + msg);

        public void LogInfo(string msg, string type = "[Info]")
        {
            Write(CheckPath(@$"{Config.LogPath}\InfoLog"), _name + $": {type}" + msg);
            if (d_LogWrite != null)
                d_LogWrite(_name + ": " + msg);
        }

        private static string CheckPath(string path)
        {
            string fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".htm";
            string fullPath = Path.Combine(path, fileName);

            var directoryPath = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath); // 自动创建多级目录‌
            }

            if (!File.Exists(fullPath))
            {
                using (File.Create(fullPath)) { } // 创建后立即释放资源‌
            }

            return fullPath;
        }

        private void Write(string path, string msg)
        {
            lock (_fileLock)
            {
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
                }
            }

        }
    }
}
