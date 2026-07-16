using System.Windows;
using YF_Manager;

namespace YFrame
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = MainWindowViewModel.Instance;
            MainWindowViewModel.Instance.Init();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 初始化全局热键服务，传入主窗口以注册 WndProc 消息钩子
            HotkeyService.Instance.Initialize(this);
        }
    }
}
