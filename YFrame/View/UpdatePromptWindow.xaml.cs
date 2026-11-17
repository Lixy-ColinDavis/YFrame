using System.Windows;

namespace YFrame.View;

/// <summary>
/// 启动更新提示窗口：只负责收集“立即更新/不更新”的选择。
/// </summary>
public partial class UpdatePromptWindow : Window
{
    /// <summary>用户是否选择了立即更新。</summary>
    public bool UpdateAccepted { get; private set; }

    /// <summary>
    /// 创建更新提示。
    /// </summary>
    /// <param name="currentVersion">本地当前版本</param>
    /// <param name="newVersion">远端最新版本</param>
    public UpdatePromptWindow(string currentVersion, string newVersion)
    {
        InitializeComponent();
        CurrentVersionText.Text = $"当前 {currentVersion}";
        NewVersionText.Text = $"新版本 {newVersion}";
    }

    /// <summary>用户选择立即更新。</summary>
    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateAccepted = true;
        DialogResult = true;
        Close();
    }

    /// <summary>用户选择暂不更新，由 App 写入三天跳过状态。</summary>
    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateAccepted = false;
        DialogResult = false;
        Close();
    }
}
