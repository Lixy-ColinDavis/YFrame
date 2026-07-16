using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager
{
    public class Config
    {
        // PaddlOCR 模型根路径
        public const string Paddlepath = @"plugins\YF_ScreenOCRTranslate\inference";

        // 日志路径
        public const string LogPath = @"Log";

        // 脚本保存路径
        public const string ScriptPath = @"Config\Script";

        // 插件路径
        public const string PluginPath = @"Plugins";

        // 服务端端口
        public const string TcpHelper_Port_Server = "8021";

        // 客户端端口
        public const string TcpHelper_Port_Client = "8022"; 
    }
}
