using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// 插件信息类，用于存储插件进行加载和切换
    /// </summary>
    public class CtrlDataModel
    {
        // 插件名称
        public string Name { get; set; }

        // 插件对象-默认不保存，只存加载的那个，节省性能
        public UserControl userControl { get; set; }

        // 命令对象-默认不保存，只存加载的那个，节省性能
        public I_YF_Command CommandHandler { get; set; }

        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
    }
}
