using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager
{
    public class YF_Manager_Main : I_YF_Detail
    {
        public string YF_ID => "YF_Manager";

        public string YF_Name => "主控类";

        public static YF_Manager_Log logger;

        /// <summary>
        /// 静态构造函数确保 logger 在类首次被引用时就初始化，
        /// 避免 AOP 拦截器在实例构造前访问 null 的 logger
        /// </summary>
        static YF_Manager_Main()
        {
            logger = new YF_Manager_Log("主控类", "YF_Manager");
        }
    }
}
