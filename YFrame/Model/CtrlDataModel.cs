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

        // 插件ID，便于 O(1) 直接获取，避免遍历字典反查
        public string PluginId { get; set; } = string.Empty;


        // 本框架不长期持有全部插件实例，仅保留当前显示的那个（切换即重建，旧实例被 GC 回收）
        public object? LastSubscribedHandler { get; set; } = null;

        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
    }
}
