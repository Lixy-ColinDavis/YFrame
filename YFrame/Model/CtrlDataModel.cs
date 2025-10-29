using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YFrame
{
    /// <summary>
    /// 插件信息类，用于存储插件进行加载和切换
    /// </summary>
    public class CtrlDataModel
    {
        // 插件名称
        public string Name { get; set; }

        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
    }
}
