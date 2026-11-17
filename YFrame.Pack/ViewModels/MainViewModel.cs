using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using YFrame.Pack.Models;
using YFrame.Pack.Services;

namespace YFrame.Pack.ViewModels;

/// <summary>
/// Pack 主界面 ViewModel：维护两个版本包、发布地址、构建状态和日志。
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly Regex VersionRegex = new(
        @"^YF-(?<version>\d+(?:\.\d+){1,3})\.zip$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly PackConfig _config;
    private string _previousZipPath = string.Empty;
    private string _currentZipPath = string.Empty;
    private string _outputDirectory;
    private string _currentUrl;
    private string _incrementUrl;
    private string _statusText = "就绪";
    private string _logText = string.Empty;
    private bool _isBusy;
    private int _addedCount;
    private int _changedCount;
    private int _removedCount;
    private string _incrementFileName = "increment-版本号.zip";
    private string _lastOutputDirectory = string.Empty;

    /// <summary>创建 ViewModel 并载入 exe 同目录配置。</summary>
    public MainViewModel()
    {
        _config = PackConfig.Load();
        _outputDirectory = _config.OutputDirectory;
        _currentUrl = _config.CurrentUrl;
        _incrementUrl = _config.IncrementUrl;
        _lastOutputDirectory = _config.OutputDirectory;
        _incrementFileName = BuildIncrementFileName(_previousZipPath);

        BrowsePreviousCommand = new RelayCommand(BrowsePrevious);
        BrowseCurrentCommand = new RelayCommand(BrowseCurrent);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        SwapCommand = new RelayCommand(SwapVersions);
        BuildCommand = new AsyncRelayCommand(BuildAsync, CanBuild);
        OpenOutputCommand = new RelayCommand(OpenOutput);
        ClearLogCommand = new RelayCommand(() => LogText = string.Empty);

        AppendLog($"配置文件：{PackConfig.ConfigPath}");
        AppendLog($"自动扫描目录：{AppContext.BaseDirectory}");
        LoadLatestPackages();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand BrowsePreviousCommand { get; }
    public ICommand BrowseCurrentCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand SwapCommand { get; }
    public ICommand BuildCommand { get; }
    public ICommand OpenOutputCommand { get; }
    public ICommand ClearLogCommand { get; }

    /// <summary>旧版本完整包路径。</summary>
    public string PreviousZipPath
    {
        get => _previousZipPath;
        set
        {
            if (!SetProperty(ref _previousZipPath, value)) return;
            IncrementFileName = BuildIncrementFileName(value);
            OnPropertyChanged(nameof(PreviousVersionText));
            OnPropertyChanged(nameof(VersionTransition));
            RaiseCommandState();
        }
    }

    /// <summary>新版本完整包路径。</summary>
    public string CurrentZipPath
    {
        get => _currentZipPath;
        set
        {
            if (!SetProperty(ref _currentZipPath, value)) return;
            OnPropertyChanged(nameof(CurrentVersionText));
            OnPropertyChanged(nameof(VersionTransition));
            RaiseCommandState();
        }
    }

    /// <summary>增量包输出目录。</summary>
    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            if (!SetProperty(ref _outputDirectory, value)) return;
            OnPropertyChanged(nameof(IncrementFileName));
            OnPropertyChanged(nameof(ManifestFileName));
        }
    }

    /// <summary>current.zip 固定下载地址，可在界面修改。</summary>
    public string CurrentUrl
    {
        get => _currentUrl;
        set => SetProperty(ref _currentUrl, value);
    }

    /// <summary>increment.zip 固定下载地址，可在界面修改。</summary>
    public string IncrementUrl
    {
        get => _incrementUrl;
        set => SetProperty(ref _incrementUrl, value);
    }

    /// <summary>顶部状态文本。</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>构建日志。</summary>
    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    /// <summary>是否正在构建。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(IsIdle));
            RaiseCommandState();
        }
    }

    /// <summary>是否空闲，供界面控制状态显示。</summary>
    public bool IsIdle => !IsBusy;

    public int AddedCount
    {
        get => _addedCount;
        set => SetProperty(ref _addedCount, value);
    }

    public int ChangedCount
    {
        get => _changedCount;
        set => SetProperty(ref _changedCount, value);
    }

    public int RemovedCount
    {
        get => _removedCount;
        set => SetProperty(ref _removedCount, value);
    }

    /// <summary>旧版本号，从文件名解析；无效时显示 —。</summary>
    public string PreviousVersionText => ParseVersion(PreviousZipPath) ?? "—";

    /// <summary>新版本号，从文件名解析；无效时显示 —。</summary>
    public string CurrentVersionText => ParseVersion(CurrentZipPath) ?? "—";

    /// <summary>版本变化展示，例如 2.0.0 → 2.0.1。</summary>
    public string VersionTransition => $"{PreviousVersionText}  →  {CurrentVersionText}";

    /// <summary>本次增量包文件名。</summary>
    public string IncrementFileName
    {
        get => _incrementFileName;
        set => SetProperty(ref _incrementFileName, value);
    }

    /// <summary>版本清单文件名，固定为 YF_Version.json。</summary>
    public string ManifestFileName => "YF_Version.json";

    /// <summary>是否有可打开的输出目录。</summary>
    public bool HasOutput => Directory.Exists(_lastOutputDirectory);

    /// <summary>
    /// 打开旧版本 ZIP 选择框。
    /// </summary>
    private void BrowsePrevious()
    {
        string? path = PickZip("选择旧版本压缩包", PreviousZipPath);
        if (path == null) return;
        PreviousZipPath = path;
        SaveConfig();
    }

    /// <summary>
    /// 打开新版本 ZIP 选择框。
    /// </summary>
    private void BrowseCurrent()
    {
        string? path = PickZip("选择新版本压缩包", CurrentZipPath);
        if (path == null) return;
        CurrentZipPath = path;
        SaveConfig();
    }

    /// <summary>
    /// 打开输出目录选择框。
    /// </summary>
    private void BrowseOutput()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择增量包输出目录",
            InitialDirectory = Directory.Exists(OutputDirectory) ? OutputDirectory : AppContext.BaseDirectory
        };

        if (dialog.ShowDialog() == true)
        {
            OutputDirectory = dialog.FolderName;
            SaveConfig();
        }
    }

    /// <summary>
    /// 交换新旧版本包，便于快速反向检查。
    /// </summary>
    private void SwapVersions()
    {
        (PreviousZipPath, CurrentZipPath) = (CurrentZipPath, PreviousZipPath);
        SaveConfig();
    }

    /// <summary>构建前检查：两个路径存在且不是同一文件。</summary>
    private bool CanBuild()
    {
        return !IsBusy &&
               File.Exists(PreviousZipPath) &&
               File.Exists(CurrentZipPath) &&
               !string.Equals(Path.GetFullPath(PreviousZipPath), Path.GetFullPath(CurrentZipPath),
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 执行增量构建；耗时的 ZIP 比较放到后台线程，日志通过 Progress 回到界面线程。
    /// </summary>
    private async Task BuildAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        StatusText = "正在构建增量包...";
        AddedCount = 0;
        ChangedCount = 0;
        RemovedCount = 0;
        LogText = string.Empty;
        SaveConfig();

        try
        {
            // 每次构建使用独立时间戳子目录，避免覆盖上一次结果
            string outputDirectory = CreateTimestampedOutputDirectory(OutputDirectory);
            AppendLog($"本次输出目录：{outputDirectory}");

            var progress = new Progress<string>(AppendLog);
            PackResult result = await Task.Run(() => PackService.Build(
                PreviousZipPath,
                CurrentZipPath,
                outputDirectory,
                CurrentUrl,
                IncrementUrl,
                message => ((IProgress<string>)progress).Report(message)));

            AddedCount = result.AddedFiles.Count;
            ChangedCount = result.ChangedFiles.Count;
            RemovedCount = result.RemovedFiles.Count;
            _lastOutputDirectory = outputDirectory;
            IncrementFileName = Path.GetFileName(result.IncrementPath);

            AppendLog(string.Empty);
            AppendLog("构建完成。");
            AppendLog($"增量文件：{result.IncrementPath}");
            AppendLog($"版本清单：{result.ManifestPath}");

            // 只输出前 20 条文件明细，避免日志窗口被大版本包淹没
            LogFileList("新增文件", result.AddedFiles);
            LogFileList("修改文件", result.ChangedFiles);
            LogFileList("删除文件", result.RemovedFiles);

            StatusText = $"构建完成：新增 {AddedCount}，修改 {ChangedCount}，删除 {RemovedCount}";
            OnPropertyChanged(nameof(HasOutput));
        }
        catch (Exception ex)
        {
            StatusText = "构建失败";
            AppendLog($"错误：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>输出一组文件路径，最多显示前 20 条。</summary>
    private void LogFileList(string title, List<string> files)
    {
        if (files.Count == 0) return;

        AppendLog($"{title}（{files.Count}）：");
        foreach (string file in files.Take(20))
            AppendLog($"  {file}");
        if (files.Count > 20)
            AppendLog($"  ... 其余 {files.Count - 20} 个文件已写入增量包/清单");
    }

    /// <summary>打开最近一次输出目录。</summary>
    private void OpenOutput()
    {
        string directory = Directory.Exists(_lastOutputDirectory) ? _lastOutputDirectory : OutputDirectory;
        if (!Directory.Exists(directory))
        {
            AppendLog("输出目录尚不存在，请先构建一次。");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendLog($"无法打开输出目录：{ex.Message}");
        }
    }

        /// <summary>
        /// 启动时扫描 exe 所在目录，自动加载版本号最高的两个 YF-*.zip。
        /// 输入路径只在当前会话使用，不写入配置文件。
        /// </summary>
        private void LoadLatestPackages()
        {
            try
            {
                var candidates = Directory
                    .EnumerateFiles(AppContext.BaseDirectory, "YF-*.zip", SearchOption.TopDirectoryOnly)
                    .Select(path => new { Path = path, VersionText = ParseVersion(path) })
                    .Where(item => !string.IsNullOrWhiteSpace(item.VersionText))
                    .Select(item => new { item.Path, Version = Version.Parse(item.VersionText!) })
                    .OrderByDescending(item => item.Version)
                    .ThenByDescending(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .Take(2)
                    .ToList();

                if (candidates.Count == 2)
                {
                    CurrentZipPath = candidates[0].Path;
                    PreviousZipPath = candidates[1].Path;
                    AppendLog($"已自动加载：{Path.GetFileName(candidates[1].Path)} → {Path.GetFileName(candidates[0].Path)}");
                }
                else
                {
                    AppendLog($"自动加载：目录中找到 {candidates.Count} 个有效版本包，请手动选择两个 YF-*.zip。");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"自动加载失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 在用户选择的输出目录下创建带时间戳的子目录，保证每次构建结果独立。
        /// </summary>
        /// <param name="baseDirectory">界面中选择的输出根目录</param>
        /// <returns>本次构建实际使用的输出目录</returns>
        private static string CreateTimestampedOutputDirectory(string baseDirectory)
        {
            string root = string.IsNullOrWhiteSpace(baseDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "output")
                : Path.GetFullPath(baseDirectory);
            string folderName = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string output = Path.Combine(root, folderName);

            // 极端情况下同一秒内重复构建，追加序号避免覆盖
            int suffix = 1;
            while (Directory.Exists(output))
                output = Path.Combine(root, $"{folderName}-{suffix++}");

            Directory.CreateDirectory(output);
            return output;
        }
    /// <summary>保存当前输入到本地 config。</summary>
    public void SaveConfig()
    {
        _config.OutputDirectory = OutputDirectory;
        _config.CurrentUrl = CurrentUrl;
        _config.IncrementUrl = IncrementUrl;
        _config.Save();
    }

    /// <summary>追加一行日志并自动滚动到末尾。</summary>
    private void AppendLog(string message)
    {
        LogText = string.IsNullOrEmpty(LogText)
            ? message
            : LogText + Environment.NewLine + message;
    }

    /// <summary>
    /// 打开 ZIP 文件选择框，返回 null 表示取消。
    /// </summary>
    private static string? PickZip(string title, string currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "YF 版本包 (YF-*.zip)|YF-*.zip|ZIP 压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            string? directory = Path.GetDirectoryName(currentPath);
            if (Directory.Exists(directory))
                dialog.InitialDirectory = directory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>从文件名解析 YF-x.y.z.zip 的版本号。</summary>
    private static string? ParseVersion(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        Match match = VersionRegex.Match(Path.GetFileName(path));
        return match.Success ? match.Groups["version"].Value : null;
    }

    /// <summary>根据旧版本路径生成增量包文件名。</summary>
    private static string BuildIncrementFileName(string? path)
    {
        string? version = ParseVersion(path);
        return string.IsNullOrEmpty(version) ? "increment-版本号.zip" : $"increment-{version}.zip";
    }

    private void RaiseCommandState()
    {
        if (BuildCommand is AsyncRelayCommand asyncCommand)
            asyncCommand.RaiseCanExecuteChanged();
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>无参同步命令。</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>异步命令，执行期间阻止重复点击。</summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _isRunning = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
