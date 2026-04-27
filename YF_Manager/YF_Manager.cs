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

        public YF_Manager_Main()
        {
            logger = new YF_Manager_Log(YF_Name.ToString(), YF_ID);
        }
    }
}
