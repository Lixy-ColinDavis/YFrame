using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager
{
    /// <summary>
    /// 详细信息接口
    /// </summary>
    public interface IDetail
    {
        // ID
        string YF_ID { get; }
        
        // 名称
        string YF_Name { get; }
    }
}
