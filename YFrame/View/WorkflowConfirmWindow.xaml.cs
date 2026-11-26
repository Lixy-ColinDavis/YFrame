using System.Windows;
using System.Windows.Input;

namespace YFrame.View;

/// <summary>
/// 与主框架主题一致的工作流删除确认窗口。
/// </summary>
public partial class WorkflowConfirmWindow : Window
{
    /// <summary>用户是否确认删除。</summary>
    public bool Confirmed { get; private set; }

    /// <summary>创建确认窗口。</summary>
    /// <param name="message">提示文本</param>
    public WorkflowConfirmWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    /// <summary>确认删除。</summary>
    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    /// <summary>取消删除。</summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
        Close();
    }

    /// <summary>拖动窗口。</summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
}
