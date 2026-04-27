using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager
{ 
    /// <summary>
    /// 日志特性 - 自动记录方法调用日志
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class LogAttribute : Attribute
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }

        public LogAttribute(LogLevel level = LogLevel.Info)
        {
            Level = level;
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
