using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager
{
    public interface I_YF_Command
    {
        [Log(Level = LogLevel.Info, Message = "发送命令到插件")]
        void ExecuteCommand(string command, object? parameter = null);

        // 插件事件回调；没有订阅者时为空
        event EventHandler<PluginEventArgs>? OnPluginCallback;
    }

    public class PluginEventArgs : EventArgs
    {
        public string PluginId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
