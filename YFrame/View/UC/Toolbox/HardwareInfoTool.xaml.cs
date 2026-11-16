using System.Windows.Controls;

namespace YFrame.View.UC.Toolbox
{
    /// <summary>
    /// 硬件检查工具视图：逻辑全部在 HardwareInfoToolViewModel，
    /// 此处仅负责加载 XAML。
    /// </summary>
    public partial class HardwareInfoTool : UserControl
    {
        public HardwareInfoTool()
        {
            InitializeComponent();
        }
    }
}
