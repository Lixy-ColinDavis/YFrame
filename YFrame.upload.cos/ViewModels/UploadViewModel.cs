using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using YFrame.upload.cos.Models;
using YFrame.upload.cos.Services;

namespace YFrame.upload.cos.ViewModels;

/// <summary>
/// 上传器主 ViewModel：管理本地配置、密钥读取、Pack 文件扫描和上传命令。
/// </summary>
public sealed class UploadViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly CosCredentialService _credentialService;
    private readonly PackFileService _packFileService;
    private readonly CosUploadService _uploadService;
    private readonly IDialogService _dialogService;
    private UploadSettings _settings;
    private CosCredentials? _credentials;

    private string _packOutputRoot = string.Empty;
    private string _secretKeyFile = string.Empty;
    private string _bucket = string.Empty;
    private string _region = string.Empty;
    private string _environment = "uat";
    private string _selectedPackDirectory = string.Empty;
    private string _secretIdHint = "未读取";
    private string _status = "就绪";
    private string _logText = string.Empty;
    private double _progressPercent;
    private bool _isUploading;

    /// <summary>创建上传器 ViewModel。</summary>
    public UploadViewModel()
    {
        _settingsService = new SettingsService();
        _credentialService = new CosCredentialService();
        _packFileService = new PackFileService();
        _uploadService = new CosUploadService();
        _dialogService = new DialogService();

        _settings = _settingsService.Load();
        _packOutputRoot = _settings.PackOutputRoot;
        _secretKeyFile = _settings.SecretKeyFile;
        _bucket = _settings.Bucket;
        _region = _settings.Region;
        _environment = string.Equals(_settings.Environment, "pdt", StringComparison.OrdinalIgnoreCase)
            ? "pdt"
            : "uat";

        Environments = new ObservableCollection<string> { "uat", "pdt" };
        Files = new ObservableCollection<PackFileItem>();

        BrowsePackCommand = new RelayCommand(BrowsePack);
        BrowseSecretCommand = new RelayCommand(BrowseSecretFile);
        RefreshCommand = new RelayCommand(RefreshAll);
        ReloadCredentialCommand = new RelayCommand(ReloadCredentials);
        UploadCommand = new AsyncRelayCommand(UploadAsync, CanUpload);

        // 首次启动即落盘默认路径，后续用户修改也会立即保存
        SaveSettings();
        ReloadAll();
    }

    /// <summary>环境列表。</summary>
    public ObservableCollection<string> Environments { get; }

    /// <summary>四个待上传文件。</summary>
    public ObservableCollection<PackFileItem> Files { get; }

    /// <summary>Pack 输出根目录。</summary>
    public string PackOutputRoot
    {
        get => _packOutputRoot;
        set
        {
            if (SetProperty(ref _packOutputRoot, value))
                SaveSettings();
        }
    }

    /// <summary>SecretKey.csv 路径。</summary>
    public string SecretKeyFile
    {
        get => _secretKeyFile;
        set
        {
            if (SetProperty(ref _secretKeyFile, value))
                SaveSettings();
        }
    }

    /// <summary>COS Bucket。</summary>
    public string Bucket
    {
        get => _bucket;
        set
        {
            if (SetProperty(ref _bucket, value))
            {
                SaveSettings();
                UploadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>COS Region。</summary>
    public string Region
    {
        get => _region;
        set
        {
            if (SetProperty(ref _region, value))
            {
                SaveSettings();
                UploadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>UAT/PDT 环境。</summary>
    public string Environment
    {
        get => _environment;
        set
        {
            if (!SetProperty(ref _environment, value))
                return;

            SaveSettings();
            OnPropertyChanged(nameof(ObjectPrefix));
            RefreshPackFiles();
        }
    }

    /// <summary>当前环境的 COS 对象前缀。</summary>
    public string ObjectPrefix => $"YFrame/update/{Environment}/";

    /// <summary>当前选中的时间戳目录。</summary>
    public string SelectedPackDirectory
    {
        get => _selectedPackDirectory;
        private set => SetProperty(ref _selectedPackDirectory, value);
    }

    /// <summary>SecretId 脱敏提示，不显示完整密钥。</summary>
    public string SecretIdHint
    {
        get => _secretIdHint;
        private set => SetProperty(ref _secretIdHint, value);
    }

    /// <summary>上传状态。</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>日志文本。</summary>
    public string LogText
    {
        get => _logText;
        private set => SetProperty(ref _logText, value);
    }

    /// <summary>整体上传进度。</summary>
    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    /// <summary>是否正在上传。</summary>
    public bool IsUploading
    {
        get => _isUploading;
        private set
        {
            if (SetProperty(ref _isUploading, value))
                UploadCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>文件数量文本。</summary>
    public string FileCountText => $"{Files.Count}/4";

    public RelayCommand BrowsePackCommand { get; }
    public RelayCommand BrowseSecretCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand ReloadCredentialCommand { get; }
    public AsyncRelayCommand UploadCommand { get; }

    /// <summary>初始化加载配置、密钥和 Pack 文件。</summary>
    private void ReloadAll()
    {
        ReloadCredentials(silent: true);
        RefreshPackFiles();
    }

    /// <summary>刷新密钥和文件列表。</summary>
    private void RefreshAll()
    {
        ReloadCredentials();
        RefreshPackFiles();
    }

    /// <summary>选择 Pack 输出根目录。</summary>
    private void BrowsePack()
    {
        string? folder = _dialogService.PickFolder("选择 Pack output 目录", PackOutputRoot);
        if (folder == null)
            return;

        PackOutputRoot = folder;
        RefreshPackFiles();
    }

    /// <summary>选择密钥 CSV。</summary>
    private void BrowseSecretFile()
    {
        string? file = _dialogService.PickFile(
            "选择 SecretKey.csv",
            "CSV 文件|*.csv|所有文件|*.*",
            Path.GetDirectoryName(SecretKeyFile));
        if (file == null)
            return;

        SecretKeyFile = file;
        ReloadCredentials();
    }

    /// <summary>重新读取 CSV 凭证。</summary>
    private void ReloadCredentials() => ReloadCredentials(silent: false);

    /// <summary>读取 CSV 凭证。</summary>
    /// <param name="silent">是否静默加载，初始化时使用</param>
    private void ReloadCredentials(bool silent)
    {
        try
        {
            _credentials = _credentialService.Load(SecretKeyFile);
            SecretIdHint = $"已读取 · {MaskSecretId(_credentials.SecretId)}";
            if (!silent)
                AppendLog("密钥读取成功。");
        }
        catch (Exception ex)
        {
            _credentials = null;
            SecretIdHint = $"读取失败：{ex.Message}";
            if (!silent)
                AppendLog($"密钥读取失败：{ex.Message}");
        }

        UploadCommand.RaiseCanExecuteChanged();
    }

    /// <summary>扫描最新完整 Pack 目录。</summary>
    private void RefreshPackFiles()
    {
        PackDirectoryResult result = _packFileService.FindLatest(PackOutputRoot, Environment);
        Files.Clear();

        if (!result.Success)
        {
            SelectedPackDirectory = string.Empty;
            Status = result.Error ?? "未找到发布文件";
            AppendLog(Status);
            OnPropertyChanged(nameof(FileCountText));
            UploadCommand.RaiseCanExecuteChanged();
            return;
        }

        SelectedPackDirectory = result.DirectoryPath!;
        foreach (PackFileItem file in result.Files)
            Files.Add(file);

        Status = $"已找到最新 Pack：{Path.GetFileName(SelectedPackDirectory)}";
        AppendLog($"Pack 目录：{SelectedPackDirectory}");
        AppendLog($"文件数量：{Files.Count}，环境：{Environment}");
        OnPropertyChanged(nameof(FileCountText));
        UploadCommand.RaiseCanExecuteChanged();
    }

    /// <summary>判断是否可以上传。</summary>
    private bool CanUpload()
        => !IsUploading && Files.Count == 4 && _credentials != null;

    /// <summary>按固定顺序上传四个文件。</summary>
    private async Task UploadAsync()
    {
        try
        {
            ReloadCredentials(silent: true);
            if (_credentials == null)
            {
                ShowError("未读取到有效密钥，请检查 SecretKey.csv。");
                return;
            }

            RefreshPackFiles();
            if (Files.Count != 4)
            {
                ShowError("未找到完整的四个发布文件。");
                return;
            }

            if (string.IsNullOrWhiteSpace(Bucket) || string.IsNullOrWhiteSpace(Region))
            {
                ShowError("Bucket 和 Region 不能为空。");
                return;
            }

            SaveSettings();
            IsUploading = true;
            ProgressPercent = 0;
            Status = "准备上传...";
            foreach (PackFileItem file in Files)
                file.Status = "待上传";

            AppendLog($"开始上传，环境：{Environment}");
            var progress = new Progress<UploadProgressInfo>(info =>
            {
                ProgressPercent = info.OverallPercent;
                Status = info.Message;
            });

            await _uploadService.UploadAsync(
                _credentials, Bucket.Trim(), Region.Trim(), Files, progress);

            ProgressPercent = 100;
            Status = "四个文件全部上传成功";
            AppendLog("全部上传成功。");
        }
        catch (Exception ex)
        {
            Status = "上传失败";
            AppendLog($"上传失败：{ex.Message}");
            ShowError(ex.Message);
        }
        finally
        {
            IsUploading = false;
            UploadCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>保存当前设置到本地 JSON。</summary>
    private void SaveSettings()
    {
        _settings = new UploadSettings
        {
            PackOutputRoot = PackOutputRoot,
            SecretKeyFile = SecretKeyFile,
            Bucket = Bucket,
            Region = Region,
            Environment = Environment
        };

        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            AppendLog($"保存配置失败：{ex.Message}");
        }
    }

    /// <summary>脱敏 SecretId。</summary>
    /// <param name="secretId">完整 SecretId</param>
    /// <returns>只显示末四位的文本</returns>
    private static string MaskSecretId(string secretId)
        => secretId.Length <= 4 ? "****" : $"****{secretId[^4..]}";

    /// <summary>追加日志。</summary>
    /// <param name="message">日志内容</param>
    private void AppendLog(string message)
        => LogText += $"[{DateTime.Now:HH:mm:ss}] {message}{System.Environment.NewLine}";

    /// <summary>显示错误提示。</summary>
    /// <param name="message">错误内容</param>
    private void ShowError(string message)
        => _dialogService.ShowError("COS 上传", message);
}
