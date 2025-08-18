using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager
{
    public class YF_Manager : IDetail
    {
        public string YF_ID => "YF_Manager";

        public string YF_Name => "YF工具类";

        public static YF_Manager_Log logger
            ;

        YF_Manager()
        {
            logger = new YF_Manager_Log(YF_Name.ToString(), YF_ID);
        }
    }
}
