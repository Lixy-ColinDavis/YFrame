using System.Windows;
using System.Windows.Input;
using YFrame.upload.cos.ViewModels;

namespace YFrame.upload.cos;

/// <summary>
/// 上传器窗口：只负责窗口行为和 ViewModel 注入，上传逻辑放在 MVVM 层。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>初始化窗口并绑定 ViewModel。</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new UploadViewModel();
    }

    /// <summary>拖动自定义标题栏。</summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    /// <summary>关闭窗口。</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
