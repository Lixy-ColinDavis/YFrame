using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YFrame.Update.Models;
using YFrame.Update.Services;

namespace YFrame.Update;

/// <summary>
/// 更新窗口：启动后自动执行更新，不提供操作按钮，只显示进度和结果。
/// </summary>
public partial class MainWindow : Window
{
    private bool _started;
    private readonly UpdateLogger _logger = new(AppContext.BaseDirectory);

    /// <summary>初始化窗口并绑定自动启动事件。</summary>
    public MainWindow()
    {
        InitializeComponent();
        Loaded += Window_Loaded;
        _logger.Info($"更新窗口已打开；日志={_logger.FilePath}");
    }

    /// <summary>窗口显示后自动开始更新。</summary>
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started) return;
        _started = true;

        try
        {
            var runner = new UpdateRunner(logger: _logger);
            var progress = new Progress<UpdateProgress>(item =>
            {
                UpdateProgressBar.Value = item.Percent;
                PercentText.Text = $"{item.Percent:0}%";
                StatusText.Text = item.Status;
                DetailText.Text = item.Detail;
            });

            UpdateResult result = await Task.Run(() => runner.RunAsync(progress));
            if (result.AlreadyCurrent)
            {
                _logger.Info("本地已是最新版本，准备重启 YFrame。");
                StatusText.Text = "已是最新版本";
                DetailText.Text = "无需更新，正在启动 YFrame...";
            }
            else
            {
                _logger.Info($"更新完成；目标版本={result.CurrentVersion}；备份已清理={string.IsNullOrEmpty(result.BackupDirectory)}");
                StatusText.Text = "更新完成";
                DetailText.Text = $"已更新到 {result.CurrentVersion}，备份目录已清理，正在重启 YFrame...";
                VersionText.Text = string.IsNullOrEmpty(result.BackupDirectory)
                    ? $"目标版本：{result.CurrentVersion}"
                    : $"目标版本：{result.CurrentVersion}    备份目录：{result.BackupDirectory}";
            }

            // 给界面一点时间显示完成状态，再启动主程序
            await Task.Delay(700);
            runner.StartMainApp();
            _logger.Info("YFrame 已启动，更新器即将退出。");
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.Error("更新失败", ex);
            StatusText.Text = "更新失败";
            DetailText.Text = $"{ex.Message}{Environment.NewLine}日志：{_logger.FilePath}";
            UpdateProgressBar.Value = 0;
            PercentText.Text = "--";
            CloseButton.IsEnabled = true;
        }
    }

    /// <summary>拖动标题栏移动窗口；关闭按钮区域不触发拖动。</summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        DragMove();
    }

    /// <summary>判断事件源是否位于按钮内部。</summary>
    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.Button) return true;
            source = source is Visual
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    /// <summary>失败后允许用户关闭更新窗口。</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
