using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using YFrame.Installer.Models;
using YFrame.Installer.Services;

namespace YFrame.Installer.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly InstallService _installService;
    private readonly string _payloadPath;

    private int _currentStep;
    private string _installPath = string.Empty;
    private bool _createDesktopShortcut = true;
    private bool _addToStartMenu = true;
    private string _totalSizeText = "...";
    private string _diskSpaceText = "...";
    private bool _isInstalling;

    /// <summary>最大步骤索引（总步骤数减一），当前为3页流程</summary>
    private const int MaxStep = 3;

    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(IsWelcomeStep));
                OnPropertyChanged(nameof(IsConfigStep));
                OnPropertyChanged(nameof(IsProgressStep));
                OnPropertyChanged(nameof(IsFinishStep));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(NextButtonText));
                OnPropertyChanged(nameof(ShowNavigationButtons));
            }
        }
    }

    public string InstallPath
    {
        get => _installPath;
        set
        {
            if (SetProperty(ref _installPath, value))
            {
                OnPropertyChanged(nameof(CanStartInstall));
                UpdateDiskSpace(value);
            }
        }
    }

    public bool CreateDesktopShortcut { get => _createDesktopShortcut; set => SetProperty(ref _createDesktopShortcut, value); }
    public bool AddToStartMenu { get => _addToStartMenu; set => SetProperty(ref _addToStartMenu, value); }
    public bool IsInstalling { get => _isInstalling; set => SetProperty(ref _isInstalling, value); }
    public string TotalSizeText { get => _totalSizeText; set => SetProperty(ref _totalSizeText, value); }
    public string DiskSpaceText { get => _diskSpaceText; set => SetProperty(ref _diskSpaceText, value); }

    public bool IsWelcomeStep => CurrentStep == 0;
    public bool IsConfigStep => CurrentStep == 1;
    public bool IsProgressStep => CurrentStep == 2;
    public bool IsFinishStep => CurrentStep == 3;
    public bool CanGoNext => CurrentStep < MaxStep && !IsInstalling;
    public bool CanGoBack => CurrentStep > 0 && !IsInstalling;
    /// <summary>安装配置步骤（第2页）时允许点击安装</summary>
    public bool CanStartInstall => CurrentStep == 1 && !string.IsNullOrWhiteSpace(InstallPath) && !IsInstalling;
    public bool ShowNavigationButtons => CurrentStep < MaxStep;
    public string NextButtonText => CurrentStep == 1 ? "开始安装" : "下一步";

    public RelayCommand NextCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }

    public event Action<int>? StepChanged;
    public event Action? RequestClose;
    public event Action<double>? ProgressChanged;
    public event Action<string, string?>? StatusChanged;

    public MainViewModel(string payloadPath)
    {
        _installService = new InstallService();
        _payloadPath = payloadPath;
        InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "YFrame");

        NextCommand = new RelayCommand(OnNext, () => CanGoNext);
        BackCommand = new RelayCommand(OnBack, () => CanGoBack);
        CancelCommand = new RelayCommand(OnCancel);
        BrowseFolderCommand = new RelayCommand(OnBrowseFolder);

        UpdateTotalSize();
    }

    /// <summary>计算核心框架文件占用的磁盘空间</summary>
    private void UpdateTotalSize()
    {
        var corePath = Path.Combine(_payloadPath, "core");
        if (Directory.Exists(corePath))
        {
            var size = GetDirectorySizeSimple(corePath);
            TotalSizeText = $"预计占用空间: {FormatSizeSimple(size)}";
        }
    }

    /// <summary>更新目标磁盘剩余空间信息</summary>
    private void UpdateDiskSpace(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (root != null)
            {
                var driveInfo = new DriveInfo(root);
                DiskSpaceText = $"可用空间: {FormatSizeSimple(driveInfo.AvailableFreeSpace)}";
            }
        }
        catch { DiskSpaceText = "可用空间: 无法获取"; }
    }

    /// <summary>下一步/开始安装</summary>
    private void OnNext()
    {
        if (CurrentStep == 1)
            StartInstallation();
        else if (CurrentStep < MaxStep)
        {
            CurrentStep++;
            StepChanged?.Invoke(CurrentStep);
        }
    }

    /// <summary>返回上一步</summary>
    private void OnBack()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
            StepChanged?.Invoke(CurrentStep);
        }
    }

    /// <summary>取消安装</summary>
    private void OnCancel()
    {
        if (IsInstalling)
        {
            var result = MessageBox.Show("安装正在进行中，确定要取消吗？", "确认取消", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.No) return;
        }
        RequestClose?.Invoke();
    }

    /// <summary>浏览文件夹选择安装路径</summary>
    private void OnBrowseFolder()
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic folder = shell.BrowseForFolder(0, "选择 YFrame 安装目录", 0, InstallPath);
            if (folder != null)
            {
                var path = folder.Self?.Path as string;
                if (!string.IsNullOrEmpty(path))
                    InstallPath = path;
            }
        }
        catch { }
    }

    /// <summary>执行安装流程</summary>
    private async void StartInstallation()
    {
        if (string.IsNullOrWhiteSpace(InstallPath)) return;

        // 安装到 Program Files 需要管理员权限
        if (InstallPath.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase))
        {
            if (!IsRunningAsAdmin())
            {
                var result = MessageBox.Show("安装到 Program Files 需要管理员权限。\n\n是否以管理员身份重新启动安装程序？",
                    "需要管理员权限", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes) { RestartAsAdmin(); }
                return;
            }
        }

        IsInstalling = true;
        CurrentStep = 2;
        StepChanged?.Invoke(CurrentStep);

        var config = new InstallConfig
        {
            InstallPath = InstallPath,
            CreateDesktopShortcut = CreateDesktopShortcut,
            AddToStartMenu = AddToStartMenu
        };

        try
        {
            var progress = new Progress<double>(v => Application.Current.Dispatcher.Invoke(() => ProgressChanged?.Invoke(v)));
            var status = new Progress<string>(msg => Application.Current.Dispatcher.Invoke(() => StatusChanged?.Invoke(msg, null)));

            StatusChanged?.Invoke("正在准备安装...", null);
            await _installService.InstallAsync(_payloadPath, config, progress, status);

            Application.Current.Dispatcher.Invoke(() =>
            {
                IsInstalling = false;
                CurrentStep = 3;
                StepChanged?.Invoke(CurrentStep);
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"安装失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                IsInstalling = false;
                CurrentStep = 0;
                StepChanged?.Invoke(CurrentStep);
            });
        }
    }

    /// <summary>检测当前是否以管理员身份运行</summary>
    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>以管理员身份重新启动程序</summary>
    private void RestartAsAdmin()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath == null) return;
            var startInfo = new ProcessStartInfo { FileName = exePath, UseShellExecute = true, Verb = "runas" };
            Process.Start(startInfo);
            RequestClose?.Invoke();
        }
        catch { }
    }

    /// <summary>递归计算目录大小</summary>
    private static long GetDirectorySizeSimple(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try { return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length); }
        catch { return 0; }
    }

    /// <summary>格式化字节数为可读字符串</summary>
    private static string FormatSizeSimple(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}
