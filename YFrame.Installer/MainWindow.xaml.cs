using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using YFrame.Installer.Services;
using YFrame.Installer.ViewModels;
using YFrame.Installer.Views;

namespace YFrame.Installer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private WelcomePage? _welcomePage;
    private InstallConfigPage? _configPage;
    private ProgressPage? _progressPage;
    private FinishPage? _finishPage;

    public MainWindow()
    {
        InitializeComponent();

        // 自动查找 payload：Debug 模式从文件系统，Release 单文件模式从嵌入资源解压
        var payloadPath = PayloadExtractor.GetPayloadPath();
        _viewModel = new MainViewModel(payloadPath);

        _viewModel.StepChanged += OnStepChanged;
        _viewModel.ProgressChanged += v => Dispatcher.Invoke(() => _progressPage?.UpdateProgress(v));
        _viewModel.StatusChanged += (s, d) => Dispatcher.Invoke(() =>
        {
            _progressPage?.UpdateStatus(s, d);
            _progressPage?.AppendLog(s);
        });
        _viewModel.RequestClose += () => Close();

        DataContext = _viewModel;
        ShowPage(0);

        Closed += (_, _) => PayloadExtractor.Cleanup();
    }

    private void ShowPage(int step)
    {
        UserControl? page = step switch
        {
            0 => _welcomePage ??= new WelcomePage(),
            1 => _configPage ??= CreateConfigPage(),
            2 => _progressPage ??= new ProgressPage(),
            3 => _finishPage ??= new FinishPage(),
            _ => null
        };

        if (page != null) PageHost.Content = page;
        UpdateStepIndicators(step);
    }

    private InstallConfigPage CreateConfigPage()
    {
        var page = new InstallConfigPage();
        page.BrowseButton.Click += (_, _) => _viewModel.BrowseFolderCommand.Execute(null);
        page.DataContext = _viewModel;
        return page;
    }

    private void OnStepChanged(int step)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateStepIndicators(step);
            if (PageHost.Content is FrameworkElement oldContent)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (_, _) =>
                {
                    ShowPage(step);
                    if (PageHost.Content is FrameworkElement newContent)
                    {
                        newContent.Opacity = 0;
                        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                        newContent.BeginAnimation(OpacityProperty, fadeIn);
                    }
                };
                oldContent.BeginAnimation(OpacityProperty, fadeOut);
            }
            else ShowPage(step);
        });
    }

    private void UpdateStepIndicators(int step)
    {
        Step0.IsChecked = step >= 0;
        Step1.IsChecked = step >= 1;
        Step2.IsChecked = step >= 2;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) Close();
        else DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}