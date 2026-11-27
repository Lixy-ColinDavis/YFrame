using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using YF_Manager;
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
    private readonly PluginPackService _pluginPackService;
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

    private string _pluginSourceRoot = string.Empty;
    private string _pluginZipOutputRoot = string.Empty;
    private string _pluginRemotePrefix = string.Empty;
    private string _pluginBaseUrl = string.Empty;
    private string _pluginManifestRemotePrefix = "YFrame/plugins";
    private string _pluginManifestBaseUrl =
        "https://yfcos-1256976024.cos.ap-beijing.myqcloud.com/YFrame/plugins";
    private string _pluginManifestFileName = "plugins.json";
    private string _pluginPrivateKeyFile = string.Empty;
    private string _pluginKeyId = "plugin-2026-01";
    private string _pluginStatus = "等待处理插件包";
    private double _pluginProgressPercent;
    private bool _isPluginBusy;
    private string _pluginManifestPath = string.Empty;

    /// <summary>创建上传器 ViewModel。</summary>
    public UploadViewModel()
    {
        _settingsService = new SettingsService();
        _credentialService = new CosCredentialService();
        _packFileService = new PackFileService();
        _uploadService = new CosUploadService();
        _pluginPackService = new PluginPackService();
        _dialogService = new DialogService();

        _settings = _settingsService.Load();
        _packOutputRoot = _settings.PackOutputRoot;
        _secretKeyFile = _settings.SecretKeyFile;
        _bucket = _settings.Bucket;
        _region = _settings.Region;
        _environment = string.Equals(_settings.Environment, "pdt", StringComparison.OrdinalIgnoreCase)
            ? "pdt"
            : "uat";
        _pluginSourceRoot = _settings.PluginSourceRoot;
        _pluginZipOutputRoot = _settings.PluginZipOutputRoot;
        _pluginRemotePrefix = _settings.PluginRemotePrefix;
        _pluginBaseUrl = _settings.PluginBaseUrl;
        _pluginManifestRemotePrefix = _settings.PluginManifestRemotePrefix;
        _pluginManifestBaseUrl = _settings.PluginManifestBaseUrl;
        _pluginManifestFileName = _settings.PluginManifestFileName;
        _pluginPrivateKeyFile = _settings.PluginPrivateKeyFile;
        _pluginKeyId = _settings.PluginKeyId;

        Environments = new ObservableCollection<string> { "uat", "pdt" };
        Files = new ObservableCollection<PackFileItem>();
        PluginPackages = new ObservableCollection<PluginPackageItem>();

        BrowsePackCommand = new RelayCommand(BrowsePack);
        BrowseSecretCommand = new RelayCommand(BrowseSecretFile);
        RefreshCommand = new RelayCommand(RefreshAll);
        ReloadCredentialCommand = new RelayCommand(ReloadCredentials);
        UploadCommand = new AsyncRelayCommand(UploadAsync, CanUpload);

        BrowsePluginSourceCommand = new RelayCommand(BrowsePluginSource);
        BrowsePluginOutputCommand = new RelayCommand(BrowsePluginOutput);
        BrowsePluginKeyCommand = new RelayCommand(BrowsePluginPrivateKey);
        RefreshPluginListCommand = new RelayCommand(RefreshPluginList);
        CompressPluginsCommand = new AsyncRelayCommand(CompressPluginsAsync, CanCompressPlugins);
        UploadPluginsCommand = new AsyncRelayCommand(UploadPluginsAsync, CanUploadPlugins);

        // 先扫描插件并恢复勾选状态，再落盘默认配置，避免覆盖已有勾选
        ReloadAll();
        SaveSettings();
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

    /// <summary>插件包列表。</summary>
    public ObservableCollection<PluginPackageItem> PluginPackages { get; }

    /// <summary>本地插件目录。</summary>
    public string PluginSourceRoot
    {
        get => _pluginSourceRoot;
        set
        {
            if (SetProperty(ref _pluginSourceRoot, value))
                SaveSettings();
        }
    }

    /// <summary>插件 ZIP 和清单本地输出目录。</summary>
    public string PluginZipOutputRoot
    {
        get => _pluginZipOutputRoot;
        set
        {
            if (SetProperty(ref _pluginZipOutputRoot, value))
                SaveSettings();
        }
    }

    /// <summary>COS 插件对象前缀。</summary>
    public string PluginRemotePrefix
    {
        get => _pluginRemotePrefix;
        set
        {
            if (SetProperty(ref _pluginRemotePrefix, value))
                SaveSettings();
        }
    }

    /// <summary>插件公开访问根地址。</summary>
    public string PluginBaseUrl
    {
        get => _pluginBaseUrl;
        set
        {
            if (SetProperty(ref _pluginBaseUrl, value))
                SaveSettings();
        }
    }

    /// <summary>plugins.json 的独立 COS 对象前缀。</summary>
    public string PluginManifestRemotePrefix
    {
        get => _pluginManifestRemotePrefix;
        set
        {
            if (SetProperty(ref _pluginManifestRemotePrefix, value))
            {
                SaveSettings();
                OnPropertyChanged(nameof(PluginManifestUploadUrl));
            }
        }
    }

    /// <summary>plugins.json 的公开访问根地址。</summary>
    public string PluginManifestBaseUrl
    {
        get => _pluginManifestBaseUrl;
        set
        {
            if (SetProperty(ref _pluginManifestBaseUrl, value))
            {
                SaveSettings();
                OnPropertyChanged(nameof(PluginManifestUploadUrl));
            }
        }
    }

    /// <summary>插件清单文件名。</summary>
    public string PluginManifestFileName
    {
        get => _pluginManifestFileName;
        set
        {
            if (SetProperty(ref _pluginManifestFileName, value))
            {
                SaveSettings();
                OnPropertyChanged(nameof(PluginManifestUploadUrl));
            }
        }
    }

    /// <summary>插件处理状态。</summary>
    public string PluginStatus
    {
        get => _pluginStatus;
        private set => SetProperty(ref _pluginStatus, value);
    }

    /// <summary>插件清单签名私钥 PEM 路径。</summary>
    public string PluginPrivateKeyFile
    {
        get => _pluginPrivateKeyFile;
        set
        {
            if (SetProperty(ref _pluginPrivateKeyFile, value))
                SaveSettings();
        }
    }

    /// <summary>插件清单签名 KeyId。</summary>
    public string PluginKeyId
    {
        get => _pluginKeyId;
        set
        {
            if (SetProperty(ref _pluginKeyId, value))
                SaveSettings();
        }
    }

    /// <summary>插件处理进度。</summary>
    public double PluginProgressPercent
    {
        get => _pluginProgressPercent;
        private set => SetProperty(ref _pluginProgressPercent, value);
    }

    /// <summary>是否正在处理插件包。</summary>
    public bool IsPluginBusy
    {
        get => _isPluginBusy;
        private set
        {
            if (SetProperty(ref _isPluginBusy, value))
            {
                CompressPluginsCommand.RaiseCanExecuteChanged();
                UploadPluginsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>插件清单本地路径。</summary>
    public string PluginManifestPath
    {
        get => _pluginManifestPath;
        private set => SetProperty(ref _pluginManifestPath, value);
    }

    /// <summary>插件清单最终公开地址。</summary>
    public string PluginManifestUploadUrl
        => $"{PluginManifestBaseUrl.TrimEnd('/')}/{PluginManifestFileName}";

    /// <summary>插件数量文本。</summary>
    public string PluginCountText
    {
        get
        {
            int selected = PluginPackages.Count(item => item.IsSelected);
            return $"{selected}/{PluginPackages.Count} 个插件";
        }
    }

    public RelayCommand BrowsePluginSourceCommand { get; }
    public RelayCommand BrowsePluginOutputCommand { get; }
    public RelayCommand BrowsePluginKeyCommand { get; }
    public RelayCommand RefreshPluginListCommand { get; }
    public AsyncRelayCommand CompressPluginsCommand { get; }
    public AsyncRelayCommand UploadPluginsCommand { get; }

    /// <summary>初始化加载配置、密钥和 Pack 文件。</summary>
    private void ReloadAll()
    {
        ReloadCredentials(silent: true);
        RefreshPackFiles();
        RefreshPluginList();
    }

    /// <summary>刷新密钥和文件列表。</summary>
    private void RefreshAll()
    {
        ReloadCredentials();
        RefreshPackFiles();
        RefreshPluginList();
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

    /// <summary>选择插件源目录。</summary>
    private void BrowsePluginSource()
    {
        string? folder = _dialogService.PickFolder("选择本地 plugins 目录", PluginSourceRoot);
        if (folder == null)
            return;

        PluginSourceRoot = folder;
        RefreshPluginList();
    }

    /// <summary>选择插件 ZIP 输出目录。</summary>
    private void BrowsePluginOutput()
    {
        string? folder = _dialogService.PickFolder("选择插件 ZIP 输出目录", PluginZipOutputRoot);
        if (folder == null)
            return;

        PluginZipOutputRoot = folder;
        RefreshPluginList();
    }

    /// <summary>选择插件清单签名私钥。</summary>
    private void BrowsePluginPrivateKey()
    {
        string? file = _dialogService.PickFile(
            "选择插件清单签名私钥",
            "PEM 私钥|*.pem|所有文件|*.*",
            Path.GetDirectoryName(PluginPrivateKeyFile));
        if (file != null)
            PluginPrivateKeyFile = file;
    }

    /// <summary>扫描插件目录并恢复勾选状态。</summary>
    private void RefreshPluginList()
    {
        try
        {
            foreach (PluginPackageItem oldItem in PluginPackages)
                oldItem.PropertyChanged -= PluginPackage_PropertyChanged;

            IReadOnlyList<PluginPackageItem> packages =
                _pluginPackService.Scan(PluginSourceRoot, PluginZipOutputRoot, PluginBaseUrl);
            PluginPackages.Clear();

            var selectedNames = _settings.SelectedPlugins;
            foreach (PluginPackageItem package in packages)
            {
                // 旧配置没有 SelectedPlugins 时默认全选；空集合表示用户全部取消。
                package.IsSelected = selectedNames == null ||
                                     selectedNames.Contains(package.FolderName, StringComparer.OrdinalIgnoreCase);
                package.PropertyChanged += PluginPackage_PropertyChanged;
                PluginPackages.Add(package);
            }

            PluginManifestPath = Path.Combine(PluginZipOutputRoot, PluginManifestFileName);
            PluginStatus = $"已扫描到 {PluginPackages.Count} 个插件，已勾选 {PluginPackages.Count(item => item.IsSelected)} 个";
            AppendLog($"插件目录：{PluginSourceRoot}");
            AppendLog($"插件数量：{PluginPackages.Count}");
        }
        catch (Exception ex)
        {
            foreach (PluginPackageItem oldItem in PluginPackages)
                oldItem.PropertyChanged -= PluginPackage_PropertyChanged;
            PluginPackages.Clear();
            PluginStatus = $"扫描失败：{ex.Message}";
            AppendLog(PluginStatus);
        }

        OnPropertyChanged(nameof(PluginCountText));
        CompressPluginsCommand.RaiseCanExecuteChanged();
        UploadPluginsCommand.RaiseCanExecuteChanged();
    }

    /// <summary>插件勾选状态变化时保存配置并刷新命令状态。</summary>
    private void PluginPackage_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PluginPackageItem.IsSelected))
            return;

        SaveSettings();
        OnPropertyChanged(nameof(PluginCountText));
        CompressPluginsCommand.RaiseCanExecuteChanged();
        UploadPluginsCommand.RaiseCanExecuteChanged();
    }

    /// <summary>判断是否可以执行压缩。</summary>
    private bool CanCompressPlugins()
        => !IsPluginBusy && PluginPackages.Any(item => item.IsSelected);

    /// <summary>判断是否可以上传已压缩插件包。</summary>
    private bool CanUploadPlugins()
    {
        string manifestPath = Path.Combine(PluginZipOutputRoot, PluginManifestFileName);
        return !IsPluginBusy && _credentials != null &&
               File.Exists(manifestPath) && File.Exists(manifestPath + ".sig");
    }

    /// <summary>只压缩已勾选插件并生成清单，不上传。</summary>
    private async Task CompressPluginsAsync()
    {
        try
        {
            if (!Directory.Exists(PluginSourceRoot))
            {
                ShowError("插件源目录不存在。");
                return;
            }

            if (string.IsNullOrWhiteSpace(PluginZipOutputRoot) ||
                string.IsNullOrWhiteSpace(PluginBaseUrl) ||
                string.IsNullOrWhiteSpace(PluginManifestFileName) ||
                string.IsNullOrWhiteSpace(PluginPrivateKeyFile) ||
                string.IsNullOrWhiteSpace(PluginKeyId))
            {
                ShowError("插件输出目录、公开地址、清单文件名和签名配置不能为空。");
                return;
            }

            if (!File.Exists(PluginPrivateKeyFile))
            {
                ShowError("插件清单签名私钥不存在，请检查路径。");
                return;
            }

            var selectedPackages = PluginPackages.Where(item => item.IsSelected).ToList();
            if (selectedPackages.Count == 0)
            {
                ShowError("请至少勾选一个插件。");
                return;
            }

            SaveSettings();
            IsPluginBusy = true;
            PluginProgressPercent = 0;
            PluginStatus = "准备压缩插件包...";
            foreach (PluginPackageItem package in selectedPackages)
                package.Status = "待压缩";

            AppendLog($"开始压缩，共勾选 {selectedPackages.Count} 个插件。");
            var packProgress = new Progress<PluginPackProgress>(item =>
            {
                PluginProgressPercent = item.Percent;
                PluginStatus = item.Message;
            });

            string manifestPath = await Task.Run(() => _pluginPackService.PackageAsync(
                selectedPackages, PluginZipOutputRoot, PluginManifestFileName, PluginKeyId, packProgress)
                .GetAwaiter().GetResult());

            // 对清单原始字节签名；上传与客户端验签必须使用同一份字节
            byte[] manifestBytes = await File.ReadAllBytesAsync(manifestPath);
            byte[] signature = await Task.Run(() =>
                YF_PluginManifestSigner.Sign(manifestBytes, PluginPrivateKeyFile));
            await File.WriteAllBytesAsync(manifestPath + ".sig", signature);

            PluginManifestPath = manifestPath;
            PluginProgressPercent = 100;
            PluginStatus = $"压缩完成，清单已生成：{Path.GetFileName(manifestPath)}";
            AppendLog($"压缩完成，插件清单：{manifestPath}");
            UploadPluginsCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            PluginStatus = $"压缩失败：{ex.Message}";
            AppendLog(PluginStatus);
            ShowError(ex.Message);
        }
        finally
        {
            IsPluginBusy = false;
            CompressPluginsCommand.RaiseCanExecuteChanged();
            UploadPluginsCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>只上传当前目录中已经压缩好的 ZIP 和清单。</summary>
    private async Task UploadPluginsAsync()
    {
        try
        {
            ReloadCredentials(silent: true);
            if (_credentials == null)
            {
                ShowError("未读取到有效密钥，请检查 SecretKey.csv。");
                return;
            }

            string manifestPath = Path.Combine(PluginZipOutputRoot, PluginManifestFileName);
            if (!File.Exists(manifestPath))
            {
                ShowError("当前目录没有插件清单，请先执行“仅压缩”。");
                return;
            }

            if (string.IsNullOrWhiteSpace(PluginRemotePrefix))
            {
                ShowError("COS 插件前缀不能为空。");
                return;
            }

            SaveSettings();
            IsPluginBusy = true;
            PluginProgressPercent = 0;
            PluginStatus = "准备上传已压缩插件包...";
            AppendLog($"开始上传已压缩插件包，清单：{manifestPath}");

            var uploadProgress = new Progress<UploadProgressInfo>(info =>
            {
                PluginProgressPercent = info.OverallPercent;
                PluginStatus = info.Message;
            });

            await _pluginPackService.UploadManifestAsync(
                _credentials,
                Bucket.Trim(),
                Region.Trim(),
                manifestPath,
                PluginRemotePrefix,
                PluginManifestRemotePrefix,
                _uploadService,
                uploadProgress);

            PluginManifest manifest = _pluginPackService.LoadManifest(manifestPath);
            foreach (PluginPackageItem package in PluginPackages)
            {
                if (manifest.Plugins.Any(item =>
                        string.Equals(item.Name, package.FolderName, StringComparison.OrdinalIgnoreCase)))
                {
                    package.Status = "已上传";
                }
            }

            PluginProgressPercent = 100;
            PluginStatus = "已压缩插件包和清单全部上传成功";
            AppendLog("上传完成，未重新压缩。");
        }
        catch (Exception ex)
        {
            PluginStatus = $"上传失败：{ex.Message}";
            AppendLog(PluginStatus);
            ShowError(ex.Message);
        }
        finally
        {
            IsPluginBusy = false;
            CompressPluginsCommand.RaiseCanExecuteChanged();
            UploadPluginsCommand.RaiseCanExecuteChanged();
        }
    }

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
            Environment = Environment,
            PluginSourceRoot = PluginSourceRoot,
            PluginZipOutputRoot = PluginZipOutputRoot,
            PluginRemotePrefix = PluginRemotePrefix,
            PluginBaseUrl = PluginBaseUrl,
            PluginManifestRemotePrefix = PluginManifestRemotePrefix,
            PluginManifestBaseUrl = PluginManifestBaseUrl,
            PluginManifestFileName = PluginManifestFileName,
            PluginPrivateKeyFile = PluginPrivateKeyFile,
            PluginKeyId = PluginKeyId,
            // 扫描失败时保留原勾选，避免保存动作清空用户选择
            SelectedPlugins = PluginPackages.Count > 0
                ? PluginPackages.Where(item => item.IsSelected)
                    .Select(item => item.FolderName)
                    .ToList()
                : _settings.SelectedPlugins
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
