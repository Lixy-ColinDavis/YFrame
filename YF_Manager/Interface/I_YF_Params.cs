using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager
{
    /// <summary>
    /// 参数接口
    /// </summary>
    public interface I_YF_Params
    {
        // 插件参数列表 - 可选择性继承
        Dictionary<string, object> Parameters { get; }
    }
}
