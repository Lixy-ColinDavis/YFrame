using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YFrame.Pack.ViewModels;

namespace YFrame.Pack;

/// <summary>
/// Pack 主窗口：负责窗口拖动、标题栏按钮和日志自动滚动。
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>初始化窗口并绑定 ViewModel。</summary>
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Closing += (_, _) => _viewModel.SaveConfig();
    }

    /// <summary>
    /// 拖动整个标题栏移动窗口；点击最小化/关闭按钮时不触发拖动。
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || WindowState == WindowState.Maximized)
            return;

        // 标题栏内的按钮需要保留点击行为，不能进入 DragMove
        if (IsInsideButton(e.OriginalSource as DependencyObject))
            return;

        DragMove();
    }

    /// <summary>判断鼠标事件源是否位于按钮内部。</summary>
    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button) return true;
            source = source is Visual
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    /// <summary>最小化窗口。</summary>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    /// <summary>关闭窗口。</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    /// <summary>日志追加后自动滚动到末尾。</summary>
    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.ScrollToEnd();
    }
}
