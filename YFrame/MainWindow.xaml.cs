using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YF_Manager;

namespace YFrame
{
    public partial class MainWindow : Window, IDetail
    {
        public string YF_ID => "YF_Frame";

        public string YF_Name => "主框架";
        // 日志对象
        public static YF_Manager_Log logger;

        public MainWindow()
        {
            logger = new YF_Manager_Log(YF_Name, YF_ID);
            DataContext = new MainWindowViewModel();

            InitializeComponent();
            
        }
    }
}